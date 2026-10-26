import assert from "node:assert/strict";
import test from "node:test";
import { io as ioClient } from "socket.io-client";
import { createApp } from "../src/index.js";

async function boot() {
  const app = createApp();
  await new Promise((resolve) => app.httpServer.listen(0, "127.0.0.1", resolve));
  const { port } = app.httpServer.address();
  return { ...app, url: `http://127.0.0.1:${port}` };
}

function connect(url) {
  const socket = ioClient(url, {
    transports: ["websocket"],
    forceNew: true,
    reconnection: false,
  });
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error("connect timeout")), 3000);
    socket.once("connect", () => {
      clearTimeout(timer);
      resolve(socket);
    });
  });
}

function ack(socket, event, payload = {}) {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error(`timeout waiting for ack ${event}`)), 3000);
    socket.emit(event, payload, (response) => {
      clearTimeout(timer);
      resolve(response);
    });
  });
}

async function shutdown(app, sockets = []) {
  for (const socket of sockets) socket.disconnect();
  app.io.close();
  if (app.httpServer.listening) {
    await new Promise((resolve) => app.httpServer.close(resolve));
  }
}

function next(socket, event) {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error(`timeout waiting for ${event}`)), 3000);
    socket.once(event, (payload) => {
      clearTimeout(timer);
      resolve(payload);
    });
  });
}

async function seatTwo(url, names = ["Ann", "Ben"]) {
  const [a, b] = await Promise.all([connect(url), connect(url)]);
  const created = await ack(a, "lobby:create", { name: names[0] });
  assert.equal(created.ok, true);
  const joined = await ack(b, "lobby:join", { code: created.code, name: names[1] });
  assert.equal(joined.ok, true);
  assert.equal((await ack(a, "lobby:ready", { ready: true })).ok, true);
  assert.equal((await ack(b, "lobby:ready", { ready: true })).ok, true);
  const aState = next(a, "game:state");
  const bState = next(b, "game:state");
  const started = await ack(a, "lobby:start", { seed: 1 });
  assert.equal(started.ok, true);
  return { a, b, created, joined, opening: { a: await aState, b: await bState } };
}

test("health and protocol endpoints answer", async () => {
  const app = await boot();
  try {
    const health = await fetch(`${app.url}/health`);
    assert.equal((await health.json()).ok, true);
    const protocol = await fetch(`${app.url}/protocol`);
    const body = await protocol.json();
    assert.equal(body.client["game:declare"].action.includes("Steal"), true);
    assert.equal(body.server["game:state"].includes("own hand"), true);
  } finally {
    await shutdown(app);
  }
});

test("a match plays through socket events and hides the other hand", async () => {
  const app = await boot();
  let sockets = [];
  try {
    const { a, b, opening } = await seatTwo(app.url);
    sockets = [a, b];
    assert.equal(opening.a.you, 0);
    assert.equal(opening.a.hand.length, 2);
    assert.equal(opening.b.hand.length, 2);
    assert.equal(opening.a.players[0].coins, 1);
    assert.equal(opening.a.players[1].hand, undefined);
    assert.equal(opening.b.players[0].hand, undefined);

    const early = await ack(b, "game:declare", { action: "Income" });
    assert.equal(early.ok, false);
    assert.match(early.error, /Not your turn/);

    const aAfter = next(a, "game:state");
    const bAfter = next(b, "game:state");
    const declared = await ack(a, "game:declare", { action: "ForeignAid" });
    assert.equal(declared.ok, true);
    const actor = await aAfter;
    const target = await bAfter;
    assert.equal(actor.phase, "AwaitingBlock");
    assert.deepEqual(actor.allowed.block, []);
    assert.deepEqual(target.allowed.block, ["Duke"]);

    const aBlocked = next(a, "game:state");
    const blocked = await ack(b, "game:block", { role: "Duke" });
    assert.equal(blocked.ok, true);
    assert.equal((await aBlocked).phase, "AwaitingBlockChallenge");

    const aDone = next(a, "game:state");
    const passed = await ack(a, "game:pass");
    assert.equal(passed.ok, true);
    const done = await aDone;
    assert.equal(done.players[0].coins, 1);
    assert.equal(done.currentPlayer, 1);
    assert.equal(done.phase, "AwaitingAction");
  } finally {
    await shutdown(app, sockets);
  }
});

