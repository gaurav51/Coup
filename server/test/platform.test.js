import test, { before, after } from "node:test";
import assert from "node:assert/strict";
import { MongoMemoryReplSet } from "mongodb-memory-server";
import { MongoStore } from "../src/platform/mongo.js";
import { Accounts } from "../src/platform/auth.js";
import { GameServices } from "../src/platform/game-services.js";
import { DurableRooms } from "../src/platform/room-store.js";
import { CoupGame } from "../src/game.js";

let mongo, store, accounts;
before(async () => {
  mongo = await MongoMemoryReplSet.create({ binary: { version: "8.0.17" }, replSet: { count: 1, storageEngine: "wiredTiger" } });
  store = await new MongoStore(mongo.getUri(), "tests").open();
  accounts = new Accounts(store, { secret: "test-only-secret-with-more-than-32-characters" });
});
after(async () => { await store?.close(); await mongo?.stop(); });

test("guest sessions persist, refresh rotates credentials, and logout invalidates access", async () => {
  const a = await accounts.guest("Alice");
  assert.equal((await accounts.authenticate(a.accessToken)).accountId, a.accountId);
  assert.equal((await accounts.profile(a.accountId)).name, "Alice");
  const b = await accounts.refresh(a);
  assert.notEqual(b.refreshToken, a.refreshToken);
  await assert.rejects(accounts.refresh(a), /reused/);   // replaying a spent token now revokes
  await store.collection("sessions").deleteOne({ _id: b.sessionId });
  await assert.rejects(accounts.authenticate(b.accessToken), /expired/);
});

test("a replayed refresh token revokes its own session and leaves other sessions alone", async () => {
  const first = await accounts.guest("Mallory");
  const second = await accounts.refresh(first);
  const third = await accounts.refresh(second);
  const elsewhere = await accounts.guest("Bystander");

  // Replaying the token spent two rotations ago proves a second holder exists.
  await assert.rejects(accounts.refresh(first), /reused/);

  // The session is gone, so the thief's copy and the honest client's newest
  // token are both dead, and the access token stops working too.
  assert.equal(await store.collection("sessions").findOne({ _id: third.sessionId }), null);
  await assert.rejects(accounts.refresh(third), /expired/);
  await assert.rejects(accounts.authenticate(third.accessToken), /expired/);

  // Another device keeps its own row.
  assert.ok(await accounts.refresh(elsewhere));

  // A token that was never issued is simply refused; it must not revoke anything.
  const victim = await accounts.guest("Victim");
  await assert.rejects(accounts.refresh({ sessionId: victim.sessionId, refreshToken: "never-issued" }), /expired/);
  assert.ok(await accounts.refresh(victim));
});

test("provider challenges cannot be replayed and linked identities cannot be stolen", async () => {
  let nonce;
  const social = new Accounts(store, { secret: "test-only-secret-with-more-than-32-characters", verifyIdentity: async () => ({ sub: "google-user-123", nonce }) });
  const challenge = await social.nonce("google"); nonce = challenge.providerNonce;
  const a = await social.social("google", { idToken: "test-token", challengeId: challenge.challengeId, name: "Social" });
  await assert.rejects(social.social("google", { idToken: "test-token", challengeId: challenge.challengeId }), /already used/);
  const other = await social.guest("Other");
  const next = await social.nonce("google"); nonce = next.providerNonce;
  await assert.rejects(social.social("google", { idToken: "test-token", challengeId: next.challengeId }, other.accountId), /another account/);
  assert.ok(a.profile.id);
});

test("receipt grants are atomic and idempotent under concurrency; refunds cannot be farmed", async () => {
  const a = await accounts.guest("Buyer");
  const services = new GameServices(store, { products: [{ id: "gems", currency: "gems", amount: 100 }], verifyPurchase: async (id) => ({ transaction: "verified-order-1", accountId: id, quantity: 1 }) });
  const input = { provider: "apple", productId: "gems" };
  await Promise.all([services.purchase(a.accountId, input), services.purchase(a.accountId, input)]);
  assert.equal((await accounts.profile(a.accountId)).gems, 100);
  const thief = await accounts.guest("Thief");
  await assert.rejects(services.purchase(thief.accountId, input), /another account/);
  await services.refund("apple", "verified-order-1");
  await services.refund("apple", "verified-order-1");
  assert.equal((await accounts.profile(a.accountId)).gems, 0);
  await assert.rejects(services.purchase(a.accountId, input), /refunded/);
});

