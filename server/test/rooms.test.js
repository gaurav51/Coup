import assert from "node:assert/strict";
import test from "node:test";
import { botCommand, nextBotSeat } from "../src/bots.js";
import { RoomStore } from "../src/rooms.js";

function table(names) {
  const store = new RoomStore();
  const host = store.create({ name: names[0], socketId: "sock-0" });
  const joined = [host];
  for (let i = 1; i < names.length; i += 1) {
    joined.push(store.join({ code: host.code, name: names[i], socketId: `sock-${i}` }));
  }
  for (let i = 0; i < names.length; i += 1) store.setReady(`sock-${i}`, true);
  store.start("sock-0", 1);
  return { store, code: host.code, ids: names.map((_, index) => `sock-${index}`) };
}

test("the steal victim can commit to a block while the claim is still open", () => {
  const { store, code, ids } = table(["Ann", "Ben", "Cara"]);
  store.act(ids[0], { type: "declare", action: "Steal", target: 2 });
  store.act(ids[2], { type: "block", role: "Ambassador" });

  let game = store.peek(code).game;
  assert.equal(game.phase, "AwaitingChallenge");
  assert.equal(store.peek(code).queuedBlock.role, "Ambassador");

  store.act(ids[1], { type: "pass" });
  game = store.peek(code).game;
  assert.equal(game.phase, "AwaitingBlockChallenge");
  assert.equal(game.pendingBlocker, 2);
  assert.equal(game.pendingBlockClaim, "Ambassador");
  assert.equal(game.players[0].coins, 2);
});

test("a queued block is dropped when the steal was a bluff", () => {
  const { store, code, ids } = table(["Ann", "Ben", "Cara"]);
  store.peek(code).game.players[0].influence = ["Duke", "Duke"];
  store.act(ids[0], { type: "declare", action: "Steal", target: 2 });
  store.act(ids[2], { type: "block", role: "Captain" });
  store.act(ids[1], { type: "challenge" });

  const game = store.peek(code).game;
  assert.equal(game.phase, "AwaitingInfluenceLoss");
  store.act(ids[0], { type: "lose", role: "Duke" });

  const finished = store.peek(code).game;
  assert.equal(finished.phase, "AwaitingAction");
  assert.equal(finished.currentPlayerIndex, 1);
  assert.equal(store.peek(code).queuedBlock, null);
  assert.equal(
    finished.events.some((event) => event.kind === "BlockDeclared"),
    false,
  );
});

test("the host can fill empty seats and those seats take their turn", () => {
  const store = new RoomStore();
  const host = store.create({ name: "Ann", socketId: "sock-0" });
  store.setReady("sock-0", true);
  store.start("sock-0", 1, 1);

  const room = store.peek(host.code);
  assert.equal(room.players.length, 2);
  assert.equal(room.players[1].bot, true);
  assert.equal(room.players[1].socketId, null);

  store.act("sock-0", { type: "declare", action: "Income" });
  assert.equal(nextBotSeat(room), 1);
  const result = store.actSeat(host.code, 1, botCommand(room.game, room.players[1]));
  const humanMustAnswer = room.game.responders.has(0);
  const humanToMove = room.game.phase === "AwaitingAction" && room.game.currentPlayerIndex === 0;
  assert.equal(humanMustAnswer || humanToMove, true);
  assert.equal(result.states.length, 1);
  assert.equal(result.states[0].socketId, "sock-0");
  assert.equal(result.states[0].state.players[1].hand, undefined);
});

test("exchange cards are sent only to the player who drew them", () => {
  const { store, ids } = table(["Ann", "Ben"]);
  store.act(ids[0], { type: "declare", action: "Exchange" });
  const result = store.act(ids[1], { type: "pass" });
  const actor = result.states.find((row) => row.socketId === ids[0]).state;
  const other = result.states.find((row) => row.socketId === ids[1]).state;

  assert.equal(actor.allowed.exchange.pool.length, 4);
  assert.equal(actor.allowed.exchange.keep, 2);
  assert.equal(other.allowed.exchange, null);
  assert.equal(JSON.stringify(other).includes('"pool"'), false);
  assert.equal(other.players[0].hand, undefined);
  assert.equal(typeof other.players[0].influence, "number");
  assert.deepEqual(actor.hand, store.peek(result.code).game.players[0].influence);
});

test("queued seats survive a brief disconnect and retain timer and identity on resume", () => {
  const store=new RoomStore();store.retainDisconnectedLobbies=true;
  const first=store.quickJoin({accountId:"one",socketId:"one",name:"One",mode:"casual"});
  const second=store.quickJoin({accountId:"two",socketId:"two",name:"Two",mode:"casual"});
  assert.equal(first.code,second.code);
  store.disconnect("one");
  assert.equal(store.peek(first.code).players[1].accountId,"two");
  assert.equal(store.startQueued(first.code),null);
  const resumed=store.recover({accountId:"one",socketId:"one-new"});
  assert.equal(resumed.seat,first.seat);
  const view=resumed.recipients.find(r=>r.socketId==="one-new").lobby;
  assert.equal(view.mode,"casual");assert.ok(view.queueDeadline>view.serverTime);
  store.leave("one-new");
  assert.equal(store.activeSeat("one"),null);
});

test("four accounts with the same display name join one public table", () => {
  const store=new RoomStore();let code;
  for(let i=0;i<4;i++){
    const result=store.quickJoin({accountId:`same-${i}`,socketId:`same-${i}`,name:"Player",mode:"casual"});
    if(code)assert.equal(result.code,code);else code=result.code;
  }
  assert.equal(store.peek(code).status,"playing");
  assert.equal(new Set(store.peek(code).players.map(p=>p.name)).size,4);
});

test("expired disconnected casual seat is removed without preventing the remaining table from starting", () => {
  const store=new RoomStore();store.retainDisconnectedLobbies=true;
  const a=store.quickJoin({accountId:"a",socketId:"a",name:"A",mode:"casual"});
  store.quickJoin({accountId:"b",socketId:"b",name:"B",mode:"casual"});
  store.disconnect("a");store.peek(a.code).players[0].disconnectedAt=Date.now()-31000;
  const started=store.startQueued(a.code);
  assert.equal(started.kind,"game");assert.equal(store.activeSeat("a"),null);
  assert.equal(store.peek(a.code).players[0].accountId,"b");
});
