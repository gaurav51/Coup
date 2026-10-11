import { Deck } from "./deck.js";
import { GameError } from "./errors.js";
import {
  ACTIONS,
  EXCHANGE_DRAW_COUNT,
  FOREIGN_AID_AMOUNT,
  FORCED_COUP_THRESHOLD,
  INCOME_AMOUNT,
  MAX_PLAYERS,
  MIN_PLAYERS,
  STARTING_COINS,
  STARTING_INFLUENCE,
  STEAL_AMOUNT,
  TAX_AMOUNT,
  TWO_PLAYER_STARTING_COINS,
  actionLabel,
  canBlockWith,
  claimedRole,
  coinCost,
  isBlockable,
  isChallengeable,
  onlyTargetMayBlock,
  requiresTarget,
} from "./rules.js";

/**
 * Authoritative Coup rules. Call the declare/choose methods and read `events`.
 * Hidden cards stay on the player objects; the room layer decides what a socket may see.
 */
export class CoupGame {
  constructor(playerNames, seed = 1) {
    if (!Array.isArray(playerNames) || playerNames.length < MIN_PLAYERS) {
      throw new GameError("Coup needs at least two players.");
    }
    if (playerNames.length > MAX_PLAYERS) {
      throw new GameError("Coup allows at most six players.");
    }

    this.deck = new Deck(seed);
    this.players = [];
    this.events = [];
    this.responders = new Set();
    this.pendingLosses = [];
    this.exchangeDrawn = [];
    this.pending = null;
    this.postLoss = "none";
    this.exchangeKeepCount = 0;
    this.phase = "AwaitingAction";
    this.currentPlayerIndex = 0;
    this.turnNumber = 1;

    const twoPlayer = playerNames.length === 2;
    for (let i = 0; i < playerNames.length; i += 1) {
      const coins = twoPlayer && i === 0 ? TWO_PLAYER_STARTING_COINS : STARTING_COINS;
      const player = new Player(i, String(playerNames[i]), coins);
      for (let c = 0; c < STARTING_INFLUENCE; c += 1) player.gain(this.deck.draw());
      this.players.push(player);
    }

    this.raise({ kind: "GameStarted" });
    this.raise({ kind: "TurnStarted", player: this.currentPlayerIndex });
  }

  get deckCount() {
    return this.deck.count;
  }

  get actingPlayerIndex() {
    return this.pending ? this.pending.actorIndex : null;
  }

  get pendingAction() {
    return this.pending?.kind ?? null;
  }

  get pendingTarget() {
    return this.pending && this.pending.targetIndex >= 0 ? this.pending.targetIndex : null;
  }

  get pendingClaim() {
    return this.pending?.claimedRole ?? null;
  }

  get pendingBlocker() {
    return this.pending && this.pending.blockerIndex >= 0 ? this.pending.blockerIndex : null;
  }

  get pendingBlockClaim() {
    return this.pending?.blockClaim ?? null;
  }

  get pendingLoserIndex() {
    if (this.phase !== "AwaitingInfluenceLoss" || this.pendingLosses.length === 0) return null;
    return this.pendingLosses[0].player;
  }

  get pendingLossReason() {
    if (this.phase !== "AwaitingInfluenceLoss" || this.pendingLosses.length === 0) return null;
    return this.pendingLosses[0].reason;
  }

  get winnerIndex() {
    if (this.phase !== "GameOver") return null;
    return this.players.find((player) => player.isAlive)?.index ?? null;
  }

  copiesOf(role) {
    let n = this.deck.countOf(role);
    for (const card of this.exchangeDrawn) if (card === role) n += 1;
    for (const player of this.players) {
      for (const card of player.influence) if (card === role) n += 1;
      for (const card of player.revealed) if (card === role) n += 1;
    }
    return n;
  }

  canDeclare(actor, kind, target = -1) {
    return this.declareError(actor, kind, target) == null;
  }

  /**
   * Whether this player may claim a block. Stays true during the challenge
   * window so the victim of a steal can choose to block before everyone else
   * has finished deciding whether to challenge.
   */
  mayBlock(playerIndex) {
    if (!this.pending || playerIndex < 0 || playerIndex >= this.players.length) return false;
    if (!this.players[playerIndex].isAlive) return false;
    if (!isBlockable(this.pending.kind)) return false;
    if (onlyTargetMayBlock(this.pending.kind)) return this.pending.targetIndex === playerIndex;
    return playerIndex !== this.pending.actorIndex;
  }

