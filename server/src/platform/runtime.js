import { MongoStore } from "./mongo.js";
import { Accounts } from "./auth.js";
import { GameServices } from "./game-services.js";
import { DurableRooms } from "./room-store.js";
import { PurchaseVerifier } from "./purchases.js";
import { BrowserAuth } from "./browser-auth.js";
import { api } from "./http.js";

export async function createPlatform(env = process.env) {
  const store = await new MongoStore(env.MONGODB_URI, env.MONGODB_DATABASE || "coup").open();
  try {
    const accounts = new Accounts(store, { secret: env.AUTH_SECRET, appleAudience: env.APPLE_SIGNIN_CLIENT_ID,
      googleAudiences: [...(env.GOOGLE_CLIENT_IDS || "").split(","), env.GOOGLE_WEB_CLIENT_ID].filter(Boolean) });
    const purchases = new PurchaseVerifier(env);
    const products = JSON.parse(env.STORE_PRODUCTS_JSON || "[]");
    const services = new GameServices(store, { products, verifyPurchase: purchases.verify.bind(purchases), finishPurchase: purchases.finish.bind(purchases) });
    const rooms = await new DurableRooms(store, { worker: env.WORKER_ID || "local", maxRooms: Number(env.MAX_ROOMS) || 200,
      onFinished: services.completeMatch.bind(services) }).open();
    const platform = { store, accounts, services, rooms, browser: new BrowserAuth(store, accounts, env), content: JSON.parse(env.LIVE_CONTENT_JSON || "{}"), trustProxy: env.TRUST_PROXY === "1" };
    platform.handle = api(platform);
    const retry = setInterval(async () => {
      try {
        const pending = await store.collection("receipts").find({ pendingFinish: true }).limit(20).toArray();
        for (const row of pending) await services.finishPending(row._id).catch(() => {});
      } catch { /* Database readiness is exposed separately; never log proofs. */ }
    }, 30000);
    retry.unref();
    platform.close = async () => { clearInterval(retry); await rooms.close(); await store.close(); };
    return platform;
  } catch (err) { await store.close(); throw err; }
}
