import crypto from "node:crypto";
import { CoupGame } from "./game.js";
import { makeRng } from "./deck.js";
import { RoomError } from "./errors.js";
import { PERSONALITIES } from "./bot-mind.js";
import {
  FORCED_COUP_THRESHOLD,
  MAX_PLAYERS,
  MIN_PLAYERS,
  actionLabel,
  blockingRoles,
  canBlockWith,
  parseAction,
  parseRole,
} from "./rules.js";

const ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

/**
 * In-memory tables. Each match is one CoupGame. Sockets never touch the engine
 * directly, so a command can queue a block through the challenge window and the
 * private view can hide every other hand.
 */
export class RoomStore {
  constructor() {
    this.rooms = new Map();
  }

  create({ name, socketId }) {
    if (this.findBySocket(socketId)) throw new RoomError("You are already at a table.");
    const cleaned = cleanName(name);
    const room = new Room(uniqueCode(this.rooms));
    const player = room.addPlayer(cleaned, socketId);
    this.rooms.set(room.code, room);
    return this.lobbyResult(room, player);
  }

  join({ code, name, socketId }) {
    const room = this.requireRoom(code);
    if (room.status !== "lobby") throw new RoomError("That table has already started.");
    if (room.players.length >= MAX_PLAYERS) throw new RoomError("This table is full.");
    if (this.findBySocket(socketId)) throw new RoomError("You are already at a table.");

    const cleaned = cleanName(name);
    if (room.players.some((player) => player.name.toLowerCase() === cleaned.toLowerCase())) {
      throw new RoomError("That name is already at the table.");
    }
    const player = room.addPlayer(cleaned, socketId);
    return this.lobbyResult(room, player);
  }

  leave(socketId) {
    const found = this.findBySocket(socketId);
    if (!found) throw new RoomError("You are not at a table.");
    const { room, player } = found;

    if (room.status !== "lobby") {
      player.connected = false;
      player.socketId = null;
      return {
        kind: "game",
        ...this.snapshot(room, { from: room.game.events.length, includeLobby: true }),
      };
    }

    room.players = room.players.filter((seat) => seat !== player);
    if (room.players.length === 0) {
      this.rooms.delete(room.code);
      return { kind: "lobby", code: room.code, recipients: [] };
    }
    if (room.hostToken === player.token) room.hostToken = room.players[0].token;
    room.reindex();
    return { kind: "lobby", code: room.code, recipients: this.lobbyRecipients(room) };
  }

  disconnect(socketId) {
    const found = this.findBySocket(socketId);
    if (!found) return null;
    return this.leave(socketId);
  }

  resume({ code, token, socketId }) {
    const room = this.requireRoom(code);
    const player = room.players.find((seat) => seat.token === token);
    if (!player) throw new RoomError("That seat does not exist.");

    const replacedSocketId = player.socketId && player.socketId !== socketId ? player.socketId : null;
    for (const other of this.rooms.values()) {
      for (const seat of other.players) {
        if (seat !== player && seat.socketId === socketId) {
          seat.socketId = null;
          seat.connected = false;
        }
      }
    }

    player.socketId = socketId;
    player.connected = true;
    if (room.game) {
      return {
        kind: "game",
        token: player.token,
        seat: player.seat,
        replacedSocketId,
        ...this.snapshot(room, { fullLog: true, includeLobby: true }),
      };
    }
    const lobby = this.lobbyResult(room, player);
    lobby.replacedSocketId = replacedSocketId;
    return lobby;
  }

  setReady(socketId, ready) {
    const { room, player } = this.requireLobby(socketId);
    player.ready = typeof ready === "boolean" ? ready : !player.ready;
    return { kind: "lobby", code: room.code, recipients: this.lobbyRecipients(room) };
  }