  declareError(actor, kind, target = -1) {
    if (!ACTIONS.includes(kind)) return "Unknown action.";
    if (this.phase !== "AwaitingAction") return "Not accepting actions right now.";
    if (actor !== this.currentPlayerIndex) return "Not your turn.";

    const player = this.players[actor];
    if (player.coins >= FORCED_COUP_THRESHOLD && kind !== "Coup") {
      return `Holding ${player.coins} coins forces a Coup.`;
    }

    const cost = coinCost(kind);
    if (player.coins < cost) return `${actionLabel(kind)} costs ${cost} coins.`;

    if (requiresTarget(kind)) {
      if (target < 0 || target >= this.players.length) return "A target is required.";
      if (target === actor) return "You cannot target yourself.";
      if (!this.players[target].isAlive) return "That player is already out.";
    }

    return null;
  }

  legalActions(seat) {
    const legal = [];
    if (this.phase !== "AwaitingAction" || seat !== this.currentPlayerIndex) return legal;

    for (const kind of ACTIONS) {
      if (requiresTarget(kind)) {
        for (const target of this.players) {
          if (this.canDeclare(seat, kind, target.index)) {
            legal.push({ action: kind, target: target.index });
          }
        }
      } else if (this.canDeclare(seat, kind)) {
        legal.push({ action: kind });
      }
    }
    return legal;
  }

  declare(actor, kind, target = -1) {
    const error = this.declareError(actor, kind, target);
    if (error) throw new GameError(error);

    const cost = coinCost(kind);
    if (cost > 0) this.adjustCoins(actor, -cost);

    this.pending = {
      actorIndex: actor,
      kind,
      targetIndex: requiresTarget(kind) ? target : -1,
      claimedRole: claimedRole(kind),
      blockerIndex: -1,
      blockClaim: null,
    };

    this.raise({
      kind: "ActionDeclared",
      player: actor,
      target: this.pending.targetIndex >= 0 ? this.pending.targetIndex : null,
      action: kind,
      role: this.pending.claimedRole,
    });

    if (isChallengeable(kind)) this.openChallengeWindow();
    else if (isBlockable(kind)) this.openBlockWindow();
    else this.resolveAction();
  }

  passChallenge(playerIndex) {
    this.requirePhase("AwaitingChallenge");
    if (!this.responders.delete(playerIndex)) {
      throw new GameError("You are not being asked to respond.");
    }
    if (this.responders.size === 0) this.afterChallengeWindow();
  }

  challenge(challenger) {
    this.requirePhase("AwaitingChallenge");
    if (!this.responders.has(challenger)) {
      throw new GameError("You are not being asked to respond.");
    }

    const claim = this.pending.claimedRole;
    this.responders.clear();
    this.raise({
      kind: "ChallengeDeclared",
      player: challenger,
      target: this.pending.actorIndex,
      role: claim,
    });

    // A caught bluff means the action never happened, so its fee comes back.
    // A blocked action is different: that fee stays spent.
    this.resolveChallenge(this.pending.actorIndex, challenger, claim, "proceed", "fail", true);
  }

  passBlock(playerIndex) {
    this.requirePhase("AwaitingBlock");
    if (!this.responders.delete(playerIndex)) {
      throw new GameError("You are not being asked to respond.");
    }
    if (this.responders.size === 0) this.resolveAction();
  }

  block(blocker, withRole) {
    this.requirePhase("AwaitingBlock");
    if (!this.responders.has(blocker)) {
      throw new GameError("You are not being asked to respond.");
    }
    if (!canBlockWith(this.pending.kind, withRole)) {
      throw new GameError(`${withRole} cannot block ${actionLabel(this.pending.kind)}.`);
    }

    this.pending.blockerIndex = blocker;
    this.pending.blockClaim = withRole;
    this.responders.clear();
    this.raise({
      kind: "BlockDeclared",
      player: blocker,
      target: this.pending.actorIndex,
      action: this.pending.kind,
      role: withRole,
    });
    this.openBlockChallengeWindow();
  }

  passBlockChallenge(playerIndex) {
    this.requirePhase("AwaitingBlockChallenge");
    if (!this.responders.delete(playerIndex)) {
      throw new GameError("You are not being asked to respond.");
    }
    if (this.responders.size === 0) this.failAction();
  }

