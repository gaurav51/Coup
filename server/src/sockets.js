import { voiceAccess } from "./voice.js";
import { isRejection } from "./errors.js";
import { Client, Server } from "./protocol.js";
import { RoomStore, parseCommand } from "./rooms.js";
import { scheduleBots, cancelBots } from "./bots.js";
import { RateLimit } from "./platform/rate-limit.js";

export function channel(code) {
  return `table:${code}`;
}

export function attachSockets(io, store = new RoomStore()) {
  const sweep = setInterval(async () => {
    // Public tables whose matchmaking window lapsed: seat bots and deal.
    for (const code of store.expiredQueues?.() ?? []) {
      try {
        const started = await store.startQueued(code);
        if (started) pushMatch(io, store, started);
      } catch (err) { console.error("[coup] queue start failed", err.name); }
    }
    for (const room of [...store.rooms.values()]) {
      if (!room.game || room.game.phase === "GameOver" || !room.deadline || room.deadline > Date.now()) continue;
      try {
        const result = await store.timeout(room.code);
        if (result) pushMatch(io, store, result);
      } catch (err) { console.error("[coup] deadline commit failed", err.name); }
    }
  }, 1000);
  sweep.unref();
  io.engine.once("close", () => { clearInterval(sweep); for (const code of store.rooms.keys()) cancelBots(code); });
  for (const room of store.rooms.values()) scheduleBots(store, room.code, result => pushMatch(io, store, result));
  io.on("connection", (socket) => {
    const limits = new RateLimit({ capacity: 30, refillPerSecond: 5, maxKeys: 1 });
    socket.use((packet, next) => {
      if (limits.take("commands")) return next();
      const ack = packet.at(-1);
      if (typeof ack === "function") ack({ ok: false, error: "Slow down and try again." });
      socket.emit(Server.REJECTED, { message: "Slow down and try again." });
    });
    socket.onAny((event, ...args) => {
      const data = args.filter((item) => typeof item !== "function");
      logJson("<-", event, data.length === 1 ? data[0] : data);
    });

    socket.on("voice:access", (payload, ack) => {
      const [data, done] = normalize(payload, ack);
      try { done({ ok: true, ...voiceAccess(store, socket, data.action) }); }
      catch (error) { done({ ok: false, error: error.message }); }
    });

    socket.on("queue:join", async (payload, ack) => {
      const [data, done] = normalize(payload, ack);
      try {
        const result = await store.quickJoin({ accountId: socket.data.accountId, socketId: socket.id, name: data.name, mode: data.mode });
        bind(socket, result);
        if (result.kind === "lobby") pushLobby(io, result.recipients); else pushMatch(io, store, result);
        done({ ok: true, code: result.code, token: result.token, seat: result.seat });
      } catch (err) { fail(socket, done, err); }
    });
    socket.on("session:recover", async (payload, ack) => {
      const [, done] = normalize(payload, ack);
      try {
        const result = await store.recover({ accountId: socket.data.accountId, socketId: socket.id });
        retire(io, result.replacedSocketId, result.code); bind(socket, result);
        if (result.kind === "lobby") pushLobby(io, result.recipients); else pushMatch(io, store, result);
        done({ ok: true, code: result.code, token: result.token, seat: result.seat });
      } catch (err) { fail(socket, done, err); }
    });
    socket.on(Client.CREATE, async (payload, ack) => {
      const [data, done] = normalize(payload, ack);
      try {
        const result = await store.create({ name: data.name, socketId: socket.id, accountId: socket.data.accountId });
        bind(socket, result);
        pushLobby(io, result.recipients);
        console.log(`[coup ${result.code}] ${seatName(store, result)} created the table`);
        done({ ok: true, code: result.code, token: result.token, seat: result.seat });
      } catch (err) {
        fail(socket, done, err);
      }
    });

    socket.on(Client.JOIN, async (payload, ack) => {
      const [data, done] = normalize(payload, ack);
      try {
        const result = await store.join({ code: data.code, name: data.name, socketId: socket.id, accountId: socket.data.accountId });
        bind(socket, result);
        pushLobby(io, result.recipients);
        console.log(`[coup ${result.code}] ${seatName(store, result)} joined`);
        done({ ok: true, code: result.code, token: result.token, seat: result.seat });
      } catch (err) {
        fail(socket, done, err);
      }
    });

    socket.on(Client.LEAVE, async (payload, ack) => {
      const [, done] = normalize(payload, ack);
      try {
        const code = socket.data.code;
        const result = await store.leave(socket.id);
        unbind(socket);
        if (result.kind === "lobby") pushLobby(io, result.recipients);
        else pushMatch(io, store, result);
        console.log(`[coup ${code}] a player left`);
        done({ ok: true });
      } catch (err) {
        fail(socket, done, err);
      }
    });

    socket.on(Client.READY, async (payload, ack) => {
      const [data, done] = normalize(payload, ack);
      try {
        const result = await store.setReady(socket.id, data.ready);
        pushLobby(io, result.recipients);
        const ready = store.peek(result.code)?.players.find((seat) => seat.socketId === socket.id);
        console.log(`[coup ${result.code}] ${ready?.name ?? "a player"} is ${ready?.ready ? "ready" : "not ready"}`);
        done({ ok: true });
      } catch (err) {
        fail(socket, done, err);
      }
    });

    socket.on(Client.START, async (payload, ack) => {
      const [data, done] = normalize(payload, ack);
      try {
        const bots = Number(data.bots);
        const result = await store.start(
          socket.id,
          undefined,
          Number.isInteger(bots) ? bots : 0,
        );
        const room = store.peek(result.code);
        const names = room.players.map((seat) => (seat.bot ? `${seat.name} (bot)` : seat.name)).join(", ");
        console.log(`[coup ${result.code}] started: ${names}`);
        pushMatch(io, store, result);
        done({ ok: true, code: result.code });
      } catch (err) {
        fail(socket, done, err);
      }
    });

    socket.on(Client.RESUME, async (payload, ack) => {
      const [data, done] = normalize(payload, ack);
      try {
        const result = await store.resume({ code: data.code, token: data.token, socketId: socket.id, accountId: socket.data.accountId });
        retire(io, result.replacedSocketId, result.code);
        bind(socket, result);
        console.log(`[coup ${result.code}] ${seatName(store, result)} resumed`);
        if (result.kind === "lobby") pushLobby(io, result.recipients);
        else pushMatch(io, store, result);
        done({ ok: true, code: result.code, token: result.token, seat: result.seat });
      } catch (err) {
        fail(socket, done, err);
      }
    });

    for (const [event, type] of [
      [Client.DECLARE, "declare"],
      [Client.CHALLENGE, "challenge"],
      [Client.PASS, "pass"],
      [Client.BLOCK, "block"],
      [Client.LOSE, "lose"],
      [Client.EXCHANGE, "exchange"],
    ]) {
      socket.on(event, async (payload, ack) => {
        const [data, done] = normalize(payload, ack);
        try {
          const result = await store.act(socket.id, parseCommand(type, data));
          pushMatch(io, store, result);
          done({ ok: true });
        } catch (err) {
          fail(socket, done, err);
        }
      });
    }

    socket.on("disconnect", async () => {
      const code = socket.data.code;
      let result;
      try { result = await store.disconnect(socket.id); }
      catch (err) { console.error("[coup] disconnect checkpoint failed", err.name); return; }
      if (!result) return;
      if (code) console.log(`[coup ${code}] a player disconnected`);
      if (result.kind === "lobby") pushLobby(io, result.recipients);
      else pushMatch(io, store, result);
    });
  });

  return store;
}