  start(socketId, seed, botCount = 0) {
    const { room, player } = this.requireLobby(socketId);
    if (player.token !== room.hostToken) throw new RoomError("Only the host can start.");
    this.addBots(room, botCount);
    if (room.players.length < MIN_PLAYERS) throw new RoomError("Coup needs at least two players.");
    if (room.players.some((seat) => !seat.ready)) throw new RoomError("Everyone must be ready.");
    if (room.players.some((seat) => !seat.connected)) throw new RoomError("A player is disconnected.");

    const deal = Number.isInteger(seed) ? seed : crypto.randomInt(1, 2 ** 31 - 1);
    room.game = new CoupGame(
      room.players.map((seat) => seat.name),
      deal,
    );
    room.seed = deal;
    room.status = "playing";
    room.queuedBlock = null;
    for (const seat of room.players) {
      if (!seat.bot) continue;
      seat.rng = makeRng((deal * 31 + seat.seat + 1) >>> 0);
    }
    return { kind: "game", ...this.snapshot(room, { fullLog: true, includeLobby: true }) };
  }

  act(socketId, command) {
    const found = this.findBySocket(socketId);
    if (!found) throw new RoomError("You are not at a table.");
    const { room, player } = found;
    if (!room.game || room.status === "lobby") throw new RoomError("The match has not started.");
    if (room.game.phase === "GameOver") throw new RoomError("The game is over.");
    if (!player.connected) throw new RoomError("You are disconnected.");

    const before = room.game.events.length;
    this.apply(room, player.seat, command);
    this.settleQueuedBlock(room);
    return { kind: "game", ...this.snapshot(room, { from: before }) };
  }

  actSeat(code, seat, command) {
    const room = this.requireRoom(code);
    const player = room.players[seat];
    if (!player?.bot) throw new RoomError("That seat is not a bot.");
    if (!room.game || room.game.phase === "GameOver") throw new RoomError("The game is over.");

    const before = room.game.events.length;
    this.apply(room, seat, command);
    this.settleQueuedBlock(room);
    return { kind: "game", ...this.snapshot(room, { from: before }) };
  }

  addBots(room, count) {
    const names = ["Valca", "Orsin", "Meridia", "Tarron", "Sable"];
    const wanted = Math.max(0, Math.min(MAX_PLAYERS - room.players.length, Number(count) || 0));
    let added = 0;
    let cursor = 0;
    while (added < wanted && cursor < 24) {
      let name = names[cursor % names.length];
      if (cursor >= names.length) name = `${names[cursor % names.length]} ${cursor}`;
      cursor += 1;
      if (room.players.some((seat) => seat.name.toLowerCase() === name.toLowerCase())) continue;
      const bot = room.addPlayer(name, null);
      bot.bot = true;
      bot.ready = true;
      bot.connected = true;
      bot.socketId = null;
      bot.personality = PERSONALITIES[added % PERSONALITIES.length];
      added += 1;
    }
  }

  peek(code) {
    return this.rooms.get(String(code || "").trim().toUpperCase()) ?? null;
  }

  apply(room, seat, command) {
    const game = room.game;
    switch (command.type) {
      case "declare":
        room.queuedBlock = null;
        game.declare(seat, command.action, command.target ?? -1);
        return;
      case "challenge":
        if (game.phase === "AwaitingBlockChallenge") game.challengeBlock(seat);
        else game.challenge(seat);
        return;
      case "pass":
        if (game.phase === "AwaitingChallenge") game.passChallenge(seat);
        else if (game.phase === "AwaitingBlock") game.passBlock(seat);
        else if (game.phase === "AwaitingBlockChallenge") game.passBlockChallenge(seat);
        else throw new RoomError("Nothing to pass on.");
        return;
      case "block":
        this.playBlock(room, seat, command.role);
        return;
      case "lose":
        game.chooseInfluenceToLose(seat, command.role);
        return;
      case "exchange":
        if (seat !== game.actingPlayerIndex) throw new RoomError("It is not your exchange.");
        game.chooseExchangeKeep(command.keep);
        return;
      default:
        throw new RoomError("Unknown command.");
    }
  }

  /**
   * Steal and Assassinate are challenged before they can be blocked. The victim
   * may already know they want to block, so remember the claim and step them out
   * of the challenge. It is played the moment the block window opens.
   */
  playBlock(room, seat, role) {
    const game = room.game;
    if (!game.pendingAction) throw new RoomError("Nothing to block.");
    if (!canBlockWith(game.pendingAction, role)) {
      throw new RoomError(`${role} cannot block ${actionLabel(game.pendingAction)}.`);
    }

    if (game.phase === "AwaitingChallenge") {
      if (!game.mayBlock(seat) || !game.responders.has(seat)) {
        throw new RoomError("You cannot block this action.");
      }
      room.queuedBlock = { seat, role };
      game.passChallenge(seat);
      return;
    }

    game.block(seat, role);
  }

