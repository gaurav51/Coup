using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Coup.Core;
using Coup.AI;

namespace Coup.Core.Tests
{
    /// <summary>
    /// Plays complete bot-vs-bot games and asserts the rules hold throughout.
    /// Random play reaches interleavings that hand-written cases never would.
    /// </summary>
    public class AiFuzzTests
    {
        private const int TotalCards = Rules.CopiesPerRole * 5;

        private static GameDriver BuildGame(int seed, int players = 4)
        {
            var names = Enumerable.Range(0, players).Select(i => $"P{i}").ToList();
            var game = new CoupGame(names, seed);

            var presets = BotPersonality.Presets;
            var agents = Enumerable.Range(0, players)
                .Select(i => (IAgent)new BotAgent(i, presets[i % presets.Length], seed * 31 + i))
                .ToList();

            return new GameDriver(game, agents);
        }

        /// <summary>
        /// All 15 cards must always be somewhere: a hand, a reveal pile, the deck, or
        /// the Ambassador's in-flight draw while an exchange is being decided.
        /// </summary>
        private static void AssertCardsConserved(CoupGame game, string where)
        {
            var inHands = game.Players.Sum(p => p.InfluenceCount);
            var revealed = game.Players.Sum(p => p.Revealed.Count);
            var inFlight = game.ExchangeDrawn.Count;
            Assert.That(inHands + revealed + game.DeckCount + inFlight, Is.EqualTo(TotalCards),
                $"card count drifted {where}");
            foreach (var role in Rules.AllRoles)
                Assert.That(game.CopiesOf(role), Is.EqualTo(Rules.CopiesPerRole),
                    $"{role} is not limited to {Rules.CopiesPerRole} copies {where}");
        }

        private static void AssertStateSane(CoupGame game, string where)
        {
            AssertCardsConserved(game, where);
            foreach (var p in game.Players)
            {
                Assert.That(p.Coins, Is.GreaterThanOrEqualTo(0), $"{p.Name} coins negative {where}");
                Assert.That(p.InfluenceCount, Is.InRange(0, Rules.StartingInfluence),
                    $"{p.Name} influence out of range {where}");
            }
        }

        [Test]
        public void BotGames_AlwaysFinishWithExactlyOneWinner([Range(1, 60)] int seed)
        {
            var driver = BuildGame(seed);
            var game = driver.Game;

            var steps = 0;
            while (game.Phase != GamePhase.GameOver)
            {
                Assert.That(++steps, Is.LessThan(5000), "game failed to terminate");
                driver.Step();
                AssertStateSane(game, $"seed {seed} step {steps}");
            }

            Assert.That(game.AlivePlayers.Count(), Is.EqualTo(1), $"seed {seed}");
            Assert.That(game.Winner, Is.Not.Null);
            Assert.That(driver.IllegalProposals, Is.Zero, $"bot proposed an illegal action (seed {seed})");
        }

        [Test]
        public void BotGames_RunCleanAcrossManySeedsAndTableSizes()
        {
            for (var players = 2; players <= 6; players++)
            {
                for (var seed = 1; seed <= 40; seed++)
                {
                    var driver = BuildGame(seed, players);
                    var status = driver.RunUntilHumanOrEnd();

                    Assert.That(status, Is.EqualTo(DriverStatus.GameOver),
                        $"{players}p seed {seed}");
                    Assert.That(driver.Game.AlivePlayers.Count(), Is.EqualTo(1),
                        $"{players}p seed {seed}");
                    Assert.That(driver.IllegalProposals, Is.Zero,
                        $"{players}p seed {seed}");
                    AssertCardsConserved(driver.Game, $"end of {players}p seed {seed}");
                }
            }
        }

