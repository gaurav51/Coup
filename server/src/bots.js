import { decide } from "./bot-mind.js";
import { GameError, RoomError } from "./errors.js";
import { canBlockWith } from "./rules.js";

const timers = new Map();

/** How long a bot studies the table before it answers. Matches MatchController.botThinkTime. */
export const BOT_THINK_MS = 900;

/** Quiet pause after an ordinary resolution. Matches MatchController.resolveBeat. */
export const RESOLVE_BEAT_MS = 450;

/**
 * How long the table holds after these events, before the next bot may act.
 * A challenge has to stay up long enough to show the card, and a proven claim
 * then has to finish its swap back into the deck. Same beats as the Unity pump.
 */
export function reactionBeat(events) {
  let beat = RESOLVE_BEAT_MS;
  for (const event of events ?? []) {
    if (event.kind === "CardReplaced") beat = Math.max(beat, 2300);
    else if (event.kind === "ChallengeWon") beat = Math.max(beat, 1300);
    else if (event.kind === "ChallengeDeclared") beat = Math.max(beat, 950);
    else if (event.kind === "InfluenceLost" && event.reason === "FailedChallenge") beat = Math.max(beat, 1450);
  }
  return beat;
}

/** Think time, plus the beat for the events that just landed. Opening the table is think time only. */
export function delayBeforeBot(events) {
  if (!events?.length) return BOT_THINK_MS;
  return reactionBeat(events) + BOT_THINK_MS;
}

/**
 * The next bot who owes a decision, or null when a person still has to answer.
 * Response windows move in seat order, lowest first, the same as GameDriver.
 */
export function nextBotSeat(room) {
  const game = room?.game;
  if (!game || game.phase === "GameOver") return null;
  const isBot = (seat) => room.players[seat]?.bot === true;

  if (game.phase === "AwaitingAction") {
    return isBot(game.currentPlayerIndex) ? game.currentPlayerIndex : null;
  }
  if (game.phase === "AwaitingInfluenceLoss") {
    const seat = game.pendingLoserIndex;
    return seat != null && isBot(seat) ? seat : null;
  }
  if (game.phase === "AwaitingExchangeSelection") {
    const seat = game.actingPlayerIndex;
    return seat != null && isBot(seat) ? seat : null;
  }
  if (
    game.phase === "AwaitingChallenge" ||
    game.phase === "AwaitingBlock" ||
    game.phase === "AwaitingBlockChallenge"
  ) {
    const next = [...game.responders].sort((a, b) => a - b)[0];
    return next != null && isBot(next) ? next : null;
  }
  return null;
}

/** A legal command for the bot, or a plain pass/income if the clever choice cannot be played. */
export function botCommand(game, playerOrSeat) {
  const player = typeof playerOrSeat === "number" ? { seat: playerOrSeat } : playerOrSeat;
  const seat = player.seat ?? player.index;
  if (player.seat == null) player.seat = seat;
  const chosen = decide(game, player);
  if (chosen && acceptable(game, seat, chosen)) return chosen;
  return fallback(game, seat);
}

function acceptable(game, seat, command) {
  if (command.type === "declare") return game.canDeclare(seat, command.action, command.target ?? -1);
  if (command.type === "challenge" || command.type === "pass") return game.responders.has(seat);
  if (command.type === "block") {
    return game.responders.has(seat) && canBlockWith(game.pendingAction, command.role);
  }
  if (command.type === "lose") return game.players[seat].influence.includes(command.role);
  if (command.type === "exchange") return keepFits(game, command.keep);
  return false;
}

function keepFits(game, keep) {
  if (!Array.isArray(keep) || keep.length !== game.exchangeKeepCount) return false;
  const pool = game.exchangePool();
  for (const role of keep) {
    const at = pool.indexOf(role);
    if (at < 0) return false;
    pool.splice(at, 1);
  }
  return true;
}

function fallback(game, seat) {
  if (game.phase === "AwaitingAction") {
    const legal = game.legalActions(seat);
    const choice = legal.find((item) => item.action === "Income") ?? legal[0];
    if (!choice) return null;
    return { type: "declare", action: choice.action, target: choice.target ?? -1 };
  }
  if (game.phase === "AwaitingInfluenceLoss") {
    const card = game.players[seat].influence[0];
    return card ? { type: "lose", role: card } : null;
  }
  if (game.phase === "AwaitingExchangeSelection") {
    return { type: "exchange", keep: game.exchangePool().slice(0, game.exchangeKeepCount) };
  }
  if (game.responders.has(seat)) return { type: "pass" };
  return null;
}

export function scheduleBots(store, code, deliver, events = []) {
  const pending = timers.get(code);
  if (pending) clearTimeout(pending);
  timers.delete(code);

  const room = store.peek(code);
  if (!room?.game || room.game.phase === "GameOver" || nextBotSeat(room) == null) return;

  const delay = delayBeforeBot(events);
  const timer = setTimeout(() => {
    timers.delete(code);
    const live = store.peek(code);
    const actor = nextBotSeat(live);
    if (actor == null) return;
    const player = live.players[actor];
    let command = botCommand(live.game, player);
    if (!command) {
      console.error(`[coup] ${player.name} has no legal move`);
      return;
    }
    try {
      deliver(store.actSeat(code, actor, command));
    } catch (err) {
      const plain = fallback(live.game, actor);
      if (!plain || sameCommand(plain, command)) {
        if (!(err instanceof RoomError) && !(err instanceof GameError)) console.error(err);
        return;
      }
      command = plain;
      try {
        deliver(store.actSeat(code, actor, command));
      } catch (again) {
        console.error(again);
        return;
      }
    }
  }, delay);
  timers.set(code, timer);
}

function sameCommand(a, b) {
  return JSON.stringify(a) === JSON.stringify(b);
}