function retire(io, socketId, code) {
  if (!socketId) return;
  const previous = io.sockets.sockets.get(socketId);
  if (!previous) return;
  if (code) previous.leave(channel(code));
  previous.data.code = null;
  previous.data.token = null;
  emit(previous, Server.REJECTED, { message: "This seat was resumed somewhere else." });
}

function bind(socket, result) {
  if (socket.data.code && socket.data.code !== result.code) socket.leave(channel(socket.data.code));
  socket.data.code = result.code;
  socket.data.token = result.token;
  socket.join(channel(result.code));
}

function unbind(socket) {
  if (socket.data.code) socket.leave(channel(socket.data.code));
  socket.data.code = null;
  socket.data.token = null;
}

function pushLobby(io, recipients) {
  if (!recipients) return;
  for (const row of recipients) {
    emit(io.sockets.sockets.get(row.socketId), Server.LOBBY, row.lobby);
  }
}

function pushMatch(io, store, result) {
  if (!result?.code) return;
  pushLobby(io, result.lobbies);

  if (result.fullLog) {
    for (const row of result.states) {
      const sock = io.sockets.sockets.get(row.socketId);
      if (!sock) continue;
      emit(sock, Server.LOG, { events: result.fullLog });
      emit(sock, Server.STATE, row.state);
    }
  } else {
    for (const event of result.events ?? []) emit(io.to(channel(result.code)), Server.EVENT, event);
    for (const row of result.states ?? []) {
      emit(io.sockets.sockets.get(row.socketId), Server.STATE, row.state);
    }
  }

  if (result.over) emit(io.to(channel(result.code)), Server.OVER, result.over);
  for (const event of result.fullLog ?? result.events ?? []) {
    if (event?.text) console.log(`[coup ${result.code}] ${event.text}`);
  }
  const pacing = result.fullLog ? [] : (result.events ?? []);
  scheduleBots(store, result.code, (next) => pushMatch(io, store, next), pacing);
}

function fail(socket, ack, err) {
  const message = isRejection(err) ? err.message : "Something went wrong.";
  const code = socket.data.code ? ` ${socket.data.code}` : "";
  console.log(`[coup${code}] rejected: ${message}`);
  if (!isRejection(err)) console.error(err);
  emit(socket, Server.REJECTED, { message });
  ack({ ok: false, error: message });
}

function emit(target, event, payload) {
  if (!target) return;
  logJson("->", event, payload);
  target.emit(event, payload);
}

function logJson(direction, event, payload) {
  // Payloads contain bearer tokens, hands and exchange pools. Never log them.
  if (process.env.LOG_EVENTS === "1") console.log(`[coup ${direction}] ${event}`);
}

function seatName(store, result) {
  const room = store.peek(result.code);
  return room?.players[result.seat]?.name ?? "a player";
}

function normalize(payload, ack) {
  if (typeof payload === "function") return [{}, payload];
  const data = payload && typeof payload === "object" ? payload : {};
  const done = typeof ack === "function"
    ? (body) => {
        logJson("->", "ack", body);
        ack(body);
      }
    : () => {};
  return [data, done];
}
