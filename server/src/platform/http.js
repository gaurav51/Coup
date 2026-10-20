import { ApiError, requireValue, displayName } from "./errors.js";
import { RateLimit } from "./rate-limit.js";

export function api(platform) {
  const limits = new RateLimit({ capacity: 60, refillPerSecond: 1 });
  const authLimits = new RateLimit({ capacity: 10, refillPerSecond: .1 });
  return async (req, res) => {
    const url = new URL(req.url, "http://localhost");
    if (!url.pathname.startsWith("/v1/")) return false;
    // Log route metadata only: OAuth codes, bearer headers and bodies never enter logs.
    const started=performance.now();
    const route=url.pathname.replace(/(\/friends\/)[^/]+/, "$1:id").replace(/(\/missions\/)[^/]+/, "$1:id");
    let logged=false;
    const logRequest=aborted=>{
      if(logged)return;logged=true;
      console.log(JSON.stringify({type:"api",method:req.method,route:route.slice(0,160),
        status:aborted?0:res.statusCode,durationMs:Math.round(performance.now()-started),aborted}));
    };
    res.once("finish",()=>logRequest(false));
    res.once("close",()=>logRequest(!res.writableFinished));
    const ip = platform.trustProxy && req.socket.remoteAddress?.match(/^(::1|::ffff:127\.|127\.)/)
      ? String(req.headers["x-forwarded-for"] ?? req.socket.remoteAddress).split(",").at(-1).trim() : req.socket.remoteAddress;
    try {
      requireValue(limits.take(ip), 429, "Too many requests. Try again shortly.");
      const path = url.pathname; const method = req.method;
      if (path.startsWith("/v1/auth/") && path !== "/v1/auth/browser/poll") requireValue(authLimits.take(ip), 429, "Too many sign-in attempts. Try again shortly.");
      if (/^\/v1\/auth\/browser\/callback\/(apple|google)$/.test(path)) {
        let values = Object.fromEntries(url.searchParams);
        if (method === "POST") {
          let raw = "";
          for await (const chunk of req) { raw += chunk.toString("utf8"); requireValue(Buffer.byteLength(raw) <= 32768, 413, "Request too large."); }
          values = Object.fromEntries(new URLSearchParams(raw));
        }
        await platform.browser.callback(path.split("/").at(-1), values);
        res.writeHead(200, { "Content-Type": "text/html; charset=utf-8", "Cache-Control": "no-store", "Content-Security-Policy": "default-src 'none'", "Referrer-Policy": "no-referrer" });
        res.end("<!doctype html><title>Coup</title><h1>Return to Coup</h1><p>Continue in the game to finish signing in.</p>");
        return true;
      }
      const body = method === "GET" || method === "DELETE" ? {} : await jsonBody(req);
      const auth = async () => platform.accounts.authenticate(String(req.headers.authorization ?? "").replace(/^Bearer /i, ""));
      let result;
      if (path === "/v1/auth/browser/start" && method === "POST") result = await platform.browser.start(body.provider, body.link ? (await auth()).accountId : null);
      else if (path === "/v1/auth/browser/poll" && method === "POST") result = await platform.browser.poll(body.state, body.pollToken);
      else if (path === "/v1/auth/guest" && method === "POST") result = await platform.accounts.guest(body.name);
      else if (path === "/v1/auth/refresh" && method === "POST") result = await platform.accounts.refresh(body);
      else if (path === "/v1/auth/nonce" && method === "POST") result = await platform.accounts.nonce(body.provider);
      else if (/^\/v1\/auth\/(apple|google)$/.test(path) && method === "POST") result = await platform.accounts.social(path.split("/").at(-1), body);
      else if (/^\/v1\/auth\/link\/(apple|google)$/.test(path) && method === "POST") result = await platform.accounts.social(path.split("/").at(-1), body, (await auth()).accountId);
      else {
        const user = await auth(); const id = user.accountId;
        if (path === "/v1/auth/logout" && method === "POST") { await platform.store.collection("sessions").deleteOne({ _id: user.sessionId }); platform.accounts.forget(user.sessionId); result = {}; }
        else if (path === "/v1/profile" && method === "GET") result = { profile: await platform.accounts.profile(id) };
        else if (path === "/v1/profile" && method === "PATCH") {
          await platform.store.collection("accounts").updateOne({ _id: id }, { $set: { name: displayName(body.name) } });
          result = { profile: await platform.accounts.profile(id) };
        }
        else if (path === "/v1/session/active" && method === "GET") {
          const active = platform.rooms.activeSeat(id);
          result = { active: active ? { code: active.code, status: active.status, mode: active.mode } : null };
        }
        else if (path === "/v1/content" && method === "GET") result = { season: platform.content?.season ?? null, event: platform.content?.event ?? null };
        else if (path === "/v1/account" && method === "DELETE") {
          requireValue(!platform.rooms.activeSeat(id), 409, "Leave your active table before deleting your account.");
          await platform.store.transaction(async session => {
            for (const collection of ["sessions", "identities", "ledger", "friends"]) {
              await platform.store.collection(collection).deleteMany(collection === "friends" ? { $or: [{ from: id }, { to: id }] } : { accountId: id }, { session });
            }
            await platform.store.collection("accounts").deleteOne({ _id: id }, { session });
            await platform.store.collection("matches").updateMany({ accountIds: id }, {
              $pull: { accountIds: id }, $set: { "players.$[p].name": "Deleted player", "players.$[p].accountId": null }
            }, { session, arrayFilters: [{ "p.accountId": id }] });
            await platform.store.collection("matches").updateMany({ winner: id }, { $set: { winner: null } }, { session });
          });
          platform.accounts.forgetAccount(id);
          result = {};
        }
        else if (path === "/v1/friends" && method === "GET") {
          const links = await platform.store.collection("friends").find({ $or: [{ from: id }, { to: id }] }).limit(100).toArray();
          const ids = links.map(l => l.from === id ? l.to : l.from);
          const peers = await platform.store.collection("accounts").find({ _id: { $in: ids } }, { projection: { _id: 1, name: 1 } }).toArray();
          result = { friends: links.map(l => ({ id: l._id, accountId: l.from === id ? l.to : l.from, name: peers.find(p => p._id === (l.from === id ? l.to : l.from))?.name ?? "Player", accepted: l.accepted, incoming: l.to === id })) };
        }
        else if (path === "/v1/friends" && method === "POST") {
          requireValue(typeof body.accountId === "string" && /^[a-f0-9-]{36}$/i.test(body.accountId) && body.accountId !== id, 400, "Enter another player's full player ID.");
          requireValue(await platform.store.collection("accounts").findOne({ _id: body.accountId }), 404, "Player not found.");
          const key = [id, body.accountId].sort().join(":");
          requireValue(await platform.store.collection("friends").countDocuments({ $or: [{ from: id }, { to: id }] }) < 100, 409, "Your friends list is full.");
          await platform.store.collection("friends").updateOne({ _id: key }, { $setOnInsert: { from: id, to: body.accountId, accepted: false, createdAt: new Date() } }, { upsert: true });
          result = {};
        }
        else if (/^\/v1\/friends\/[^/]+$/.test(path) && (method === "PATCH" || method === "DELETE")) {
          const key = decodeURIComponent(path.split("/")[3]);
          const row = await platform.store.collection("friends").findOne({ _id: key, $or: [{ from: id }, { to: id }] });
          requireValue(row, 404, "Friend request not found.");
          if (method === "DELETE") await platform.store.collection("friends").deleteOne({ _id: key });
          else { requireValue(row.to === id, 403, "Only the recipient can accept."); await platform.store.collection("friends").updateOne({ _id: key }, { $set: { accepted: true } }); }
          result = {};
        }
        else if (path === "/v1/catalog" && method === "GET") result = platform.services.catalog();
        else if (path === "/v1/missions" && method === "GET") result = await platform.services.missions(id);
        else if (/^\/v1\/missions\/[^/]+\/claim$/.test(path) && method === "POST") result = { profile: await platform.services.claimMission(id, path.split("/")[3]) };
        else if (path === "/v1/matches" && method === "GET") result = { matches: await platform.services.history(id) };
        else if (path === "/v1/leaderboard" && method === "GET") result = { players: await platform.services.leaderboard() };
        else if (path === "/v1/wallet/ledger" && method === "GET") result = { ledger: await platform.services.wallet(id) };
        else if (path === "/v1/purchases/verify" && method === "POST") result = await platform.services.purchase(id, body);
        else if (path === "/v1/status" && method === "GET") result = { purchases: platform.services.products.size > 0, apple: !!platform.browser?.configured("apple"), google: !!platform.browser?.configured("google"), worker: platform.rooms.worker, protocol: 2 };
        else throw new ApiError(404, "API not found.");
      }
      send(res, 200, { ok: true, ...result });
    } catch (err) {
      const status = err.status ?? (err.code === 11000 ? 409 : 500);
      if (status === 500) console.error("[api] request failed", err.name); // Never log proofs or tokens.
      send(res, status, { ok: false, error: status === 500 ? "The server could not complete the request." : err.code === 11000 ? "This operation has already been completed. Refresh and try again." : err.message });
    }
    return true;
  };
}

export function send(res, status, value) {
  res.writeHead(status, { "Content-Type": "application/json", "Cache-Control": "no-store", "X-Content-Type-Options": "nosniff" });
  res.end(JSON.stringify(value));
}

async function jsonBody(req) {
  requireValue(String(req.headers["content-type"] ?? "").startsWith("application/json"), 415, "Send application/json.");
  let bytes = 0; const chunks = [];
  for await (const chunk of req) {
    bytes += chunk.length;
    requireValue(bytes <= 32768, 413, "Request too large.");
    chunks.push(chunk);
  }
  try {
    const value = JSON.parse(Buffer.concat(chunks).toString("utf8") || "{}");
    requireValue(value && !Array.isArray(value) && typeof value === "object", 400, "Send a JSON object.");
    return value;
  } catch (err) { if (err instanceof ApiError) throw err; throw new ApiError(400, "Invalid JSON."); }
}