  settleQueuedBlock(room) {
    const queued = room.queuedBlock;
    const game = room.game;
    if (!queued || !game) return;

    if (
      game.phase === "AwaitingBlock" &&
      game.responders.has(queued.seat) &&
      game.mayBlock(queued.seat) &&
      canBlockWith(game.pendingAction, queued.role)
    ) {
      room.queuedBlock = null;
      game.block(queued.seat, queued.role);
      return;
    }

    if (game.phase === "AwaitingChallenge" || game.phase === "AwaitingInfluenceLoss") return;
    room.queuedBlock = null;
  }

  viewFor(room, player) {
    const game = room.game;
    const seat = player.seat;
    const self = game.players[seat];
    const queued = room.queuedBlock?.seat === seat ? room.queuedBlock.role : null;
    const exchange =
      game.phase === "AwaitingExchangeSelection" && game.actingPlayerIndex === seat
        ? { pool: game.exchangePool(), keep: game.exchangeKeepCount }
        : null;
    const legalActions = game.legalActions(seat);
    const challenge =
      (game.phase === "AwaitingChallenge" || game.phase === "AwaitingBlockChallenge") &&
      game.responders.has(seat);
    const pass = challenge || (game.phase === "AwaitingBlock" && game.responders.has(seat));
    const blocks = blockOptions(game, seat, room.queuedBlock);
    const lose = game.phase === "AwaitingInfluenceLoss" && game.pendingLoserIndex === seat;

    return {
      you: seat,
      phase: game.phase,
      turn: game.turnNumber,
      currentPlayer: game.currentPlayerIndex,
      deckCount: game.deckCount,
      yourTurn: legalActions.length > 0 || challenge || pass || blocks.length > 0 || lose || exchange != null,
      hand: [...self.influence],
      players: game.players.map((other, index) => ({
        seat: other.index,
        name: other.name,
        coins: other.coins,
        influence: other.influence.length,
        revealed: [...other.revealed],
        alive: other.isAlive,
        connected: room.players[index]?.connected ?? false,
        ready: room.players[index]?.ready ?? true,
        host: room.players[index]?.token === room.hostToken,
        mustCoup: other.isAlive && other.coins >= FORCED_COUP_THRESHOLD,
        you: other.index === seat,
      })),
      pending: game.pending
        ? {
            actor: game.pending.actorIndex,
            action: game.pending.kind,
            target: game.pending.targetIndex >= 0 ? game.pending.targetIndex : null,
            claim: game.pending.claimedRole,
            blocker: game.pending.blockerIndex >= 0 ? game.pending.blockerIndex : null,
            blockClaim: game.pending.blockClaim,
          }
        : null,
      waitingOn: [...game.responders].sort((a, b) => a - b),
      queuedBlock: queued,
      lossReason: game.pendingLossReason,
      loser: game.pendingLoserIndex,
      winner:
        game.winnerIndex == null
          ? null
          : { seat: game.winnerIndex, name: game.players[game.winnerIndex].name },
      allowed: {
        declare: legalActions,
        challenge,
        pass,
        block: blocks,
        lose,
        exchange,
      },
    };
  }

  lobbyView(room, player) {
    return {
      code: room.code,
      status: room.status,
      you: {
        name: player.name,
        seat: player.seat,
        ready: player.ready,
        host: player.token === room.hostToken,
      },
      players: room.players.map((seat) => ({
        name: seat.name,
        seat: seat.seat,
        ready: seat.ready,
        connected: seat.connected,
        host: seat.token === room.hostToken,
        you: seat.token === player.token,
      })),
    };
  }

  snapshot(room, { from = 0, fullLog = false, includeLobby = false } = {}) {
    const states = room.players
      .filter((player) => player.connected && player.socketId)
      .map((player) => ({
        socketId: player.socketId,
        state: this.viewFor(room, player),
      }));

    let over = null;
    if (room.game.phase === "GameOver" && !room.overAnnounced) {
      room.overAnnounced = true;
      room.status = "over";
      const seat = room.game.winnerIndex;
      over = { seat, name: seat == null ? null : room.game.players[seat].name };
    }

    return {
      code: room.code,
      events: fullLog ? [] : room.game.events.slice(from),
      fullLog: fullLog ? room.game.events.slice() : null,
      states,
      lobbies: includeLobby || over ? this.lobbyRecipients(room) : null,
      over,
    };
  }

