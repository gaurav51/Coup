import crypto from "node:crypto";
import { SignJWT, jwtVerify, createRemoteJWKSet } from "jose";
import { ApiError, requireValue, displayName } from "./errors.js";

export const hash = (text) => crypto.createHash("sha256").update(String(text)).digest("hex");
const secretToken = () => crypto.randomBytes(32).toString("base64url");
const ACCESS_SECONDS = 900;
const SESSION_MS = 30 * 86400000;
/** How many retired refresh hashes a session remembers for reuse detection. */
const REUSE_HISTORY = 10;

export class Accounts {
  constructor(store, { secret, appleAudience, googleAudiences = [], verifyIdentity } = {}) {
    requireValue(typeof secret === "string" && secret.length >= 32, 500, "AUTH_SECRET must contain at least 32 characters.");
    this.store = store;
    this.key = new TextEncoder().encode(secret);
    this.appleAudience = appleAudience;
    this.googleAudiences = googleAudiences;
    this.verifyIdentity = verifyIdentity ?? this.providerClaims.bind(this);
    this.keys = {
      apple: createRemoteJWKSet(new URL("https://appleid.apple.com/auth/keys")),
      google: createRemoteJWKSet(new URL("https://www.googleapis.com/oauth2/v3/certs")),
    };
  }

  newAccount(name) {
    return { _id: crypto.randomUUID(), name: displayName(name), coins: 0, gems: 0, rating: 1000,
      xp: 0, games: 0, wins: 0, inventory: ["cardback_default"], equipped: "cardback_default",
      daily: { day: "", games: 0, wins: 0 }, createdAt: new Date() };
  }

  async session(accountId, mongoSession) {
    const refreshToken = secretToken();
    const row = { _id: crypto.randomUUID(), accountId, refreshHash: hash(refreshToken), expiresAt: new Date(Date.now() + SESSION_MS) };
    await this.store.collection("sessions").insertOne(row, { session: mongoSession });
    return { ...(await this.tokens(row)), refreshToken };
  }

  async tokens(row) {
    const accessToken = await new SignJWT({ sid: row._id }).setProtectedHeader({ alg: "HS256" })
      .setIssuer("coup").setAudience("coup-client").setSubject(row.accountId).setIssuedAt()
      .setExpirationTime(`${ACCESS_SECONDS}s`).sign(this.key);
    return { accessToken, expiresIn: ACCESS_SECONDS, sessionId: row._id, accountId: row.accountId };
  }

  async guest(name = "Player") {
    const account = this.newAccount(name);
    return this.store.transaction(async (session) => {
      await this.store.collection("accounts").insertOne(account, { session });
      return { ...(await this.session(account._id, session)), profile: publicProfile(account) };
    });
  }

  async authenticate(token) {
    try {
      const { payload } = await jwtVerify(token ?? "", this.key, { algorithms: ["HS256"], issuer: "coup", audience: "coup-client" });
      const row = await this.store.collection("sessions").findOne({ _id: payload.sid, accountId: payload.sub, expiresAt: { $gt: new Date() } });
      requireValue(row, 401, "Session expired. Sign in again.");
      return { accountId: payload.sub, sessionId: payload.sid, expiresAt: payload.exp * 1000 };
    } catch { throw new ApiError(401, "Session expired. Sign in again."); }
  }