  challengeBlock(challenger) {
    this.requirePhase("AwaitingBlockChallenge");
    if (!this.responders.has(challenger)) {
      throw new GameError("You are not being asked to respond.");
    }

    const claim = this.pending.blockClaim;
    this.responders.clear();
    this.raise({
      kind: "ChallengeDeclared",
      player: challenger,
      target: this.pending.blockerIndex,
      role: claim,
    });

    // A proven block defeats the action. A bluffed block lets it through.
    // The fee was already spent and is not refunded.
    this.resolveChallenge(this.pending.blockerIndex, challenger, claim, "fail", "resolve", false);
  }

  exchangePool() {
    if (this.phase !== "AwaitingExchangeSelection" || !this.pending) return [];
    return [...this.players[this.pending.actorIndex].influence, ...this.exchangeDrawn];
  }

  chooseExchangeKeep(keep) {
    this.requirePhase("AwaitingExchangeSelection");
    if (!Array.isArray(keep) || keep.length !== this.exchangeKeepCount) {
      throw new GameError(`You must keep exactly ${this.exchangeKeepCount} card(s).`);
    }

    const pool = this.exchangePool();
    for (const role of keep) {
      const at = pool.indexOf(role);
      if (at < 0) throw new GameError(`${role} is not available to keep.`);
      pool.splice(at, 1);
    }

    const actor = this.players[this.pending.actorIndex];
    actor.influence = [...keep];
    for (const role of pool) this.deck.returnAndShuffle(role);

    this.exchangeDrawn = [];
    this.raise({ kind: "ExchangeCompleted", player: actor.index });
    this.endTurn();
  }

  chooseInfluenceToLose(playerIndex, role) {
    this.requirePhase("AwaitingInfluenceLoss");
    if (this.pendingLosses.length === 0 || this.pendingLosses[0].player !== playerIndex) {
      throw new GameError("It is not your card to lose.");
    }

    const state = this.players[playerIndex];
    if (!state.hasRole(role)) throw new GameError(`You do not hold ${role}.`);

    this.applyLoss(state, role, this.pendingLosses[0].reason);
    this.pendingLosses.shift();
    this.processLossQueue();
  }

  openChallengeWindow() {
    this.responders.clear();
    for (const player of this.players) {
      if (player.isAlive && player.index !== this.pending.actorIndex) this.responders.add(player.index);
    }
    if (this.responders.size === 0) {
      this.afterChallengeWindow();
      return;
    }
    this.phase = "AwaitingChallenge";
  }

  afterChallengeWindow() {
    if (isBlockable(this.pending.kind)) this.openBlockWindow();
    else this.resolveAction();
  }

  openBlockWindow() {
    this.responders.clear();
    if (onlyTargetMayBlock(this.pending.kind)) {
      const target = this.pending.targetIndex;
      if (target >= 0 && this.players[target].isAlive) this.responders.add(target);
    } else {
      for (const player of this.players) {
        if (player.isAlive && player.index !== this.pending.actorIndex) this.responders.add(player.index);
      }
    }
    if (this.responders.size === 0) {
      this.resolveAction();
      return;
    }
    this.phase = "AwaitingBlock";
  }

  openBlockChallengeWindow() {
    this.responders.clear();
    for (const player of this.players) {
      if (player.isAlive && player.index !== this.pending.blockerIndex) this.responders.add(player.index);
    }
    if (this.responders.size === 0) {
      this.failAction();
      return;
    }
    this.phase = "AwaitingBlockChallenge";
  }

  resolveChallenge(claimant, challenger, claim, onProven, onBluffed, refundOnBluff) {
    const holder = this.players[claimant];
    if (holder.hasRole(claim)) {
      this.raise({
        kind: "ChallengeWon",
        player: claimant,
        target: challenger,
        role: claim,
      });
      holder.take(claim);
      this.deck.returnAndShuffle(claim);
      holder.gain(this.deck.draw());
      this.raise({ kind: "CardReplaced", player: claimant, role: claim });
      this.queueLoss(challenger, "LostChallenge");
      this.postLoss = onProven;
    } else {
      this.raise({
        kind: "ChallengeLost",
        player: claimant,
        target: challenger,
        role: claim,
      });
      const cost = coinCost(this.pending.kind);
      if (refundOnBluff && cost > 0) this.adjustCoins(claimant, cost);
      this.queueLoss(claimant, "FailedChallenge");
      this.postLoss = onBluffed;
    }
    this.processLossQueue();
  }