test("exchange draws stay on the exchanging socket", async () => {
  const app = await boot();
  let sockets = [];
  try {
    const { a, b } = await seatTwo(app.url);
    sockets = [a, b];
    assert.equal((await ack(a, "game:declare", { action: "Exchange" })).ok, true);

    const seen = [];
    const onEvent = (event) => seen.push(event);
    b.on("game:event", onEvent);
    const actorState = next(a, "game:state");
    const otherState = next(b, "game:state");
    assert.equal((await ack(b, "game:pass")).ok, true);
    b.off("game:event", onEvent);

    const actor = await actorState;
    const other = await otherState;
    const drawn = seen.find((event) => event.kind === "ExchangeDrawn");
    assert.equal(actor.phase, "AwaitingExchangeSelection");
    assert.equal(actor.allowed.exchange.pool.length, 4);
    assert.equal(other.allowed.exchange, null);
    assert.equal(JSON.stringify(other).includes('"pool"'), false);
    assert.equal(JSON.stringify(seen).includes("pool"), false);
    assert.ok(drawn);
    assert.equal(drawn.role, null);
    assert.equal(drawn.amount, 2);

    const kept = actor.allowed.exchange.pool.slice(0, 2);
    const finished = next(a, "game:state");
    assert.equal((await ack(a, "game:exchange", { keep: kept })).ok, true);
    const after = await finished;
    assert.equal(after.phase, "AwaitingAction");
    assert.equal(after.currentPlayer, 1);
    assert.deepEqual(after.hand, kept);
  } finally {
    await shutdown(app, sockets);
  }
});

test("a dropped connection can resume the same seat", async () => {
  const app = await boot();
  const sockets = [];
  try {
    const table = await seatTwo(app.url);
    sockets.push(table.a, table.b);
    const hand = table.opening.a.hand;

    const dropped = new Promise((resolve, reject) => {
      const timer = setTimeout(() => reject(new Error("disconnect timeout")), 3000);
      table.a.once("disconnect", () => {
        clearTimeout(timer);
        resolve(undefined);
      });
    });
    table.a.disconnect();
    await dropped;

    const resumed = await connect(app.url);
    sockets.push(resumed);
    const state = next(resumed, "game:state");
    const log = next(resumed, "game:log");
    const again = await ack(resumed, "session:resume", {
      code: table.created.code,
      token: table.created.token,
    });
    assert.equal(again.ok, true);
    assert.equal(again.seat, 0);
    assert.deepEqual((await state).hand, hand);
    assert.equal((await log).events[0].kind, "GameStarted");
  } finally {
    await shutdown(app, sockets);
  }
});

test("two live tables recover their own seats without cross-table state delivery", async () => {
  const app=await boot();let sockets=[];
  try {
    const one=await seatTwo(app.url,["One-A","One-B"]);
    const two=await seatTwo(app.url,["Two-A","Two-B"]);
    sockets=[one.a,one.b,two.a,two.b];
    assert.notEqual(one.created.code,two.created.code);
    one.b.disconnect();two.b.disconnect();
    const [ra,rb]=await Promise.all([connect(app.url),connect(app.url)]);sockets.push(ra,rb);
    const sa=next(ra,"game:state"),sb=next(rb,"game:state");
    const replies=await Promise.all([
      ack(ra,"session:resume",{code:one.created.code,token:one.joined.token}),
      ack(rb,"session:resume",{code:two.created.code,token:two.joined.token})]);
    assert.ok(replies.every(r=>r.ok));
    const [a,b]=await Promise.all([sa,sb]);
    assert.equal(a.you,1);assert.equal(b.you,1);
    assert.equal(a.players[1].name,"One-B");assert.equal(b.players[1].name,"Two-B");
    let leaked=false;rb.on("game:state",()=>{leaked=true;});
    const changed=next(one.a,"game:state");
    assert.equal((await ack(one.a,"game:declare",{action:"Income"})).ok,true);
    await changed;await new Promise(resolve=>setTimeout(resolve,50));
    assert.equal(leaked,false);
  } finally {await shutdown(app,sockets);}
});
