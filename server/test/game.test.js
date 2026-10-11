import assert from "node:assert/strict";
import test from "node:test";
import { CoupGame } from "../src/game.js";
import { ROLES } from "../src/rules.js";

function newGame(players = 2) {
  const game = new CoupGame(
    Array.from({ length: players }, (_, index) => `P${index}`),
    1,
  );
  for (const player of game.players) player.coins = 2;
  return game;
}

function setHand(game, seat, ...roles) {
  game.players[seat].influence = [...roles];
}

function setCoins(game, seat, coins) {
  game.players[seat].coins = coins;
}

function passWindow(game) {
  const phase = game.phase;
  let guard = 12;
  while (game.phase === phase && game.responders.size > 0 && guard > 0) {
    guard -= 1;
    const who = [...game.responders][0];
    if (phase === "AwaitingChallenge") game.passChallenge(who);
    else if (phase === "AwaitingBlock") game.passBlock(who);
    else if (phase === "AwaitingBlockChallenge") game.passBlockChallenge(who);
    else return;
  }
}

test("a new game deals two cards and two coins", () => {
  const game = newGame(4);
  assert.equal(game.players.length, 4);
  for (const player of game.players) {
    assert.equal(player.influence.length, 2);
    assert.equal(player.coins, 2);
  }
  assert.equal(game.deckCount, 7);
  assert.equal(game.phase, "AwaitingAction");
});

test("two-player games start the first seat on one coin", () => {
  const game = new CoupGame(["A", "B"], 1);
  assert.equal(game.players[0].coins, 1);
  assert.equal(game.players[1].coins, 2);
});

test("income adds one coin and cannot be stopped", () => {
  const game = newGame();
  game.declare(0, "Income");
  assert.equal(game.players[0].coins, 3);
  assert.equal(game.currentPlayerIndex, 1);
  assert.equal(game.phase, "AwaitingAction");
  assert.equal(game.responders.size, 0);
});

test("ten coins forces a coup", () => {
  const game = newGame();
  setCoins(game, 0, 10);
  assert.equal(game.canDeclare(0, "Income"), false);
  assert.equal(game.canDeclare(0, "Tax"), false);
  assert.equal(game.canDeclare(0, "Coup", 1), true);
});

test("coup costs seven and cannot be blocked or challenged", () => {
  const game = newGame();
  setCoins(game, 0, 7);
  setHand(game, 1, "Duke", "Contessa");
  game.declare(0, "Coup", 1);
  assert.equal(game.players[0].coins, 0);
  assert.equal(game.phase, "AwaitingInfluenceLoss");
  assert.equal(game.pendingLoserIndex, 1);
  game.chooseInfluenceToLose(1, "Duke");
  assert.equal(game.players[1].influence.length, 1);
  assert.deepEqual(game.players[1].revealed, ["Duke"]);
});

test("unchallenged tax pays three", () => {
  const game = newGame();
  game.declare(0, "Tax");
  assert.equal(game.phase, "AwaitingChallenge");
  passWindow(game);
  assert.equal(game.players[0].coins, 5);
});

test("a truthful claim costs the challenger and still pays", () => {
  const game = newGame();
  setHand(game, 0, "Duke", "Captain");
  setHand(game, 1, "Assassin", "Contessa");
  const deckBefore = game.deckCount;
  game.declare(0, "Tax");
  game.challenge(1);
  assert.equal(game.pendingLoserIndex, 1);
  game.chooseInfluenceToLose(1, "Assassin");
  assert.equal(game.players[1].influence.length, 1);
  assert.equal(game.players[0].coins, 5);
  assert.equal(game.players[0].influence.length, 2);
  assert.equal(game.deckCount, deckBefore);
  assert.ok(game.events.some((event) => event.kind === "CardReplaced"));
});

test("a caught bluff refunds nothing and the action fails", () => {
  const game = newGame();
  setHand(game, 0, "Captain", "Contessa");
  setHand(game, 1, "Duke", "Assassin");
  game.declare(0, "Tax");
  game.challenge(1);
  assert.equal(game.pendingLoserIndex, 0);
  game.chooseInfluenceToLose(0, "Captain");
  assert.equal(game.players[0].coins, 2);
  assert.equal(game.players[0].influence.length, 1);
  assert.equal(game.currentPlayerIndex, 1);
});

test("a caught assassin bluff refunds the three coins", () => {
  const game = newGame();
  setCoins(game, 0, 5);
  setHand(game, 0, "Captain", "Duke");
  setHand(game, 1, "Contessa", "Duke");
  game.declare(0, "Assassinate", 1);
  assert.equal(game.players[0].coins, 2);
  game.challenge(1);
  game.chooseInfluenceToLose(0, "Captain");
  assert.equal(game.players[0].coins, 5);
  assert.equal(game.players[1].influence.length, 2);
});

