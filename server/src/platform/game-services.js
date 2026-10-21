import { randomUUID } from "node:crypto";
import { requireValue, ApiError } from "./errors.js";
import { publicProfile } from "./auth.js";

const day = () => new Date().toISOString().slice(0, 10);
export const MISSIONS = Object.freeze([
  { id: "play-one", title: "Play a public match", stat: "games", target: 1, coins: 50 },
  { id: "win-one", title: "Win a public match", stat: "wins", target: 1, coins: 100 },
]);

export class GameServices {
  constructor(store, { products = [], verifyPurchase, finishPurchase } = {}) {
    this.store = store;
    this.products = new Map(products.map((p) => [p.id, p]));
    this.verifyPurchase = verifyPurchase;
    this.finishPurchase = finishPurchase;
    for (const p of products) requireValue(p.id && ["coins", "gems"].includes(p.currency) && Number.isSafeInteger(p.amount) && p.amount > 0, 500, "Invalid product configuration.");
  }

  catalog() { return { products: [...this.products.values()].map(({ id, title, currency, amount, apple, google }) => ({ id, title, currency, amount, apple, google })), collection: ["Duke", "Captain", "Assassin", "Contessa", "Ambassador"] }; }

  async wallet(accountId) {
    return this.store.collection("ledger").find({ accountId }, { projection: { _id: 0, delta: 1, currency: 1, reason: 1, createdAt: 1 } }).sort({ createdAt: -1 }).limit(50).toArray();
  }

  async missions(accountId) {
    const a = await this.store.collection("accounts").findOne({ _id: accountId });
    const claims = await this.store.collection("ledger").find({ accountId, day: day(), type: "mission" }).toArray();
    return { day: day(), missions: MISSIONS.map((m) => ({ ...m, progress: a?.daily?.day === day() ? a.daily[m.stat] : 0, claimed: claims.some((c) => c.mission === m.id) })) };
  }

  async claimMission(accountId, missionId) {
    const m = MISSIONS.find((item) => item.id === missionId);
    requireValue(m, 404, "Mission not found.");
    const currentDay = day();
    return this.store.transaction(async (session) => {
      const accounts = this.store.collection("accounts");
      const a = await accounts.findOne({ _id: accountId }, { session });
      requireValue(a?.daily?.day === currentDay && a.daily[m.stat] >= m.target, 409, "Mission is not complete.");
      const id = `mission:${accountId}:${currentDay}:${m.id}`;
      if (!(await this.store.collection("ledger").findOne({ _id: id }, { session }))) {
        await this.store.collection("ledger").insertOne({ _id: id, accountId, day: currentDay, type: "mission", mission: m.id, delta: m.coins, currency: "coins", reason: m.title, createdAt: new Date() }, { session });
        await accounts.updateOne({ _id: accountId }, { $inc: { coins: m.coins } }, { session });
      }
      return publicProfile(await accounts.findOne({ _id: accountId }, { session }));
    });
  }

  async history(accountId) {
    return this.store.collection("matches").find({ accountIds: accountId }, { projection: { _id: 1, mode: 1, players: 1, winner: 1, endedAt: 1, turns: 1 } }).sort({ endedAt: -1 }).limit(30).toArray();
  }

  async leaderboard() {
    return this.store.collection("accounts").find({ rankedGames: { $gt: 0 } }, { projection: { _id: 1, name: 1, rating: 1, rankedGames: 1 } }).sort({ rating: -1, _id: 1 }).limit(100).toArray();
  }