  resolveAction() {
    const actor = this.players[this.pending.actorIndex];
    if (!actor.isAlive) {
      this.failAction();
      return;
    }

    switch (this.pending.kind) {
      case "Income":
        this.adjustCoins(actor.index, INCOME_AMOUNT);
        break;
      case "ForeignAid":
        this.adjustCoins(actor.index, FOREIGN_AID_AMOUNT);
        break;
      case "Tax":
        this.adjustCoins(actor.index, TAX_AMOUNT);
        break;
      case "Steal": {
        const target = this.players[this.pending.targetIndex];
        const amount = Math.min(STEAL_AMOUNT, target.coins);
        if (target.isAlive && amount > 0) {
          this.adjustCoins(target.index, -amount);
          this.adjustCoins(actor.index, amount);
        }
        break;
      }
      case "Assassinate":
      case "Coup": {
        const target = this.players[this.pending.targetIndex];
        if (target.isAlive) {
          this.queueLoss(target.index, this.pending.kind === "Coup" ? "Couped" : "Assassinated");
        }
        this.raiseResolved();
        this.postLoss = "end";
        this.processLossQueue();
        return;
      }
      case "Exchange":
        this.raiseResolved();
        this.beginExchange(actor);
        return;
      default:
        break;
    }

    this.raiseResolved();
    this.endTurn();
  }

  raiseResolved() {
    this.raise({
      kind: "ActionResolved",
      player: this.pending.actorIndex,
      target: this.pending.targetIndex >= 0 ? this.pending.targetIndex : null,
      action: this.pending.kind,
    });
  }

  failAction() {
    this.raise({
      kind: "ActionFailed",
      player: this.pending.actorIndex,
      target: this.pending.targetIndex >= 0 ? this.pending.targetIndex : null,
      action: this.pending.kind,
    });
    this.endTurn();
  }

  beginExchange(actor) {
    this.exchangeDrawn = [];
    for (let i = 0; i < EXCHANGE_DRAW_COUNT; i += 1) this.exchangeDrawn.push(this.deck.draw());
    this.exchangeKeepCount = actor.influence.length;
    this.phase = "AwaitingExchangeSelection";
    this.raise({
      kind: "ExchangeDrawn",
      player: actor.index,
      amount: EXCHANGE_DRAW_COUNT,
    });
  }

  queueLoss(playerIndex, reason) {
    this.pendingLosses.push({ player: playerIndex, reason });
  }

  processLossQueue() {
    while (this.pendingLosses.length > 0) {
      const next = this.pendingLosses[0];
      const player = this.players[next.player];
      if (!player.isAlive) {
        this.pendingLosses.shift();
        continue;
      }
      if (player.influence.length === 1) {
        this.applyLoss(player, player.influence[0], next.reason);
        this.pendingLosses.shift();
        continue;
      }
      this.phase = "AwaitingInfluenceLoss";
      return;
    }
    this.runPostLoss();
  }

  applyLoss(player, role, reason) {
    player.reveal(role);
    this.raise({
      kind: "InfluenceLost",
      player: player.index,
      role,
      reason,
    });

    if (!player.isAlive) {
      if (player.coins !== 0) this.adjustCoins(player.index, -player.coins);
      this.raise({ kind: "PlayerEliminated", player: player.index });
    }
  }

  runPostLoss() {
    const step = this.postLoss;
    this.postLoss = "none";
    if (this.checkGameOver()) return;
    switch (step) {
      case "proceed":
        this.afterChallengeWindow();
        break;
      case "resolve":
        this.resolveAction();
        break;
      case "fail":
        this.failAction();
        break;
      case "end":
        this.endTurn();
        break;
      default:
        this.endTurn();
        break;
    }
  }

  endTurn() {
    if (this.checkGameOver()) return;
    this.pending = null;
    this.responders.clear();

    let next = this.currentPlayerIndex;
    do {
      next = (next + 1) % this.players.length;
    } while (!this.players[next].isAlive);

    this.currentPlayerIndex = next;
    this.turnNumber += 1;
    this.phase = "AwaitingAction";
    this.raise({ kind: "TurnStarted", player: next });
  }

  checkGameOver() {
    const alive = this.players.filter((player) => player.isAlive);
    if (alive.length > 1) return false;
    this.phase = "GameOver";
    this.responders.clear();
    this.pendingLosses.length = 0;
    this.raise({ kind: "GameOver", player: alive[0]?.index ?? null });
    return true;
  }