test("a proven assassin keeps the fee spent", () => {
  const game = newGame(3);
  setCoins(game, 0, 5);
  setHand(game, 0, "Assassin", "Duke");
  setHand(game, 1, "Captain", "Captain");
  game.declare(0, "Assassinate", 1);
  game.challenge(2);
  game.chooseInfluenceToLose(2, game.players[2].influence[0]);
  assert.equal(game.players[0].coins, 2);
});

test("foreign aid blocked by a duke pays nothing", () => {
  const game = newGame();
  game.declare(0, "ForeignAid");
  assert.equal(game.phase, "AwaitingBlock");
  game.block(1, "Duke");
  passWindow(game);
  assert.equal(game.players[0].coins, 2);
  assert.ok(game.events.some((event) => event.kind === "ActionFailed"));
});

test("unblocked foreign aid pays two", () => {
  const game = newGame();
  game.declare(0, "ForeignAid");
  passWindow(game);
  assert.equal(game.players[0].coins, 4);
});

test("a bluffed block lets the action through", () => {
  const game = newGame();
  setHand(game, 1, "Assassin", "Contessa");
  game.declare(0, "ForeignAid");
  game.block(1, "Duke");
  game.challengeBlock(0);
  game.chooseInfluenceToLose(1, "Assassin");
  assert.equal(game.players[0].coins, 4);
  assert.equal(game.players[1].influence.length, 1);
});

test("a truthful block stands and costs the challenger", () => {
  const game = newGame();
  setHand(game, 0, "Captain", "Captain");
  setHand(game, 1, "Duke", "Contessa");
  game.declare(0, "ForeignAid");
  game.block(1, "Duke");
  game.challengeBlock(0);
  assert.equal(game.pendingLoserIndex, 0);
  game.chooseInfluenceToLose(0, "Captain");
  assert.equal(game.players[0].coins, 2);
  assert.equal(game.players[0].influence.length, 1);
});

test("steal takes two coins, or one when that is all the target has", () => {
  const rich = newGame();
  setCoins(rich, 1, 5);
  rich.declare(0, "Steal", 1);
  passWindow(rich);
  passWindow(rich);
  assert.equal(rich.players[0].coins, 4);
  assert.equal(rich.players[1].coins, 3);

  const poor = newGame();
  setCoins(poor, 1, 1);
  poor.declare(0, "Steal", 1);
  passWindow(poor);
  passWindow(poor);
  assert.equal(poor.players[0].coins, 3);
  assert.equal(poor.players[1].coins, 0);
});

test("only the steal target may block, and any other player may challenge", () => {
  const game = newGame(4);
  game.declare(0, "Steal", 2);
  assert.equal(game.phase, "AwaitingChallenge");
  assert.deepEqual([...game.responders].sort(), [1, 2, 3]);
  assert.equal(game.mayBlock(2), true);
  assert.equal(game.mayBlock(1), false);
  assert.equal(game.mayBlock(0), false);
  passWindow(game);
  assert.equal(game.phase, "AwaitingBlock");
  assert.deepEqual([...game.responders], [2]);
});

test("an ambassador can block a steal", () => {
  const game = newGame();
  game.declare(0, "Steal", 1);
  passWindow(game);
  game.block(1, "Ambassador");
  passWindow(game);
  assert.equal(game.players[0].coins, 2);
});

test("assassination spends three even when the contessa blocks", () => {
  const game = newGame();
  setCoins(game, 0, 3);
  setHand(game, 0, "Assassin", "Captain");
  setHand(game, 1, "Contessa", "Duke");
  game.declare(0, "Assassinate", 1);
  assert.equal(game.players[0].coins, 0);
  passWindow(game);
  game.block(1, "Contessa");
  passWindow(game);
  assert.equal(game.players[0].coins, 0);
  assert.equal(game.players[1].influence.length, 2);
});

test("the target who wrongly challenges an assassin loses both cards", () => {
  const game = newGame();
  setCoins(game, 0, 3);
  setHand(game, 0, "Assassin", "Captain");
  setHand(game, 1, "Duke", "Ambassador");
  game.declare(0, "Assassinate", 1);
  game.challenge(1);
  game.chooseInfluenceToLose(1, "Duke");
  assert.equal(game.players[1].influence.length, 1);
  assert.equal(game.phase, "AwaitingBlock");
  game.passBlock(1);
  assert.equal(game.players[1].isAlive, false);
  assert.equal(game.phase, "GameOver");
  assert.equal(game.winnerIndex, 0);
});

