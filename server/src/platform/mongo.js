import { MongoClient } from "mongodb";

/** Shared durable data. A replica set is required for wallet/receipt transactions. */
export class MongoStore {
  constructor(uri, database = "coup") {
    if (!uri) throw new Error("MONGODB_URI is required.");
    this.client = new MongoClient(uri, { maxPoolSize: 20, serverSelectionTimeoutMS: 5000 });
    this.database = database;
  }

  async open() {
    await this.client.connect();
    this.db = this.client.db(this.database);
    const hello = await this.db.admin().command({ hello: 1 });
    if (!hello.setName && hello.msg !== "isdbgrid") throw new Error("MongoDB must be a replica set (including local development).");
    await Promise.all([
      this.db.collection("identities").createIndex({ provider: 1, subject: 1 }, { unique: true }),
      this.db.collection("sessions").createIndex({ expiresAt: 1 }, { expireAfterSeconds: 0 }),
      this.db.collection("browserAuth").createIndex({ expiresAt: 1 }, { expireAfterSeconds: 0 }),
      this.db.collection("nonces").createIndex({ expiresAt: 1 }, { expireAfterSeconds: 0 }),
      this.db.collection("receipts").createIndex({ provider: 1, transaction: 1 }, { unique: true }),
      this.db.collection("matches").createIndex({ accountIds: 1, endedAt: -1 }),
      this.db.collection("rooms").createIndex({ expiresAt: 1 }, { expireAfterSeconds: 0 }),
      this.db.collection("rooms").createIndex({ worker: 1 }),
      this.db.collection("accounts").createIndex({ rating: -1, _id: 1 }),
    ]);
    return this;
  }

  collection(name) { return this.db.collection(name); }
  async transaction(fn) {
    const session = this.client.startSession();
    try {
      return await session.withTransaction(() => fn(session), {
        readConcern: { level: "snapshot" }, writeConcern: { w: "majority" }, readPreference: "primary",
      });
    } finally { await session.endSession(); }
  }
  async close() { await this.client.close(); }
}