  adjustCoins(playerIndex, delta) {
    this.players[playerIndex].coins += delta;
    this.raise({ kind: "CoinsChanged", player: playerIndex, amount: delta });
  }

  requirePhase(expected) {
    if (this.phase !== expected) {
      throw new GameError(`Expected ${expected} but the game is in ${this.phase}.`);
    }
  }

  raise(partial) {
    const event = {
      seq: this.events.length + 1,
      kind: partial.kind,
      player: partial.player ?? null,
      target: partial.target ?? null,
      action: partial.action ?? null,
      role: partial.role ?? null,
      amount: partial.amount ?? 0,
      reason: partial.reason ?? null,
    };
    event.text = describe(this, event);
    this.events.push(event);
  }
}

class Player {
  constructor(index, name, coins) {
    this.index = index;
    this.name = name;
    this.coins = coins;
    this.influence = [];
    this.revealed = [];
  }

  get isAlive() {
    return this.influence.length > 0;
  }

  hasRole(role) {
    return this.influence.includes(role);
  }

  gain(role) {
    this.influence.push(role);
  }

  reveal(role) {
    if (!this.take(role)) return false;
    this.revealed.push(role);
    return true;
  }

  take(role) {
    const at = this.influence.indexOf(role);
    if (at < 0) return false;
    this.influence.splice(at, 1);
    return true;
  }
}

function pname(game, index) {
  if (index == null || index < 0 || !game.players[index]) return "Someone";
  return game.players[index].name;
}

function describe(game, event) {
  const actor = pname(game, event.player);
  const other = pname(game, event.target);
  switch (event.kind) {
    case "GameStarted":
      return "The court is seated.";
    case "TurnStarted":
      return `${actor}'s turn.`;
    case "ActionDeclared":
      return describeAction(actor, other, event.action);
    case "ChallengeDeclared":
      return `${actor} challenges ${other}'s ${event.role}.`;
    case "ChallengeWon":
      return `${actor} shows ${event.role}. ${other} loses the challenge.`;
    case "ChallengeLost":
      return `${actor} does not have ${event.role}.`;
    case "CardReplaced":
      return `${actor} returns ${event.role} to the deck and draws a replacement.`;
    case "BlockDeclared":
      return `${actor} blocks with ${event.role}.`;
    case "ActionResolved":
      return `${actor}'s ${actionLabel(event.action)} succeeds.`;
    case "ActionFailed":
      return `${actor}'s ${actionLabel(event.action)} fails.`;
    case "CoinsChanged": {
      const n = Math.abs(event.amount);
      const word = n === 1 ? "coin" : "coins";
      if (event.amount > 0) return `${actor} gains ${n} ${word}.`;
      if (event.amount < 0) return `${actor} loses ${n} ${word}.`;
      return `${actor}'s coins are unchanged.`;
    }
    case "InfluenceLost":
      return `${actor} loses a ${event.role} (${lossPhrase(event.reason)}).`;
    case "PlayerEliminated":
      return `${actor} is out of the game.`;
    case "ExchangeDrawn":
      return `${actor} draws ${event.amount} cards.`;
    case "ExchangeCompleted":
      return `${actor} finishes the exchange.`;
    case "GameOver":
      return event.player == null ? "The game is over." : `${actor} wins.`;
    default:
      return event.kind;
  }
}

function describeAction(actor, target, action) {
  switch (action) {
    case "Income":
      return `${actor} takes Income.`;
    case "ForeignAid":
      return `${actor} takes Foreign Aid.`;
    case "Coup":
      return `${actor} coups ${target}.`;
    case "Tax":
      return `${actor} claims Duke and collects Tax.`;
    case "Steal":
      return `${actor} claims Captain and steals from ${target}.`;
    case "Assassinate":
      return `${actor} claims Assassin and assassinates ${target}.`;
    case "Exchange":
      return `${actor} claims Ambassador and exchanges cards.`;
    default:
      return `${actor} declares ${actionLabel(action)}.`;
  }
}

function lossPhrase(reason) {
  switch (reason) {
    case "FailedChallenge":
      return "bluff called";
    case "LostChallenge":
      return "wrong challenge";
    case "Assassinated":
      return "assassinated";
    case "Couped":
      return "couped";
    default:
      return "lost influence";
  }
}
