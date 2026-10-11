/** Static rules for standard Coup. Costs, claims, and block rights live here only. */

export const ROLES = Object.freeze([
  "Duke",
  "Captain",
  "Assassin",
  "Contessa",
  "Ambassador",
]);

export const ACTIONS = Object.freeze([
  "Income",
  "ForeignAid",
  "Coup",
  "Tax",
  "Steal",
  "Assassinate",
  "Exchange",
]);

export const STARTING_COINS = 2;
export const TWO_PLAYER_STARTING_COINS = 1;
export const STARTING_INFLUENCE = 2;
export const COPIES_PER_ROLE = 3;
export const FORCED_COUP_THRESHOLD = 10;
export const COUP_COST = 7;
export const ASSASSINATE_COST = 3;
export const STEAL_AMOUNT = 2;
export const TAX_AMOUNT = 3;
export const INCOME_AMOUNT = 1;
export const FOREIGN_AID_AMOUNT = 2;
export const EXCHANGE_DRAW_COUNT = 2;
export const MIN_PLAYERS = 2;
export const MAX_PLAYERS = 6;

const BLOCKS = Object.freeze({
  ForeignAid: Object.freeze(["Duke"]),
  Assassinate: Object.freeze(["Contessa"]),
  Steal: Object.freeze(["Captain", "Ambassador"]),
});

const ACTION_ALIASES = new Map([
  ["income", "Income"],
  ["foreignaid", "ForeignAid"],
  ["foreign aid", "ForeignAid"],
  ["foreign_aid", "ForeignAid"],
  ["coup", "Coup"],
  ["tax", "Tax"],
  ["steal", "Steal"],
  ["assassinate", "Assassinate"],
  ["exchange", "Exchange"],
]);

export function parseAction(value) {
  if (typeof value !== "string") return null;
  return ACTION_ALIASES.get(value.trim().toLowerCase()) ?? null;
}

export function parseRole(value) {
  if (typeof value !== "string") return null;
  const wanted = value.trim().toLowerCase();
  return ROLES.find((role) => role.toLowerCase() === wanted) ?? null;
}

export function coinCost(kind) {
  if (kind === "Coup") return COUP_COST;
  if (kind === "Assassinate") return ASSASSINATE_COST;
  return 0;
}

/** The role an action claims, or null when the action claims nothing. */
export function claimedRole(kind) {
  switch (kind) {
    case "Tax":
      return "Duke";
    case "Steal":
      return "Captain";
    case "Assassinate":
      return "Assassin";
    case "Exchange":
      return "Ambassador";
    default:
      return null;
  }
}

export function isChallengeable(kind) {
  return claimedRole(kind) != null;
}

export function requiresTarget(kind) {
  return kind === "Coup" || kind === "Steal" || kind === "Assassinate";
}

export function blockingRoles(kind) {
  return BLOCKS[kind] ? [...BLOCKS[kind]] : [];
}

export function isBlockable(kind) {
  return blockingRoles(kind).length > 0;
}

/**
 * Foreign Aid hits the whole table, so anyone else may block it.
 * Steal and Assassinate may only be blocked by the target.
 */
export function onlyTargetMayBlock(kind) {
  return kind === "Assassinate" || kind === "Steal";
}

export function canBlockWith(kind, role) {
  return blockingRoles(kind).includes(role);
}

export function actionLabel(kind) {
  return kind === "ForeignAid" ? "Foreign Aid" : kind;
}
