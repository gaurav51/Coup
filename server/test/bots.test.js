import assert from "node:assert/strict";
import { mock } from "node:test";
import test from "node:test";
import { decide } from "../src/bot-mind.js";
import { botCommand, delayBeforeBot, nextBotSeat, scheduleBots } from "../src/bots.js";
import { CoupGame } from "../src/game.js";
import { RoomStore } from "../src/rooms.js";

const quiet = {
  aggression: 0,
  bluffChance: 0,
  suspicion: 0,
  blockBluffChance: 0,
};

function mind(seat, personality = quiet) {
  return { seat, personality, rng: () => 0 };
}

test("a bot holding the Duke taxes instead of taking income", () => {
  const game = new CoupGame(["Ann", "Valca"], 1);
  game.players[1].influence = ["Duke", "Ambassador"];
  game.players[1].coins = 2;
  game.currentPlayerIndex = 1;

  assert.deepEqual(decide(game, mind(1)), { type: "declare", action: "Tax" });
});

test("ten coins forces a Coup even when the bot holds a Duke", () => {
  const game = new CoupGame(["Ann", "Valca"], 1);
  game.players[1].influence = ["Duke", "Duke"];
  game.players[1].coins = 10;
  game.currentPlayerIndex = 1;

  assert.deepEqual(decide(game, mind(1)), { type: "declare", action: "Coup", target: 0 });
});

test("a bot with Contessa blocks an assassination it is not sure is a bluff", () => {
  const game = new CoupGame(["Ann", "Valca"], 1);
  game.players[0].influence = ["Captain", "Duke"];
  game.players[0].coins = 3;
  game.players[1].influence = ["Contessa", "Ambassador"];
  game.declare(0, "Assassinate", 1);

  assert.equal(game.phase, "AwaitingChallenge");
  assert.deepEqual(decide(game, mind(1)), { type: "pass" });

  game.passChallenge(1);
  assert.equal(game.phase, "AwaitingBlock");
  assert.deepEqual(decide(game, mind(1)), { type: "block", role: "Contessa" });
});

test("a bot challenges a claim once every copy of that role is accounted for", () => {
  const game = new CoupGame(["Ann", "Valca", "Cara"], 1);
  game.players[0].influence = ["Duke", "Captain"];
  game.players[0].coins = 3;
  game.players[1].influence = ["Assassin", "Assassin"];
  game.players[2].revealed = ["Assassin"];
  game.currentPlayerIndex = 0;
  game.declare(0, "Assassinate", 1);

  assert.deepEqual(decide(game, mind(1)), { type: "challenge" });
});

test("a bot gives up its least useful card", () => {
  const game = new CoupGame(["Ann", "Valca"], 1);
  game.players[0].coins = 7;
  game.players[1].influence = ["Duke", "Ambassador"];
  game.declare(0, "Coup", 1);

  assert.equal(game.phase, "AwaitingInfluenceLoss");
  assert.deepEqual(decide(game, mind(1)), { type: "lose", role: "Ambassador" });
});

test("an exchange keeps stronger cards and does not duplicate a single copy", () => {
  const game = new CoupGame(["Ann", "Valca"], 1);
  game.players[1].influence = ["Ambassador", "Ambassador"];
  game.currentPlayerIndex = 1;
  game.declare(1, "Exchange");
  game.passChallenge(0);

  const pool = game.exchangePool();
  const command = decide(game, mind(1));
  assert.equal(command.type, "exchange");
  assert.equal(command.keep.length, 2);

  const leftover = [...pool];
  for (const role of command.keep) {
    const at = leftover.indexOf(role);
    assert.notEqual(at, -1, `${role} was kept twice`);
    leftover.splice(at, 1);
  }
  if (pool.includes("Duke")) assert.ok(command.keep.includes("Duke"));
  game.chooseExchangeKeep(command.keep);
  assert.equal(game.phase, "AwaitingAction");
});

test("the lowest seat answers a challenge before any later bot", () => {
  const store = new RoomStore();
  const host = store.create({ name: "Ann", socketId: "sock-0" });
  store.setReady("sock-0", true);
  store.start("sock-0", 1, 2);

  const room = store.peek(host.code);
  room.game.currentPlayerIndex = 2;
  room.game.declare(2, "Tax");

  assert.equal(nextBotSeat(room), null);
  store.act("sock-0", { type: "pass" });
  assert.equal(nextBotSeat(room), 1);
});

test("challenge reveals hold the table longer than an ordinary action", () => {
  assert.equal(delayBeforeBot([]), 900);
  assert.equal(delayBeforeBot([{ kind: "TurnStarted" }]), 1350);
  assert.equal(
    delayBeforeBot([
      { kind: "ChallengeDeclared" },
      { kind: "ChallengeWon" },
      { kind: "CardReplaced" },
      { kind: "InfluenceLost", reason: "LostChallenge" },
    ]),
    6500,
  );
  assert.equal(delayBeforeBot([{ kind: "InfluenceLost", reason: "FailedChallenge" }]), 2350);
});

test("the scheduler plays the bot's real choice on its turn", () => {
  mock.timers.enable({ apis: ["setTimeout"] });
  try {
    const store = new RoomStore();
    const host = store.create({ name: "Ann", socketId: "sock-0" });
    store.setReady("sock-0", true);
    store.start("sock-0", 1, 1);
    const income = store.act("sock-0", { type: "declare", action: "Income" });

    const room = store.peek(host.code);
    room.game.players[1].influence = ["Duke", "Ambassador"];
    room.players[1].personality = quiet;
    room.players[1].rng = () => 0;

    let played = null;
    scheduleBots(store, host.code, (result) => {
      played = result;
    }, income.events);
    mock.timers.tick(900);
    assert.equal(played, null);

    mock.timers.tick(delayBeforeBot(income.events) - 900);
    assert.equal(room.game.pendingAction, "Tax");
    assert.equal(played.events.some((event) => event.kind === "ActionDeclared"), true);
    assert.equal(nextBotSeat(room), null);
    assert.equal(botCommand(room.game, room.players[1]), null);
  } finally {
    mock.timers.reset();
  }
});
