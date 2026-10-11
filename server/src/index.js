import { createServer } from "node:http";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { Server } from "socket.io";
import { protocolDocument } from "./protocol.js";
import { attachSockets } from "./sockets.js";

export function createApp() {
  const httpServer = createServer((req, res) => {
    const url = req.url?.split("?")[0];
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
    cors: { origin: "*" },
  });
  const store = attachSockets(io);
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
  const { httpServer } = createApp();
  httpServer.listen(port, () => {
    console.log(`Coup server listening on ${port}`);
    console.log(`Health: http://localhost:${port}/health`);
    console.log(`Events: http://localhost:${port}/protocol`);
  });
}
