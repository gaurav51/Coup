import { requireValue } from "./errors.js";

export const SUPPORT = Object.freeze({ publisher: "Fiesta games", email: "gaurav5130sharma@gmail.com" });

// Explicit allowlists: adding a database field must never silently expose a credential.
export async function exportAccount(platform, id) {
  const own = (collection, projection) => platform.store.collection(collection)
    .find({ accountId: id }, { projection }).limit(10001).toArray();
  const [profile, identities, purchases, ledger, matches] = await Promise.all([
    platform.accounts.profile(id),
    own("identities", { _id: 0, provider: 1, createdAt: 1 }),
    own("receipts", { _id: 0, provider: 1, productId: 1, currency: 1, amount: 1, quantity: 1, createdAt: 1, refundedAt: 1 }),
    own("ledger", { _id: 0, currency: 1, delta: 1, reason: 1, createdAt: 1 }),
    platform.store.collection("matches").find({ accountIds: id }, {
      projection: { _id: 1, mode: 1, winner: 1, players: 1, turns: 1, endedAt: 1 }
    }).limit(10001).toArray(),
  ]);
  requireValue([identities, purchases, ledger, matches].every(rows => rows.length <= 10000), 413,
    "Your export needs support assistance. Contact " + SUPPORT.email + ".");
  return { exportedAt: new Date(), profile, linkedProviders: identities, purchases, ledger,
    matches: matches.map(m => ({ id: m._id, mode: m.mode, won: m.winner === id,
      seat: m.players?.find(p => p.accountId === id)?.seat, turns: m.turns, endedAt: m.endedAt })) };
}

export function deletionPage(req, res) {
  if (req.method !== "GET" || new URL(req.url, "http://localhost").pathname !== "/support/account-deletion") return false;
  res.writeHead(200, { "Content-Type": "text/html; charset=utf-8", "Cache-Control": "no-store",
    "Content-Security-Policy": "default-src 'none'; base-uri 'none'; form-action 'none'", "X-Content-Type-Options": "nosniff", "Referrer-Policy": "no-referrer" });
  res.end(`<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Account deletion — ${SUPPORT.publisher}</title><h1>Request account deletion</h1><p>Publisher: ${SUPPORT.publisher}</p><p>In the game, open Settings, Account and privacy, Delete account. Leave your active table first.</p><p>If you cannot access the app, email <a href="mailto:${SUPPORT.email}?subject=Account%20deletion">${SUPPORT.email}</a> with the subject Account deletion and your player ID if available. We will verify account ownership before deleting data. Never send a password, sign-in token or full payment details.</p><p>Deletion removes your profile, friends and sign-in sessions, and anonymizes saved match-history entries. Store transaction records are retained for reconciliation and disputes; temporary room snapshots and backups may retain data until they expire. Contact us for a data-access request, retention details or other privacy questions.</p><p>This page provides deletion instructions. It is not a privacy policy.</p></html>`);
  return true;
}