  /** Called only by the authoritative room commit, never by a client result endpoint. */
  async completeMatch(room, session) {
    if (room.game?.phase !== "GameOver" || !room.matchId) return;
    const matches = this.store.collection("matches");
    if (await matches.findOne({ _id: room.matchId }, { session })) return;
    const humans = room.players.filter((p) => !p.bot && p.accountId);
    const eligible = room.mode !== "private" && humans.length >= 2 && humans.length === room.players.length && humans.every(p => p.moves > 0);
    const winner = room.players[room.game.winnerIndex]?.accountId ?? null;
    const players = room.players.map((p) => ({ accountId: p.accountId ?? null, name: p.name, bot: p.bot, seat: p.seat }));
    await matches.insertOne({ _id: room.matchId, accountIds: humans.map((p) => p.accountId), players, winner, mode: room.mode, eligible, turns: room.game.turnNumber, endedAt: new Date() }, { session });
    if (!eligible) return;
    const accounts = this.store.collection("accounts");
    const rows = await accounts.find({ _id: { $in: humans.map((p) => p.accountId) } }, { session }).toArray();
    const ratings = new Map(rows.map((a) => [a._id, a.rating]));
    for (const p of humans) {
      const account = rows.find((a) => a._id === p.accountId);
      if (!account) continue;
      const won = p.accountId === winner;
      const daily = account.daily?.day === day() ? { ...account.daily } : { day: day(), games: 0, wins: 0 };
      daily.games++; if (won) daily.wins++;
      // Multiplayer pairwise Elo: winner beats each opponent, all other pairs tie.
      let delta = 0;
      if (room.mode === "ranked") for (const other of humans) {
        if (other.accountId === p.accountId) continue;
        const actual = won ? 1 : other.accountId === winner ? 0 : .5;
        const expected = 1 / (1 + 10 ** ((ratings.get(other.accountId) - account.rating) / 400));
        delta += (32 / (humans.length - 1)) * (actual - expected);
      }
      await accounts.updateOne({ _id: p.accountId }, { $set: { daily }, $inc: { games: 1, wins: won ? 1 : 0, xp: won ? 100 : 40, rating: Math.round(delta), rankedGames: room.mode === "ranked" ? 1 : 0 } }, { session });
    }
  }

  async purchase(accountId, input) {
    const product = this.products.get(input.productId);
    requireValue(product && (input.provider === "apple" || input.provider === "google"), 400, "Unknown product or store.");
    requireValue(this.verifyPurchase, 503, "Purchases are not configured.");
    const verified = await this.verifyPurchase(accountId, product, input);
    requireValue(verified.transaction && verified.accountId === accountId, 403, "Purchase account mismatch.");
    const quantity = verified.quantity ?? 1;
    requireValue(Number.isSafeInteger(quantity) && quantity > 0 && quantity <= 100, 400, "Invalid purchase quantity.");
    const receiptId = `${input.provider}:${verified.transaction}`;
    const result = await this.store.transaction(async (session) => {
      requireValue(await this.store.collection("accounts").findOne({ _id: accountId }, { session }), 401, "Account no longer exists.");
      const existing = await this.store.collection("receipts").findOne({ _id: receiptId }, { session });
      if (existing) {
        requireValue(existing.accountId === accountId && existing.productId === product.id, 403, "Purchase already belongs to another account or product.");
        requireValue(!existing.refundedAt, 409, "Purchase was refunded.");
      } else {
        await this.store.collection("receipts").insertOne({ _id: receiptId, provider: input.provider, transaction: verified.transaction, accountId, productId: product.id, currency: product.currency, amount: product.amount * quantity, quantity, createdAt: new Date(), pendingFinish: true, proof: verified.finishProof ?? null }, { session });
        await this.store.collection("accounts").updateOne({ _id: accountId }, { $inc: { [product.currency]: product.amount * quantity } }, { session });
        await this.store.collection("ledger").insertOne({ _id: receiptId, accountId, currency: product.currency, delta: product.amount * quantity, reason: `purchase:${product.id}`, createdAt: new Date() }, { session });
      }
      return { receiptId, profile: publicProfile(await this.store.collection("accounts").findOne({ _id: accountId }, { session })) };
    });
    await this.finishPending(receiptId).catch(() => {}); // Durable outbox is retried on the server.
    return result;
  }

  async finishPending(id) {
    const row = await this.store.collection("receipts").findOne({ _id: id, pendingFinish: true });
    if (!row) return;
    if (this.finishPurchase) await this.finishPurchase(row);
    await this.store.collection("receipts").updateOne({ _id: id }, { $set: { pendingFinish: false }, $unset: { proof: "" } });
  }

  async refund(provider, transaction) {
    return this.store.transaction(async (session) => {
      const receipt = await this.store.collection("receipts").findOne({ _id: `${provider}:${transaction}` }, { session });
      if (!receipt || receipt.refundedAt) return;
      await this.store.collection("receipts").updateOne({ _id: receipt._id }, { $set: { refundedAt: new Date() } }, { session });
      // Negative balances are retained as debt rather than enabling refund farming.
      await this.store.collection("accounts").updateOne({ _id: receipt.accountId }, { $inc: { [receipt.currency]: -receipt.amount } }, { session });
      await this.store.collection("ledger").insertOne({ _id: `refund:${receipt._id}`, accountId: receipt.accountId, currency: receipt.currency, delta: -receipt.amount, reason: "store-refund", createdAt: new Date() }, { session });
    });
  }
}
