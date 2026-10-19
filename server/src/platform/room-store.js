import crypto from "node:crypto";
import { RoomStore, saveRoom, restoreRoom } from "../rooms.js";
import { RoomError } from "../errors.js";

/** A worker owns its rooms; MongoDB fences out overlapping/replaced processes. */
export class DurableRooms extends RoomStore {
  constructor(store, { worker = "local", onFinished = async () => {}, maxRooms = 200 } = {}) {
    super();
    this.db = store;
    this.worker = worker;
    this.owner = crypto.randomUUID();
    this.onFinished = onFinished;
    this.maxRooms = maxRooms;
    this.retainDisconnectedLobbies = true;
    this.tail = Promise.resolve();
    this.closed = false;
  }

  async open() {
    const now = new Date();
    try {
      await this.db.collection("workers").findOneAndUpdate(
        { _id: this.worker, expiresAt: { $lte: now } },
        { $set: { owner: this.owner, expiresAt: new Date(Date.now() + 20000) } }, { upsert: true });
    } catch (err) {
      if (err.code === 11000) throw new Error(`Worker ${this.worker} is already running. Use a unique WORKER_ID for another server.`);
      throw err;
    }
    const rows = await this.db.collection("rooms").find({ worker: this.worker, expiresAt: { $gt: now } }).toArray();
    for (const row of rows) this.rooms.set(row._id, restoreRoom(row.saved, true));
    this.heartbeat = setInterval(() => this.renew().catch(() => { this.closed = true; }), 5000);
    this.heartbeat.unref();
    return this;
  }

  async renew() {
    const result = await this.db.collection("workers").updateOne(
      { _id: this.worker, owner: this.owner, expiresAt: { $gt: new Date() } },
      { $set: { expiresAt: new Date(Date.now() + 20000) } });
    if (!result.matchedCount) throw new Error("Room worker lease was lost.");
  }

  mutate(method, args) {
    const work = this.tail.then(async () => {
      if (this.closed) throw new RoomError("Server restarting. Reconnect shortly.");
      if (method === "create" && this.rooms.size >= this.maxRooms) throw new RoomError("Server is full. Try again shortly.");
      const before = new Map([...this.rooms].map(([code, room]) => [code, saveRoom(room)]));
      try {
        const result = RoomStore.prototype[method].apply(this, args);
        if (!result?.code) return result;
        const room = this.peek(result.code);
        const saved = room ? saveRoom(room) : null;
        await this.db.transaction(async (session) => {
          const fence = await this.db.collection("workers").updateOne(
            { _id: this.worker, owner: this.owner, expiresAt: { $gt: new Date() } },
            { $inc: { writes: 1 } }, { session });
          if (!fence.matchedCount) throw new RoomError("Server ownership changed. Reconnect shortly.");
          if (!room) await this.db.collection("rooms").deleteOne({ _id: result.code, worker: this.worker }, { session });
          else {
            await this.db.collection("rooms").replaceOne({ _id: room.code, worker: this.worker }, {
              _id: room.code, worker: this.worker, saved,
              updatedAt: new Date(), expiresAt: new Date(Date.now() + (room.status === "over" ? 3600000 : 86400000)),
            }, { upsert: true, session });
            if (room.game?.phase === "GameOver") await this.onFinished(room, session);
          }
        });
        return result;
      } catch (err) {
        this.rooms = new Map([...before].map(([code, saved]) => [code, restoreRoom(saved)]));
        throw err;
      }
    });
    this.tail = work.catch(() => {});
    return work;
  }

  async close() {
    clearInterval(this.heartbeat);
    await this.tail;
    this.closed = true;
    await this.db.collection("workers").deleteOne({ _id: this.worker, owner: this.owner });
  }
}

for (const method of ["create", "join", "leave", "disconnect", "resume", "setReady", "start", "act", "actSeat", "quickJoin", "recover", "timeout", "startQueued"]) {
  DurableRooms.prototype[method] = function (...args) { return this.mutate(method, args); };
}