test("real Mongo transaction rolls back a failed wallet grant", async () => {
  const a = await accounts.guest("Rollback");
  await assert.rejects(store.transaction(async (session) => {
    await store.collection("accounts").updateOne({ _id: a.accountId }, { $inc: { coins: 1000 } }, { session });
    throw new Error("simulated failure");
  }));
  assert.equal((await accounts.profile(a.accountId)).coins, 0);
});

test("saved matches preserve hidden state and can continue through an exchange", () => {
  const game = new CoupGame(["A", "B"], 1);
  game.declare(0, "Exchange"); game.passChallenge(1);
  const restored = CoupGame.restore(JSON.parse(JSON.stringify(game.snapshot())));
  assert.deepEqual(restored.exchangePool(), game.exchangePool());
  restored.chooseExchangeKeep(restored.exchangePool().slice(0, 2));
  assert.equal(restored.currentPlayerIndex, 1);
  assert.equal(restored.players[0].isAlive, true);
});

test("room checkpoint survives process replacement and resume binds the same account", async () => {
  const a = await accounts.guest("Resume");
  const roomStore = await new DurableRooms(store, { worker: "recovery-test" }).open();
  const created = await roomStore.create({ name: "Resume", socketId: "old", accountId: a.accountId });
  await roomStore.setReady("old", true);
  await roomStore.start("old", undefined, 1);
  const hand = [...roomStore.peek(created.code).game.players[0].influence];
  await assert.rejects(new DurableRooms(store, { worker: "recovery-test" }).open(), /already running/);
  await roomStore.close();
  const replacement = await new DurableRooms(store, { worker: "recovery-test" }).open();
  try {
    await assert.rejects(replacement.resume({ code: created.code, token: created.token, socketId: "bad", accountId: "someone-else" }), /another account/);
    await replacement.resume({ code: created.code, token: created.token, socketId: "new", accountId: a.accountId });
    assert.deepEqual(replacement.peek(created.code).game.players[0].influence, hand);
    await replacement.disconnect("new");
  } finally { await replacement.close(); }
});

test("public queues isolate modes, bind accounts, start human matches, and recover without seat tokens", async () => {
  const rooms = await new DurableRooms(store, { worker: "queue-test" }).open();
  try {
    const a=await accounts.guest("Same"), b=await accounts.guest("Same"), c=await accounts.guest("Ranked");
    const first=await rooms.quickJoin({ accountId:a.accountId,socketId:"qa",name:"Same",mode:"casual" });
    const ranked=await rooms.quickJoin({ accountId:c.accountId,socketId:"qc",name:"Ranked",mode:"ranked" });
    assert.notEqual(first.code,ranked.code);
    await assert.rejects(rooms.quickJoin({accountId:a.accountId,socketId:"dup",name:"Duplicate",mode:"ranked"}),/current table/);
    const second=await rooms.quickJoin({ accountId:b.accountId,socketId:"qb",name:"Same",mode:"casual" });
    assert.equal(second.code,first.code); assert.equal(second.kind,"game");
    assert.equal(rooms.peek(first.code).players.some(p=>p.bot),false);
    await rooms.disconnect("qa");
    const resumed=await rooms.recover({accountId:a.accountId,socketId:"qa-new"});
    assert.equal(resumed.code,first.code);assert.equal(resumed.seat,0);
    assert.equal(await rooms.timeout(first.code,Date.now()),null);
    const room=rooms.peek(first.code); const turn=room.game.currentPlayerIndex;
    await rooms.timeout(first.code,room.deadline+1);
    assert.notEqual(room.game.currentPlayerIndex,turn);
    await rooms.leave("qa-new"); assert.equal(rooms.activeSeat(a.accountId),null);
    await assert.rejects(rooms.resume({code:first.code,token:first.token,accountId:a.accountId,socketId:"abandoned"}),/does not exist/);
    await rooms.disconnect("qc"); assert.equal(rooms.peek(ranked.code),null);
  } finally { await rooms.close(); }
});

