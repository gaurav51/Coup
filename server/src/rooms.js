import crypto from "node:crypto";
import { CoupGame } from "./game.js";
import { makeRng } from "./deck.js";
import { RoomError } from "./errors.js";
import { fallback } from "./bots.js";
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

/**
 * A public table stays open this long for humans to arrive. Four is enough to
 * play, so the match starts the moment a fourth sits down rather than holding
 * out for a full six. If the window lapses short of that, bots take the empty
 * seats so nobody is left waiting alone.
 *
 * Filling with bots costs the humans their rewards, and that is deliberate:
 * completeMatch only pays out when every seat is human, so a bot-filled table
 * cannot be farmed.
 */
const QUEUE_WINDOW_MS = 40000;
const QUEUE_START_HUMANS = 4;
const QUEUE_SEATS = 4;
/**
 * Ranked keeps its old behaviour: it pairs as soon as two humans are waiting
 * and never seats a bot. Filling a ranked table would make it unratable, since
 * completeMatch only adjusts Elo when every seat is human, so the wait would
 * buy nothing.
 */
const RANKED_START_HUMANS = 2;

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

  create({ name, socketId, accountId = null, mode = "private" }) {
    if (this.findBySocket(socketId)) throw new RoomError("You are already at a table.");
    if (accountId && this.activeSeat(accountId)) throw new RoomError("Resume or leave your current table first.");
    const cleaned = cleanName(name);
    const room = new Room(uniqueCode(this.rooms));
    room.mode = mode;
    const player = room.addPlayer(cleaned, socketId);
    player.accountId = accountId;
    this.rooms.set(room.code, room);
    return this.lobbyResult(room, player);
  }

  join({ code, name, socketId, accountId = null }) {
    const room = this.requireRoom(code);
    if (room.status !== "lobby") throw new RoomError("That table has already started.");
    if (room.players.length >= MAX_PLAYERS) throw new RoomError("This table is full.");
    if (this.findBySocket(socketId)) throw new RoomError("You are already at a table.");

    if (accountId && this.activeSeat(accountId)) throw new RoomError("Resume or leave your current table first.");
    const cleaned = cleanName(name);
    if (room.players.some((player) => player.name.toLowerCase() === cleaned.toLowerCase())) {
      throw new RoomError("That name is already at the table.");
    }
    const player = room.addPlayer(cleaned, socketId);
    player.accountId = accountId;
    return this.lobbyResult(room, player);
  }

  leave(socketId, abandon = true) {
    const found = this.findBySocket(socketId);
    if (!found) throw new RoomError("You are not at a table.");
    const { room, player } = found;

    if (room.status !== "lobby") {
      if (abandon) player.abandoned = true;
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
    if (this.retainDisconnectedLobbies && found.room.mode === "private" && found.room.status === "lobby") {
      found.player.connected = false;
      found.player.socketId = null;
      return { kind: "lobby", code: found.room.code, recipients: this.lobbyRecipients(found.room) };
    }
    return RoomStore.prototype.leave.call(this, socketId, false);
  }

  resume({ code, token, socketId, accountId = null }) {
    const room = this.requireRoom(code);
    const player = room.players.find((seat) => seat.token === token);
    if (!player || player.abandoned) throw new RoomError("That seat does not exist.");
    if (player.accountId && player.accountId !== accountId) throw new RoomError("That seat belongs to another account.");
    const current = this.findBySocket(socketId);
    if (current && current.player !== player) throw new RoomError("You are already at another seat.");

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
    if (room.mode !== "private" && botCount > 0) throw new RoomError("Public matches do not allow bots.");
    this.addBots(room, botCount);
    if (room.players.length < MIN_PLAYERS) throw new RoomError("Coup needs at least two players.");
    if (room.players.some((seat) => !seat.ready)) throw new RoomError("Everyone must be ready.");
    if (room.players.some((seat) => !seat.connected)) throw new RoomError("A player is disconnected.");

    // Explicit seeds are for direct engine tests only; the network layer never accepts them.
    const deal = Number.isInteger(seed) ? seed : undefined;
    room.game = new CoupGame(
      room.players.map((seat) => seat.name),
      deal,
    );
    room.matchId = crypto.randomUUID();
    room.seed = deal;
    room.status = "playing";
    room.deadline = Date.now() + 30000;
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
    player.moves = (player.moves || 0) + 1;
    this.settleQueuedBlock(room);
    room.deadline = Date.now() + (room.game.phase === "AwaitingAction" ? 30000 : 20000);
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
    room.deadline = Date.now() + (room.game.phase === "AwaitingAction" ? 30000 : 20000);
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

  activeSeat(accountId) {
    if (!accountId) return null;
    for (const room of this.rooms.values()) {
      if (room.status === "over") continue;
      const player = room.players.find(p => p.accountId === accountId && !p.abandoned);
      if (player) return { code: room.code, token: player.token, status: room.status, mode: room.mode };
    }
    return null;
  }

  recover({ accountId, socketId }) {
    const seat = this.activeSeat(accountId);
    if (!seat) throw new RoomError("No active table to resume.");
    return RoomStore.prototype.resume.call(this, { ...seat, accountId, socketId });
  }

  quickJoin({ accountId, socketId, name, mode }) {
    if (!accountId || !["casual", "ranked"].includes(mode)) throw new RoomError("Invalid matchmaking request.");
    if (this.activeSeat(accountId)) throw new RoomError("Resume or leave your current table first.");
    const waiting = [...this.rooms.values()].find(r => r.mode === mode && r.status === "lobby"
      && r.players.length < MAX_PLAYERS && r.players.some(p => p.connected));
    let result;
    if (waiting) {
      // Public names need not be globally unique; the account owns the seat.
      let safeName = cleanName(name);
      if (waiting.players.some(p => p.name.toLowerCase() === safeName.toLowerCase())) safeName = safeName.slice(0, 13) + " II";
      result = RoomStore.prototype.join.call(this, { code: waiting.code, name: safeName, accountId, socketId });
    } else result = RoomStore.prototype.create.call(this, { accountId, socketId, name, mode });
    const room = this.peek(result.code);
    const player = room.players.find(p => p.socketId === socketId);
    player.ready = true;
    // The window belongs to the table, not to each arrival, so a late joiner
    // never resets the wait for whoever has been sitting there already.
    if (room.mode === "casual") room.queueDeadline = room.queueDeadline ?? Date.now() + QUEUE_WINDOW_MS;
    const needed = room.mode === "casual" ? QUEUE_START_HUMANS : RANKED_START_HUMANS;
    if (room.players.filter(p => !p.bot && p.connected).length >= needed) {
      const started = RoomStore.prototype.startQueued.call(this, room.code);
      if (started) return { ...started, token: result.token, seat: result.seat };
    }
    return this.lobbyResult(room, player);
  }

  /**
   * Starts a queued public table: either a fourth human arrived, or the window
   * lapsed and bots are taking the empty seats.
   *
   * start() refuses bots in a public match so nobody can farm rewards against
   * them. That guard stays in place for client commands; this path seats the
   * bots itself and then starts with a botCount of zero. Nothing becomes
   * farmable, because completeMatch pays out only when every seat is human.
   */
  startQueued(code) {
    const room = this.peek(code);
    if (!room || room.status !== "lobby" || room.mode === "private") return null;
    if (!room.players.some(seat => !seat.bot && seat.connected)) return null;
    if (room.mode === "casual") this.addBots(room, Math.max(0, QUEUE_SEATS - room.players.length));
    if (room.players.length < MIN_PLAYERS) return null;
    for (const seat of room.players) seat.ready = true;
    room.queueDeadline = null;
    const host = room.players.find(seat => seat.token === room.hostToken && seat.socketId)
      ?? room.players.find(seat => seat.socketId);
    if (!host) return null;
    return RoomStore.prototype.start.call(this, host.socketId, undefined, 0);
  }

  /** Public tables whose window has lapsed. The socket sweep starts each one. */
  expiredQueues(now = Date.now()) {
    return [...this.rooms.values()]
      .filter(room => room.status === "lobby" && room.mode === "casual" && room.queueDeadline && room.queueDeadline <= now)
      .map(room => room.code);
  }

  timeout(code, now = Date.now()) {
    const room = this.peek(code);
    if (!room?.game || room.game.phase === "GameOver" || !room.deadline || room.deadline > now) return null;
    const game = room.game;
    let seat = game.phase === "AwaitingAction" ? game.currentPlayerIndex :
      game.phase === "AwaitingInfluenceLoss" ? game.pendingLoserIndex :
      game.phase === "AwaitingExchangeSelection" ? game.actingPlayerIndex : [...game.responders].sort((a,b) => a-b)[0];
    if (seat == null) return null;
    const command = fallback(game, seat);
    if (!command) return null;
    const before = game.events.length;
    this.apply(room, seat, command);
    this.settleQueuedBlock(room);
    room.deadline = now + (game.phase === "AwaitingAction" ? 30000 : 20000);
    return { kind: "game", ...this.snapshot(room, { from: before }) };
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
      deadline: room.deadline || null,
      serverTime: Date.now(),
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
      queueDeadline: room.queueDeadline ?? null,
      serverTime: Date.now(),
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
  if (!cleaned || /[<>\u0000-\u001f\u007f]/.test(cleaned)) throw new RoomError("Enter a valid name without markup.");
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
    this.mode = "private";
    this.createdAt = Date.now();
    this.matchId = null;
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

/** Includes secrets and hidden cards: database only, never part of the wire protocol. */
export function saveRoom(room) {
  const { game, players, ...state } = room;
  return {
    version: 1, state: structuredClone(state), game: game?.snapshot() ?? null,
    players: players.map(({ rng, ...player }) => ({ ...structuredClone(player), rngState: rng?.getState() ?? null })),
  };
}

export function restoreRoom(saved, disconnect = false) {
  if (saved?.version !== 1) throw new RoomError("Unsupported saved room version.");
  const room = Object.assign(new Room(saved.state.code), structuredClone(saved.state));
  room.game = saved.game ? CoupGame.restore(saved.game) : null;
  room.players = saved.players.map(({ rngState, ...player }) => ({ ...structuredClone(player),
    ...(rngState == null ? {} : { rng: makeRng(rngState) }),
    ...(disconnect && !player.bot ? { connected: false, socketId: null } : {}),
  }));
  return room;
}
