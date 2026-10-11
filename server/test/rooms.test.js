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
