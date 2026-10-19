import { createServer } from "node:http";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { Server } from "socket.io";
import { protocolDocument } from "./protocol.js";
import { attachSockets } from "./sockets.js";
import { RateLimit } from "./platform/rate-limit.js";
import { createPlatform } from "./platform/runtime.js";

export function createApp({ platform = null } = {}) {
  const httpServer = createServer(async (req, res) => {
    if (platform && await platform.handle(req, res)) return;
    const url = req.url?.split("?")[0];
    if (url === "/ready") {
      try {
        if (!platform || platform.rooms.closed) throw new Error("Unavailable");
        await platform.store.collection("workers").findOne({ _id: platform.rooms.worker,
          owner: platform.rooms.owner, expiresAt: { $gt: new Date() } }).then(row => {
          if (!row) throw new Error("Unavailable");
        });
        sendJson(res, 200, { ok: true, authenticated: true, storage: "mongodb" });
      } catch { sendJson(res, 503, { ok: false }); }
      return;
    }
    if (url === "/" || url === "/health") {
      sendJson(res, 200, { ok: true, game: "coup" });
      return;
    }
    if (url === "/protocol") {
      sendJson(res, 200, protocolDocument);
      return;
    }
    sendJson(res, 404, { ok: false, error: "Not found." });
  });

  const io = new Server(httpServer, {
    cors: { origin: process.env.CORS_ORIGINS?.split(",").filter(Boolean) || false },
    maxHttpBufferSize: 32768,
  });
  if (platform) io.use(async (socket, next) => {
    try {
      const user = await platform.accounts.authenticate(socket.handshake.auth?.accessToken);
      socket.data.accountId = user.accountId;
      const commandLimit = new RateLimit({ capacity: 30, refillPerSecond: 5, maxKeys: 1 });
      socket.use(async (packet, nextPacket) => {
        const ack = packet.at(-1);
        if (!commandLimit.take("commands")) { if (typeof ack === "function") ack({ ok: false, error: "Slow down and try again." }); return; }
        try { await platform.accounts.authenticate(socket.handshake.auth?.accessToken); nextPacket(); }
        catch { if (typeof ack === "function") ack({ ok: false, error: "Session expired. Sign in again." }); socket.disconnect(true); }
      });
      const timer = setTimeout(() => socket.disconnect(true), Math.max(0, user.expiresAt - Date.now()));
      timer.unref(); socket.once("disconnect", () => clearTimeout(timer));
      next();
    } catch { next(new Error("Sign in again to connect.")); }
  });
  const store = attachSockets(io, platform?.rooms);
  return { httpServer, io, store };
}

function sendJson(res, status, body) {
  res.writeHead(status, { "Content-Type": "application/json" });
  res.end(JSON.stringify(body));
}

const thisFile = fileURLToPath(import.meta.url);
const invoked = process.argv[1] ? path.resolve(process.argv[1]) : "";

if (thisFile === invoked) {
  const port = Number(process.env.PORT) || 3000;
  const platform = process.env.MONGODB_URI ? await createPlatform() : null;
  if (process.env.NODE_ENV === "production" && !platform) throw new Error("Production requires MongoDB, authentication and durable room storage.");
  const { httpServer, io } = createApp({ platform });
  httpServer.listen(port, process.env.HOST || "127.0.0.1", () => {
    console.log(`Coup server listening on ${port}`);
    console.log(`Health: http://localhost:${port}/health`);
    console.log(`Events: http://localhost:${port}/protocol`);
  });
  let closing = false;
  const close = async () => {
    if (closing) return; closing = true;
    await new Promise((resolve) => io.close(resolve));
    if (platform) await platform.close();
    process.exit(0);
  };
  process.on("SIGTERM", close); process.on("SIGINT", close);
}
