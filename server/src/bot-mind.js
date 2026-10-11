import { makeRng } from "./deck.js";
import {
  ASSASSINATE_COST,
  COPIES_PER_ROLE,
  COUP_COST,
  FORCED_COUP_THRESHOLD,
  ROLES,
  STEAL_AMOUNT,
  blockingRoles,
  canBlockWith,
} from "./rules.js";

/** Same spread as the Unity table: cautious, opportunist, ruthless. */
export const PERSONALITIES = Object.freeze([
  Object.freeze({ aggression: 0.3, bluffChance: 0.12, suspicion: 0.35, blockBluffChance: 0.15 }),
  Object.freeze({ aggression: 0.55, bluffChance: 0.35, suspicion: 0.55, blockBluffChance: 0.3 }),
  Object.freeze({ aggression: 0.85, bluffChance: 0.6, suspicion: 0.75, blockBluffChance: 0.45 }),
]);

const ROLE_VALUE = {
  Duke: 5,
  Captain: 4,
  Contessa: 3,
  Assassin: 3,
  Ambassador: 2,
};

const PATIENCE_TURNS = 40;

/**
 * One legal command for this bot, from its own hand and the public table.
 * A Duke taxes, a Captain steals, a Contessa blocks, and a counted-out claim is challenged.
 */
export function decide(game, player) {
  const personality = player.personality ?? PERSONALITIES[1];
  const random = player.rng ?? (player.rng = makeRng((player.seat + 1) * 7919));
  const view = viewFor(game, player.seat);

  switch (game.phase) {
    case "AwaitingAction":
      return chooseAction(view, personality, random);
    case "AwaitingChallenge":
    case "AwaitingBlockChallenge":
      return shouldChallenge(view, personality) ? { type: "challenge" } : { type: "pass" };
    case "AwaitingBlock": {
      const block = chooseBlock(view, personality, random);
      if (block && canBlockWith(view.action, block)) return { type: "block", role: block };
      return { type: "pass" };
    }
    case "AwaitingInfluenceLoss":
      return { type: "lose", role: chooseLoss(view, random) };
    case "AwaitingExchangeSelection":
      return {
        type: "exchange",
        keep: chooseExchange(game.exchangePool(), game.exchangeKeepCount),
      };
    default:
      return null;
  }
}

function chooseAction(view, personality, random) {
  const coins = view.coins;
  const hand = view.hand;

  if (coins >= FORCED_COUP_THRESHOLD) return declare("Coup", pickTarget(view));

  const impatience = Math.min(1, view.turn / PATIENCE_TURNS);
  const aggression = personality.aggression + impatience * (1 - personality.aggression);
  const hoarding = impatience > 0.5;

  if (coins >= COUP_COST && random() < aggression) return declare("Coup", pickTarget(view));

  if (hand.includes("Assassin") && coins >= ASSASSINATE_COST) {
    const victim = pickAssassination(view);
    if (victim >= 0) return declare("Assassinate", victim);
  }

  if (hand.includes("Duke")) return declare("Tax");

  if (hand.includes("Captain") && !hoarding) {
    const rich = richest(view);
    if (rich >= 0 && view.players[rich].coins > 0) return declare("Steal", rich);
  }

  if (hand.includes("Ambassador") && hand.every((role) => ROLE_VALUE[role] <= 2) && !hoarding) {
    return declare("Exchange");
  }

  if (!hoarding && random() < personality.bluffChance) {
    const bluff = pickBluff(view, personality, random);
    if (bluff) return bluff;
  }

  if (random() < 0.55 && opponents(view).length <= 2) return declare("ForeignAid");
  return declare("Income");
}

function pickBluff(view, personality, random) {
  const options = [];
  if (!view.hand.includes("Duke")) options.push(declare("Tax"));

  if (!view.hand.includes("Captain")) {
    const rich = richest(view);
    if (rich >= 0 && view.players[rich].coins >= STEAL_AMOUNT) options.push(declare("Steal", rich));
  }

  if (!view.hand.includes("Assassin") && view.coins >= ASSASSINATE_COST && personality.aggression > 0.6) {
    const victim = pickAssassination(view);
    if (victim >= 0) options.push(declare("Assassinate", victim));
  }

  if (options.length === 0) return null;
  return options[Math.floor(random() * options.length)];
}