  lobbyRecipients(room) {
    return room.players
      .filter((player) => player.connected && player.socketId)
      .map((player) => ({
        socketId: player.socketId,
        lobby: this.lobbyView(room, player),
      }));
  }

  lobbyResult(room, player) {
    return {
      kind: "lobby",
      code: room.code,
      token: player.token,
      seat: player.seat,
      recipients: this.lobbyRecipients(room),
    };
  }

  requireRoom(code) {
    const room = this.peek(code);
    if (!room) throw new RoomError("That table does not exist.");
    return room;
  }

  requireLobby(socketId) {
    const found = this.findBySocket(socketId);
    if (!found) throw new RoomError("You are not at a table.");
    if (found.room.status !== "lobby") throw new RoomError("The match has already started.");
    return found;
  }

  findBySocket(socketId) {
    if (!socketId) return null;
    for (const room of this.rooms.values()) {
      const player = room.players.find((seat) => seat.socketId === socketId);
      if (player) return { room, player };
    }
    return null;
  }
}

export function parseCommand(type, payload) {
  const data = payload && typeof payload === "object" ? payload : {};
  switch (type) {
    case "declare": {
      const action = parseAction(data.action);
      if (!action) throw new RoomError("Unknown action.");
      return { type, action, target: parseTarget(data.target) };
    }
    case "challenge":
      return { type };
    case "pass":
      return { type };
    case "block": {
      const role = parseRole(data.role);
      if (!role) throw new RoomError("Unknown role.");
      return { type, role };
    }
    case "lose": {
      const role = parseRole(data.role);
      if (!role) throw new RoomError("Unknown role.");
      return { type, role };
    }
    case "exchange": {
      if (!Array.isArray(data.keep) || data.keep.length === 0) {
        throw new RoomError("Choose cards to keep.");
      }
      const keep = data.keep.map((role) => {
        const parsed = parseRole(role);
        if (!parsed) throw new RoomError("Unknown role.");
        return parsed;
      });
      return { type, keep };
    }
    default:
      throw new RoomError("Unknown command.");
  }
}

function blockOptions(game, seat, queued) {
  if (!game.pendingAction || queued) return [];
  const roles = blockingRoles(game.pendingAction);
  if (roles.length === 0 || !game.mayBlock(seat)) return [];
  if (game.phase === "AwaitingBlock" && game.responders.has(seat)) return roles;
  if (game.phase === "AwaitingChallenge" && game.responders.has(seat)) return roles;
  return [];
}

function parseTarget(value) {
  if (value == null || value === "") return -1;
  const target = Number(value);
  if (!Number.isInteger(target)) throw new RoomError("Target must be a seat number.");
  return target;
}

function cleanName(name) {
  if (typeof name !== "string") throw new RoomError("Enter a name.");
  const cleaned = name.trim().replace(/\s+/g, " ").slice(0, 16);
  if (!cleaned) throw new RoomError("Enter a name.");
  return cleaned;
}

function uniqueCode(rooms) {
  for (let attempt = 0; attempt < 20; attempt += 1) {
    const bytes = crypto.randomBytes(4);
    let code = "";
    for (let i = 0; i < 4; i += 1) code += ALPHABET[bytes[i] % ALPHABET.length];
    if (!rooms.has(code)) return code;
  }
  throw new RoomError("Could not open a table. Try again.");
}

class Room {
  constructor(code) {
    this.code = code;
    this.hostToken = null;
    this.players = [];
    this.status = "lobby";
    this.game = null;
    this.seed = null;
    this.queuedBlock = null;
    this.overAnnounced = false;
  }

  addPlayer(name, socketId) {
    const player = {
      token: crypto.randomBytes(18).toString("base64url"),
      name,
      socketId,
      ready: false,
      connected: true,
      bot: false,
      seat: this.players.length,
    };
    this.players.push(player);
    if (this.players.length === 1) this.hostToken = player.token;
    return player;
  }

  reindex() {
    this.players.forEach((player, index) => {
      player.seat = index;
    });
  }
}