        [Test]
        public void BotsBothBluffAndChallenge()
        {
            var challenges = 0;
            var blocks = 0;
            var bluffsCaught = 0;

            for (var seed = 1; seed <= 40; seed++)
            {
                var driver = BuildGame(seed);
                driver.RunUntilHumanOrEnd();

                challenges += driver.Game.Events.Count(e => e.Kind == GameEventKind.ChallengeDeclared);
                blocks += driver.Game.Events.Count(e => e.Kind == GameEventKind.BlockDeclared);
                bluffsCaught += driver.Game.Events.Count(e => e.Kind == GameEventKind.ChallengeLost);
            }

            // Without these the game is just Income roulette.
            Assert.That(challenges, Is.GreaterThan(0), "bots never challenged");
            Assert.That(blocks, Is.GreaterThan(0), "bots never blocked");
            Assert.That(bluffsCaught, Is.GreaterThan(0), "no bluff was ever caught");
        }

        [Test]
        public void DriverStops_WhenASeatHasNoAgent()
        {
            var names = new List<string> { "Human", "Bot1", "Bot2", "Bot3" };
            var game = new CoupGame(names, seed: 5);
            var agents = Enumerable.Range(1, 3)
                .Select(i => (IAgent)new BotAgent(i, BotPersonality.Opportunist, i))
                .ToList();

            var driver = new GameDriver(game, agents);
            var status = driver.RunUntilHumanOrEnd();

            Assert.That(status, Is.EqualTo(DriverStatus.WaitingForHuman));
            Assert.That(driver.SeatToAct(), Is.EqualTo(0), "seat 0 is the human");
        }

        [Test]
        public void AgentViewHidesOpponentHands()
        {
            var game = new CoupGame(new List<string> { "A", "B" }, seed: 3);
            var view = game.ViewFor(0);

            Assert.That(view.MyInfluence.Count, Is.EqualTo(2));
            // The opponent is visible only as a count, never as cards.
            var opponent = view.Players[1];
            Assert.That(opponent.InfluenceCount, Is.EqualTo(2));
            Assert.That(opponent.Revealed, Is.Empty);
            Assert.That(typeof(PlayerView).GetProperty("Influence"), Is.Null,
                "PlayerView must not expose hidden cards");
        }

        [Test]
        public void ProbabilityHolds_IsCertainWhenAllCopiesAreAccountedFor()
        {
            var game = new CoupGame(new List<string> { "A", "B" }, seed: 3);
            var p0 = game.Players[0];
            p0.ClearInfluence();
            p0.GainCard(Role.Duke);
            p0.GainCard(Role.Duke);

            var view = game.ViewFor(0);
            Assert.That(view.ProbabilityHolds(0, Role.Duke), Is.EqualTo(1.0));

            // One Duke left among the unseen cards, so the opponent might hold it.
            var pOpponentDuke = view.ProbabilityHolds(1, Role.Duke);
            Assert.That(pOpponentDuke, Is.GreaterThan(0.0).And.LessThan(1.0));
        }

        [Test]
        public void MutualCaptains_DoNotStealForever()
        {
            // Two Captains can steal the same coins back and forth indefinitely.
            // Impatience must break the loop and drive the game to a Coup.
            for (var seed = 1; seed <= 25; seed++)
            {
                var game = new CoupGame(new List<string> { "A", "B" }, seed);
                foreach (var p in game.Players)
                {
                    p.ClearInfluence();
                    p.GainCard(Role.Captain);
                    p.GainCard(Role.Captain);
                }

                var agents = new List<IAgent>
                {
                    new BotAgent(0, BotPersonality.Cautious, seed),
                    new BotAgent(1, BotPersonality.Cautious, seed + 500)
                };

                var driver = new GameDriver(game, agents);
                Assert.DoesNotThrow(() => driver.RunUntilHumanOrEnd(2000),
                    $"stalemate not broken (seed {seed})");
                Assert.That(game.Phase, Is.EqualTo(GamePhase.GameOver), $"seed {seed}");
            }
        }

        [Test]
        public void GamesFinishInAReasonableNumberOfTurns()
        {
            var longest = 0;
            for (var seed = 1; seed <= 60; seed++)
            {
                var driver = BuildGame(seed);
                driver.RunUntilHumanOrEnd();
                longest = System.Math.Max(longest, driver.Game.TurnNumber);
            }

            // A Coup match that runs past a hundred turns is a broken match, not a long one.
            Assert.That(longest, Is.LessThan(120), $"longest game took {longest} turns");
        }

    }
}