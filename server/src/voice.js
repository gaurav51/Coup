import crypto from "node:crypto";

/** Channel access is derived from the authenticated socket's seat, never client input. */
export function voiceAccess(store, socket, action, env = process.env) {
  const found = store.findBySocket(socket.id);
  if (!socket.data.accountId || !found || found.player.accountId !== socket.data.accountId || found.player.abandoned)
    throw new Error("Join an online table before connecting to voice.");
  const { VIVOX_SERVER: server, VIVOX_DOMAIN: domain, VIVOX_ISSUER: issuer, VIVOX_SIGNING_KEY: key } = env;
  if (!server || !domain || !issuer || !key) throw new Error("Table voice is not enabled yet.");
  const user = crypto.createHash("sha256").update(String(socket.data.accountId)).digest("hex").slice(0, 32);
  const channel = `court-${crypto.createHash("sha256").update(`${found.room.code}:${found.room.createdAt}`).digest("hex").slice(0, 32)}`;
  const config = { server, domain, issuer, user, channel };
  if (action === "config") return config;
  if (!["login", "join"].includes(action)) throw new Error("Unsupported voice action.");
  const claims = {
    iss: issuer, exp: Math.floor(Date.now() / 1000) + 90,
    vxi: crypto.randomInt(1, 2 ** 48 - 1), vxa: action,
    f: `sip:.${issuer}.${user}.@${domain}`,
    ...(action === "join" ? { t: `sip:confctl-g-${issuer}.${channel}@${domain}` } : {}),
  };
  const body = `${Buffer.from("{}").toString("base64url")}.${Buffer.from(JSON.stringify(claims)).toString("base64url")}`;
  return { token: `${body}.${crypto.createHmac("sha256", key).update(body).digest("base64url")}` };
}