test("exchange keeps the same number of cards and returns the rest", () => {
  const game = newGame();
  setHand(game, 0, "Ambassador", "Duke");
  game.declare(0, "Exchange");
  passWindow(game);
  assert.equal(game.phase, "AwaitingExchangeSelection");
  assert.equal(game.exchangeKeepCount, 2);
  const pool = game.exchangePool();
  assert.equal(pool.length, 4);
  const deckBefore = game.deckCount;
  game.chooseExchangeKeep([pool[0], pool[1]]);
  assert.equal(game.players[0].influence.length, 2);
  assert.equal(game.deckCount, deckBefore + 2);
  assert.equal(game.phase, "AwaitingAction");
});

test("exchange with one card left keeps only one", () => {
  const game = newGame();
  setHand(game, 0, "Ambassador");
  game.declare(0, "Exchange");
  passWindow(game);
  assert.equal(game.exchangeKeepCount, 1);
  assert.equal(game.exchangePool().length, 3);
});

test("losing the last influence eliminates that seat and skips them", () => {
  const game = newGame(3);
  setHand(game, 1, "Duke");
  setCoins(game, 0, 7);
  setCoins(game, 1, 6);
  game.declare(0, "Coup", 1);
  assert.equal(game.players[1].isAlive, false);
  assert.equal(game.players[1].coins, 0);
  assert.equal(game.phase, "AwaitingAction");
  assert.equal(game.currentPlayerIndex, 2);
  assert.equal(game.canDeclare(2, "Steal", 1), false);
  assert.ok(game.events.some((event) => event.kind === "PlayerEliminated" && event.player === 1));
});

test("the last player standing wins", () => {
  const game = newGame();
  setHand(game, 1, "Duke");
  setCoins(game, 0, 7);
  game.declare(0, "Coup", 1);
  assert.equal(game.phase, "GameOver");
  assert.equal(game.winnerIndex, 0);
});

test("illegal declarations are rejected", () => {
  const game = newGame();
  assert.throws(() => game.declare(1, "Income"), /Not your turn/);
  setCoins(game, 0, 6);
  assert.equal(game.canDeclare(0, "Coup", 1), false);
  setCoins(game, 0, 2);
  assert.equal(game.canDeclare(0, "Assassinate", 1), false);
  setCoins(game, 0, 7);
  assert.equal(game.canDeclare(0, "Coup", 0), false);
  game.declare(0, "ForeignAid");
  assert.throws(() => game.block(1, "Captain"), /cannot block/);
});

test("public events do not name the card drawn as a replacement", () => {
  const game = newGame();
  setHand(game, 0, "Duke", "Captain");
  const before = [...game.players[0].influence];
  game.declare(0, "Tax");
  game.challenge(1);
  game.chooseInfluenceToLose(1, game.players[1].influence[0]);
  const replaced = game.events.find((event) => event.kind === "CardReplaced");
  assert.equal(replaced.role, "Duke");
  assert.deepEqual(before.includes("Duke"), true);
  assert.equal(Object.hasOwn(replaced, "hand"), false);
});

test("income games conserve the fifteen-card deck and end with one winner", () => {
  const game = new CoupGame(["A", "B", "C", "D"], 7);
  let guard = 2000;
  while (game.phase !== "GameOver" && guard > 0) {
    guard -= 1;
    step(game);
  }
  assert.equal(game.phase, "GameOver");
  assert.notEqual(game.winnerIndex, null);
  for (const role of ROLES) assert.equal(game.copiesOf(role), 3, role);
});

function step(game) {
  switch (game.phase) {
    case "AwaitingAction": {
      const legal = game.legalActions(game.currentPlayerIndex);
      const choice = legal.find((item) => item.action === "Income") ?? legal[0];
      if (!choice) throw new Error("no legal action");
      game.declare(game.currentPlayerIndex, choice.action, choice.target ?? -1);
      return;
    }
    case "AwaitingChallenge":
      game.passChallenge([...game.responders][0]);
      return;
    case "AwaitingBlock":
      game.passBlock([...game.responders][0]);
      return;
    case "AwaitingBlockChallenge":
      game.passBlockChallenge([...game.responders][0]);
      return;
    case "AwaitingInfluenceLoss": {
      const seat = game.pendingLoserIndex;
      game.chooseInfluenceToLose(seat, game.players[seat].influence[0]);
      return;
    }
    case "AwaitingExchangeSelection":
      game.chooseExchangeKeep(game.exchangePool().slice(0, game.exchangeKeepCount));
      return;
    default:
      throw new Error(`unexpected phase ${game.phase}`);
  }
}