test("HTTP APIs enforce account privacy, friend ownership, account deletion and revoked socket sessions", async () => {
  const { createApp }=await import("../src/index.js");
  const { api }=await import("../src/platform/http.js");
  const { io:client }=await import("socket.io-client");
  const rooms=await new DurableRooms(store,{worker:"api-test"}).open();
  const platform={store,accounts,rooms,services:new GameServices(store),browser:{configured:()=>false}};
  platform.handle=api(platform);
  const app=createApp({platform});
  await new Promise(resolve=>app.httpServer.listen(0,"127.0.0.1",resolve));
  const base=`http://127.0.0.1:${app.httpServer.address().port}`;
  const a=await accounts.guest("API A"), b=await accounts.guest("API B"), c=await accounts.guest("API C");
  async function request(method,path,user,body) {
    const res=await fetch(base+path,{method,headers:{"Content-Type":"application/json",...(user?{Authorization:`Bearer ${user.accessToken}`}:{})},body:body?JSON.stringify(body):undefined});
    return {status:res.status,body:await res.json()};
  }
  let socket;
  try {
    assert.equal((await request("GET","/v1/profile")).status,401);
    assert.equal((await request("POST","/v1/missions/play-one/claim",a,{})).status,409);
    assert.equal((await request("POST","/v1/friends",a,{accountId:b.accountId})).status,200);
    const link=(await request("GET","/v1/friends",b)).body.friends[0];
    assert.equal(link.incoming,true);
    const path="/v1/friends/"+encodeURIComponent(link.id);
    assert.equal((await request("PATCH",path,c,{})).status,404);
    assert.equal((await request("PATCH",path,a,{})).status,403);
    assert.equal((await request("PATCH",path,b,{})).status,200);
    assert.equal((await request("GET","/v1/friends",a)).body.friends[0].accepted,true);
    socket=client(base,{transports:["websocket"],auth:{accessToken:a.accessToken},reconnection:false});
    await new Promise((resolve,reject)=>{socket.once("connect",resolve);socket.once("connect_error",reject);});
    await request("POST","/v1/auth/logout",a,{});
    const rejected=await new Promise((resolve,reject)=>socket.timeout(2000).emit("lobby:create",{name:"Invalid"},(err,value)=>err?reject(err):resolve(value)));
    assert.equal(rejected.ok,false);assert.equal(rooms.rooms.size,0);
    assert.equal((await request("DELETE","/v1/account",b)).status,200);
    assert.equal((await request("GET","/v1/profile",b)).status,401);
    assert.equal((await request("GET","/v1/friends",c)).body.friends.length,0);
  } finally { socket?.disconnect(); await new Promise(resolve=>app.io.close(resolve)); await rooms.close(); }
});

test("missed turns and influence choices finish an abandoned match without farming rewards", async () => {
  const services=new GameServices(store);
  const rooms=await new DurableRooms(store,{worker:"afk-test",onFinished:services.completeMatch.bind(services)}).open();
  try {
    const a=await accounts.guest("AFK A"),b=await accounts.guest("AFK B");
    const first=await rooms.quickJoin({accountId:a.accountId,socketId:"afk-a",name:"AFK A",mode:"ranked"});
    await rooms.quickJoin({accountId:b.accountId,socketId:"afk-b",name:"AFK B",mode:"ranked"});
    for(let i=0;i<250&&rooms.peek(first.code).game.phase!=="GameOver";i++) await rooms.timeout(first.code,rooms.peek(first.code).deadline+1);
    assert.equal(rooms.peek(first.code).game.phase,"GameOver");
    assert.equal((await accounts.profile(a.accountId)).games,0);
    assert.equal((await accounts.profile(b.accountId)).rating,1000);
  } finally {await rooms.close();}
});

test("browser sign-in polling requires its secret, expires safely, and is single use", async () => {
  const { BrowserAuth }=await import("../src/platform/browser-auth.js");
  let nonce;
  const identity=new Accounts(store,{secret:"browser-test-secret-longer-than-32-chars",verifyIdentity:async()=>({sub:"browser-user",nonce})});
  const browser=new BrowserAuth(store,identity,{PUBLIC_BASE_URL:"https://game.example",GOOGLE_WEB_CLIENT_ID:"test-client",GOOGLE_WEB_CLIENT_SECRET:"test-secret"});
  const guest=await identity.guest("Browser Guest");
  const started=await browser.start("google",guest.accountId);
  nonce=new URL(started.url).searchParams.get("nonce");
  assert.equal(new URL(started.url).searchParams.get("code_challenge_method"),"S256");
  await assert.rejects(browser.poll(started.state,"wrong-secret"),/expired/);
  assert.equal((await browser.poll(started.state,started.pollToken)).pending,true);
  await assert.rejects(browser.callback("google",{state:"unknown",code:"invalid"}),/expired/);
  await store.collection("browserAuth").updateOne({_id:started.state},{$set:{completed:true,idToken:"test-verified-token"}});
  const result=await browser.poll(started.state,started.pollToken);
  assert.equal(result.accountId,guest.accountId);
  await assert.rejects(browser.poll(started.state,started.pollToken),/expired/);
  assert.equal(new BrowserAuth(store,identity,{}).configured("google"),false);
});