  /**
   * Rotates the refresh token, and treats a spent one as a breach.
   *
   * Rotation alone only refuses a replayed token: whoever stole it learns their
   * copy is stale while the real session keeps running. Each rotation records the
   * hash it retired, so a token that is presented after being spent proves two
   * holders exist, and the session is destroyed rather than merely refused.
   *
   * Destroying the row is enough to cut off live access as well: HTTP calls and
   * the socket middleware both re-check the sessions collection, the latter on
   * every packet, so any connected socket drops on its next command.
   *
   * Only this session dies. Other devices hold their own rows and are untouched.
   */
  async refresh({ sessionId, refreshToken }) {
    requireValue(typeof refreshToken === "string" && refreshToken.length <= 256, 401, "Invalid refresh token.");
    const presented = hash(refreshToken);
    const next = secretToken();
    const sessions = this.store.collection("sessions");
    const row = await sessions.findOneAndUpdate(
      { _id: sessionId, refreshHash: presented, expiresAt: { $gt: new Date() } },
      { $set: { refreshHash: hash(next), expiresAt: new Date(Date.now() + SESSION_MS) },
        $push: { spentHashes: { $each: [presented], $slice: -REUSE_HISTORY } } }, { returnDocument: "after" });
    if (row) return { ...(await this.tokens(row)), refreshToken: next };

    const replayed = await sessions.findOneAndDelete({ _id: sessionId, spentHashes: presented });
    requireValue(!replayed, 401, "This session was signed out because its credentials were reused. Sign in again.");
    throw new ApiError(401, "Session expired. Sign in again.");
  }

  async nonce(provider) {
    requireValue(provider === "apple" || provider === "google", 400, "Unsupported identity provider.");
    const rawNonce = secretToken();
    const row = { _id: crypto.randomUUID(), provider, expected: provider === "apple" ? hash(rawNonce) : rawNonce, expiresAt: new Date(Date.now() + 300000) };
    await this.store.collection("nonces").insertOne(row);
    return { challengeId: row._id, rawNonce, providerNonce: row.expected, expiresIn: 300 };
  }

  async providerClaims(provider, token) {
    const audience = provider === "apple" ? this.appleAudience : this.googleAudiences;
    requireValue(audience?.length, 503, `${provider} sign-in is not configured.`);
    const issuer = provider === "apple" ? "https://appleid.apple.com" : ["https://accounts.google.com", "accounts.google.com"];
    try {
      const { payload } = await jwtVerify(token, this.keys[provider], { algorithms: ["RS256"], issuer, audience, requiredClaims: ["exp", "iat", "sub", "nonce"] });
      return payload;
    } catch { throw new ApiError(401, "The identity token could not be verified."); }
  }

  async social(provider, { challengeId, idToken, name = "Player" }, linkAccountId = null) {
    requireValue(provider === "apple" || provider === "google", 400, "Unsupported identity provider.");
    requireValue(typeof idToken === "string" && idToken.length <= 16000, 400, "Invalid identity token.");
    const claims = await this.verifyIdentity(provider, idToken);
    requireValue(typeof claims.sub === "string" && claims.sub.length > 0, 401, "Invalid provider subject.");
    return this.store.transaction(async (session) => {
      const nonce = await this.store.collection("nonces").findOneAndDelete({ _id: challengeId, provider, expected: claims.nonce, expiresAt: { $gt: new Date() } }, { session });
      requireValue(nonce, 401, "Sign-in challenge expired or already used.");
      const identities = this.store.collection("identities");
      const identity = await identities.findOne({ provider, subject: claims.sub }, { session });
      if (identity && linkAccountId && identity.accountId !== linkAccountId) throw new ApiError(409, "This identity is linked to another account.");
      let accountId = identity?.accountId ?? linkAccountId;
      if (!accountId) {
        const account = this.newAccount(name);
        accountId = account._id;
        await this.store.collection("accounts").insertOne(account, { session });
      }
      if (!identity) await identities.insertOne({ provider, subject: claims.sub, accountId }, { session });
      const account = await this.store.collection("accounts").findOne({ _id: accountId }, { session });
      requireValue(account, 404, "Account not found.");
      return { ...(await this.session(accountId, session)), profile: publicProfile(account) };
    });
  }

  async profile(accountId) {
    const account = await this.store.collection("accounts").findOne({ _id: accountId });
    requireValue(account, 404, "Account not found.");
    return publicProfile(account);
  }
}

export function publicProfile(row) {
  return { id: row._id, name: row.name, coins: row.coins, gems: row.gems, rating: row.rating,
    xp: row.xp, level: 1 + Math.floor(row.xp / 500), games: row.games, wins: row.wins,
    inventory: row.inventory, equipped: row.equipped, daily: row.daily };
}