function shouldChallenge(view, personality) {
  const blocking = view.phase === "AwaitingBlockChallenge";
  const claimant = blocking ? view.blocker : view.actor;
  const claim = blocking ? view.blockClaim : view.claim;
  if (!claim || claimant == null || claimant === view.self) return false;

  const chance = probabilityHolds(view, claimant, claim);
  if (chance <= 0) return true;

  let threshold = 0.4 + (personality.suspicion - 0.5) * 0.35;
  if (
    !blocking &&
    view.action === "Assassinate" &&
    view.target === view.self &&
    view.hand.length === 1 &&
    !view.hand.includes("Contessa")
  ) {
    threshold += 0.35;
  }
  if (view.hand.length === 1) threshold -= 0.15;
  return chance < threshold;
}

function chooseBlock(view, personality, random) {
  const roles = blockingRoles(view.action);
  if (roles.length === 0) return null;

  const held = roles.filter((role) => view.hand.includes(role));
  held.sort((a, b) => ROLE_VALUE[b] - ROLE_VALUE[a]);
  if (held.length > 0) return held[0];

  if (view.action === "Assassinate" && view.target === view.self && view.hand.length === 1) {
    return "Contessa";
  }
  if (random() < personality.blockBluffChance) return roles[Math.floor(random() * roles.length)];
  return null;
}

function chooseLoss(view, random) {
  const ranked = view.hand.map((role) => ({ role, roll: random() }));
  ranked.sort((a, b) => ROLE_VALUE[a.role] - ROLE_VALUE[b.role] || a.roll - b.roll);
  return ranked[0].role;
}

function chooseExchange(pool, keep) {
  const ordered = pool.map((role, index) => ({ role, index }));
  ordered.sort((a, b) => ROLE_VALUE[b.role] - ROLE_VALUE[a.role]);
  const picked = [];
  const used = new Set();

  const otherRolesLeft = () => {
    const have = new Set(picked.map((card) => card.role));
    return ordered.filter((card) => !used.has(card.index) && !have.has(card.role)).length;
  };

  for (const card of ordered) {
    if (picked.length >= keep) break;
    if (picked.some((kept) => kept.role === card.role) && otherRolesLeft() > keep - picked.length) continue;
    picked.push(card);
    used.add(card.index);
  }
  for (const card of ordered) {
    if (picked.length >= keep) break;
    if (used.has(card.index)) continue;
    picked.push(card);
    used.add(card.index);
  }
  return picked.slice(0, keep).map((card) => card.role);
}

function declare(action, target = -1) {
  return target >= 0 ? { type: "declare", action, target } : { type: "declare", action };
}

function viewFor(game, seat) {
  const self = game.players[seat];
  const players = game.players.map((player) => ({
    index: player.index,
    coins: player.coins,
    influence: player.influence.length,
    revealed: [...player.revealed],
    alive: player.isAlive,
  }));
  const known = [...self.influence];
  for (const player of game.players) known.push(...player.revealed);
  return {
    self: seat,
    hand: [...self.influence],
    coins: self.coins,
    players,
    known,
    phase: game.phase,
    turn: game.turnNumber,
    actor: game.actingPlayerIndex,
    action: game.pendingAction,
    target: game.pendingTarget,
    claim: game.pendingClaim,
    blocker: game.pendingBlocker,
    blockClaim: game.pendingBlockClaim,
  };
}

function opponents(view) {
  return view.players.filter((player) => player.index !== view.self && player.alive);
}

function pickTarget(view) {
  const best = opponents(view).sort((a, b) => b.influence - a.influence || b.coins - a.coins)[0];
  return best ? best.index : -1;
}

function pickAssassination(view) {
  const finishable = opponents(view)
    .filter((player) => player.influence === 1)
    .sort((a, b) => b.coins - a.coins)[0];
  return finishable ? finishable.index : pickTarget(view);
}

function richest(view) {
  const best = opponents(view).sort((a, b) => b.coins - a.coins)[0];
  return best ? best.index : -1;
}

function probabilityHolds(view, playerIndex, role) {
  if (playerIndex === view.self) return view.hand.includes(role) ? 1 : 0;
  const hand = view.players[playerIndex]?.influence ?? 0;
  if (hand <= 0) return 0;
  const unseen = COPIES_PER_ROLE * ROLES.length - view.known.length;
  const copies = COPIES_PER_ROLE - view.known.filter((card) => card === role).length;
  if (copies <= 0 || unseen <= 0) return 0;
  if (unseen - copies < hand) return 1;
  let none = 1;
  for (let i = 0; i < hand; i += 1) none *= (unseen - copies - i) / (unseen - i);
  return 1 - none;
}
