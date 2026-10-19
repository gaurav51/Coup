import { GameError } from "./errors.js";
import { COPIES_PER_ROLE, ROLES } from "./rules.js";
import { randomInt } from "node:crypto";

/** Seeded draw pile. The same seed always deals the same court. */
export function makeRng(seed) {
  let state = seed >>> 0;
  if (state === 0) state = 0x6d2b79f5;
  const next = () => {
    state = (Math.imul(state, 1664525) + 1013904223) >>> 0;
    return state / 4294967296;
  };
  next.getState = () => state;
  return next;
}

export class Deck {
  constructor(seed) {
    this.rng = Number.isInteger(seed) ? makeRng(seed) : null;
    this.cards = [];
    for (const role of ROLES) {
      for (let copy = 0; copy < COPIES_PER_ROLE; copy += 1) this.cards.push(role);
    }
    this.shuffle();
  }

  get count() {
    return this.cards.length;
  }

  countOf(role) {
    let n = 0;
    for (const card of this.cards) if (card === role) n += 1;
    return n;
  }

  shuffle() {
    for (let i = this.cards.length - 1; i > 0; i -= 1) {
      const j = this.rng ? Math.floor(this.rng() * (i + 1)) : randomInt(i + 1);
      [this.cards[i], this.cards[j]] = [this.cards[j], this.cards[i]];
    }
  }

  draw() {
    if (this.cards.length === 0) throw new GameError("Draw from an empty deck.");
    return this.cards.pop();
  }

  /** A proven claim goes back into the deck and the deck is shuffled. */
  returnAndShuffle(role) {
    this.cards.push(role);
    this.shuffle();
  }

  snapshot() {
    return { cards: [...this.cards], rngState: this.rng?.getState() ?? null };
  }

  static restore(saved) {
    const deck = Object.create(Deck.prototype);
    deck.cards = [...saved.cards];
    deck.rng = saved.rngState == null ? null : makeRng(saved.rngState);
    return deck;
  }
}
