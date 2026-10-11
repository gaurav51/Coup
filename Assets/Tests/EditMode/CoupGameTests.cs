using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Coup.Core;

namespace Coup.Core.Tests
{
    public class CoupGameTests
    {
        /// <summary>
        /// A game with everyone normalised to 2 coins. Two-player games otherwise give
        /// the starting player 1 coin, and that rule belongs in its own test rather
        /// than silently shifting every arithmetic assertion here.
        /// </summary>
        private static CoupGame NewGame(int players = 2)
        {
            var game = new CoupGame(Enumerable.Range(0, players).Select(i => $"P{i}").ToList(), seed: 1);
            foreach (var p in game.Players) p.Coins = Rules.StartingCoins;
            return game;
        }

        /// <summary>Forces an exact hand so each test states the case it is exercising.</summary>
        private static void SetHand(CoupGame game, int player, params Role[] roles)
        {
            var p = game.Players[player];
            p.ClearInfluence();
            foreach (var r in roles) p.GainCard(r);
        }

        private static void SetCoins(CoupGame game, int player, int coins) =>
            game.Players[player].Coins = coins;

        /// <summary>
        /// Passes on behalf of everyone the current window is waiting on, and stops
        /// there. Draining into the following window would hide which window a test
        /// is actually exercising.
        /// </summary>
        private static void PassAll(CoupGame game)
        {
            var window = game.Phase;
            while (game.Phase == window && game.PendingResponders.Count > 0)
            {
                var who = game.PendingResponders.First();
                switch (game.Phase)
                {
                    case GamePhase.AwaitingChallenge: game.PassChallenge(who); break;
                    case GamePhase.AwaitingBlock: game.PassBlock(who); break;
                    case GamePhase.AwaitingBlockChallenge: game.PassBlockChallenge(who); break;
                    default: return;
                }
            }
        }

        // ------------------------------------------------------------------ setup

        [Test]
        public void NewGame_DealsTwoCardsAndTwoCoinsEach()
        {
            var game = NewGame(4);
            Assert.That(game.Players.Count, Is.EqualTo(4));
            foreach (var p in game.Players)
            {
                Assert.That(p.InfluenceCount, Is.EqualTo(2), $"{p.Name} influence");
                Assert.That(p.Coins, Is.EqualTo(2), $"{p.Name} coins");
            }
            Assert.That(game.DeckCount, Is.EqualTo(15 - 8));
            Assert.That(game.Phase, Is.EqualTo(GamePhase.AwaitingAction));
        }

        // --------------------------------------------------------- simple actions

        [Test]
        public void Income_AddsOneCoinAndPassesTurn()
        {
            var game = NewGame();
            game.Declare(0, ActionKind.Income);

            Assert.That(game.Players[0].Coins, Is.EqualTo(3));
            Assert.That(game.CurrentPlayerIndex, Is.EqualTo(1));
            Assert.That(game.Phase, Is.EqualTo(GamePhase.AwaitingAction));
        }

        [Test]
        public void Income_CannotBeChallengedOrBlocked()
        {
            var game = NewGame(4);
            game.Declare(0, ActionKind.Income);
            // Resolves outright: no response window is ever opened.
            Assert.That(game.PendingResponders, Is.Empty);
        }

        [Test]
        public void TenCoins_ForcesCoup()
        {
            var game = NewGame();
            SetCoins(game, 0, 10);

            Assert.That(game.CanDeclare(0, ActionKind.Income), Is.False);
            Assert.That(game.CanDeclare(0, ActionKind.Tax), Is.False);
            Assert.That(game.CanDeclare(0, ActionKind.Coup, 1), Is.True);
        }

        [Test]
        public void Coup_CostsSevenAndCannotBeStopped()
        {
            var game = NewGame();
            SetCoins(game, 0, 7);
            SetHand(game, 1, Role.Duke, Role.Contessa);

            game.Declare(0, ActionKind.Coup, 1);

            Assert.That(game.Players[0].Coins, Is.EqualTo(0));
            // Straight to the forced reveal, with no challenge or block window.
            Assert.That(game.Phase, Is.EqualTo(GamePhase.AwaitingInfluenceLoss));
            Assert.That(game.PendingLoserIndex, Is.EqualTo(1));

            game.ChooseInfluenceToLose(1, Role.Duke);
            Assert.That(game.Players[1].InfluenceCount, Is.EqualTo(1));
            Assert.That(game.Players[1].Revealed, Does.Contain(Role.Duke));
        }

        // ------------------------------------------------------------- challenges

        [Test]
        public void Tax_Unchallenged_GivesThreeCoins()
        {
            var game = NewGame();
            game.Declare(0, ActionKind.Tax);
            Assert.That(game.Phase, Is.EqualTo(GamePhase.AwaitingChallenge));

            PassAll(game);
            Assert.That(game.Players[0].Coins, Is.EqualTo(5));
        }

        [Test]
        public void TruthfulClaim_Challenged_CostsTheChallengerAndPaysOut()
        {
            var game = NewGame();
            SetHand(game, 0, Role.Duke, Role.Captain);
            SetHand(game, 1, Role.Assassin, Role.Contessa);

            game.Declare(0, ActionKind.Tax);
            game.Challenge(1);

            // The challenger must give up a card for the bad call.
            Assert.That(game.Phase, Is.EqualTo(GamePhase.AwaitingInfluenceLoss));
            Assert.That(game.PendingLoserIndex, Is.EqualTo(1));
            game.ChooseInfluenceToLose(1, Role.Assassin);

            Assert.That(game.Players[1].InfluenceCount, Is.EqualTo(1));
            Assert.That(game.Players[0].Coins, Is.EqualTo(5), "Tax still resolves");
            // The proven Duke went back to the deck and a replacement was drawn.
            Assert.That(game.Players[0].InfluenceCount, Is.EqualTo(2));
        }

        [Test]
        public void BluffedClaim_Challenged_CostsTheBluffer_AndTheActionFails()
        {
            var game = NewGame();
            SetHand(game, 0, Role.Captain, Role.Contessa);
            SetHand(game, 1, Role.Duke, Role.Assassin);

            game.Declare(0, ActionKind.Tax);
            game.Challenge(1);

            Assert.That(game.PendingLoserIndex, Is.EqualTo(0));
            game.ChooseInfluenceToLose(0, Role.Captain);

            Assert.That(game.Players[0].Coins, Is.EqualTo(2), "no coins from a failed bluff");
            Assert.That(game.Players[0].InfluenceCount, Is.EqualTo(1));
            Assert.That(game.CurrentPlayerIndex, Is.EqualTo(1));
        }

        [Test]
        public void ProvenClaim_IsShuffledBackAndRedrawn()
        {
            var game = NewGame();
            SetHand(game, 0, Role.Duke, Role.Captain);
            SetHand(game, 1, Role.Assassin, Role.Contessa);

            var deckBefore = game.DeckCount;
            game.Declare(0, ActionKind.Tax);
            game.Challenge(1);
            game.ChooseInfluenceToLose(1, Role.Assassin);

            Assert.That(game.DeckCount, Is.EqualTo(deckBefore), "one card back, one card out");
            Assert.That(game.Events.Any(e => e.Kind == GameEventKind.CardReplaced));
        }

        // ----------------------------------------------------------------- blocks

        [Test]
        public void ForeignAid_BlockedByDuke_PaysNothing()
        {
            var game = NewGame();
            SetHand(game, 1, Role.Duke, Role.Contessa);

            game.Declare(0, ActionKind.ForeignAid);
            Assert.That(game.Phase, Is.EqualTo(GamePhase.AwaitingBlock));

            game.Block(1, Role.Duke);
            PassAll(game); // nobody challenges the block

            Assert.That(game.Players[0].Coins, Is.EqualTo(2));
            Assert.That(game.Events.Any(e => e.Kind == GameEventKind.ActionFailed));
        }

        [Test]
        public void ForeignAid_Unblocked_PaysTwo()
        {
            var game = NewGame();
            game.Declare(0, ActionKind.ForeignAid);
            PassAll(game);
            Assert.That(game.Players[0].Coins, Is.EqualTo(4));
        }

        [Test]
        public void BluffedBlock_Challenged_LetsTheActionThrough()
        {
            var game = NewGame();
            SetHand(game, 0, Role.Captain, Role.Captain);
            SetHand(game, 1, Role.Assassin, Role.Contessa); // no Duke to block with

            game.Declare(0, ActionKind.ForeignAid);
            game.Block(1, Role.Duke);
            Assert.That(game.Phase, Is.EqualTo(GamePhase.AwaitingBlockChallenge));

            game.ChallengeBlock(0);
            game.ChooseInfluenceToLose(1, Role.Assassin);

            Assert.That(game.Players[0].Coins, Is.EqualTo(4), "the broken block let Foreign Aid resolve");
            Assert.That(game.Players[1].InfluenceCount, Is.EqualTo(1));
        }

        [Test]
        public void TruthfulBlock_Challenged_CostsTheChallenger_AndTheBlockStands()
        {
            var game = NewGame();
            SetHand(game, 0, Role.Captain, Role.Captain);
            SetHand(game, 1, Role.Duke, Role.Contessa);

            game.Declare(0, ActionKind.ForeignAid);
            game.Block(1, Role.Duke);
            game.ChallengeBlock(0);

            Assert.That(game.PendingLoserIndex, Is.EqualTo(0));
            game.ChooseInfluenceToLose(0, Role.Captain);

            Assert.That(game.Players[0].Coins, Is.EqualTo(2), "block held");
            Assert.That(game.Players[0].InfluenceCount, Is.EqualTo(1));
        }

        // ------------------------------------------------------- steal and assassinate

        [Test]
        public void Steal_TakesTwoCoins()
        {
            var game = NewGame();
            SetCoins(game, 1, 5);

            game.Declare(0, ActionKind.Steal, 1);
            PassAll(game); // no challenge
            PassAll(game); // no block

            Assert.That(game.Players[0].Coins, Is.EqualTo(4));
            Assert.That(game.Players[1].Coins, Is.EqualTo(3));
        }

        [Test]
        public void Steal_TakesOnlyWhatTheTargetHas()
        {
            var game = NewGame();
            SetCoins(game, 1, 1);

            game.Declare(0, ActionKind.Steal, 1);
            PassAll(game);
            PassAll(game);

            Assert.That(game.Players[0].Coins, Is.EqualTo(3));
            Assert.That(game.Players[1].Coins, Is.EqualTo(0));
        }

        [Test]
        public void Steal_BlockableByCaptainOrAmbassador()
        {
            Assert.That(Rules.BlockingRoles(ActionKind.Steal),
                Is.EquivalentTo(new[] { Role.Captain, Role.Ambassador }));
        }

        [Test]
        public void Assassinate_SpendsThreeCoinsEvenWhenBlocked()
        {
            var game = NewGame();
            SetCoins(game, 0, 3);
            SetHand(game, 0, Role.Assassin, Role.Captain);
            SetHand(game, 1, Role.Contessa, Role.Duke);

            game.Declare(0, ActionKind.Assassinate, 1);
            Assert.That(game.Players[0].Coins, Is.EqualTo(0), "paid up front");

            PassAll(game);                 // no challenge to the Assassin claim
            game.Block(1, Role.Contessa);  // Contessa blocks
            PassAll(game);                 // nobody challenges the Contessa

            Assert.That(game.Players[0].Coins, Is.EqualTo(0), "coins are not refunded");
            Assert.That(game.Players[1].InfluenceCount, Is.EqualTo(2), "target survives");
        }

        [Test]
        public void TargetWhoWronglyChallengesAnAssassin_LosesBothCards()
        {
            var game = NewGame();
            SetCoins(game, 0, 3);
            SetHand(game, 0, Role.Assassin, Role.Captain);
            SetHand(game, 1, Role.Duke, Role.Ambassador); // no Contessa

            game.Declare(0, ActionKind.Assassinate, 1);
            game.Challenge(1);

            // First card lost for the bad challenge.
            game.ChooseInfluenceToLose(1, Role.Duke);
            Assert.That(game.Players[1].InfluenceCount, Is.EqualTo(1));

            // The assassination still has to be answered; with no Contessa, it lands.
            Assert.That(game.Phase, Is.EqualTo(GamePhase.AwaitingBlock));
            game.PassBlock(1);

            Assert.That(game.Players[1].IsAlive, Is.False);
            Assert.That(game.Phase, Is.EqualTo(GamePhase.GameOver));
            Assert.That(game.Winner.Index, Is.EqualTo(0));
        }

        // --------------------------------------------------------------- exchange

        [Test]
        public void Exchange_KeepsTheSameNumberOfCards()
        {
            var game = NewGame();
            SetHand(game, 0, Role.Ambassador, Role.Duke);

            game.Declare(0, ActionKind.Exchange);
            PassAll(game);

            Assert.That(game.Phase, Is.EqualTo(GamePhase.AwaitingExchangeSelection));
            Assert.That(game.ExchangeKeepCount, Is.EqualTo(2));

            var pool = game.ExchangePool();
            Assert.That(pool.Count, Is.EqualTo(4));

            var deckBefore = game.DeckCount;
            game.ChooseExchangeKeep(new List<Role> { pool[0], pool[1] });

            Assert.That(game.Players[0].InfluenceCount, Is.EqualTo(2));
            Assert.That(game.DeckCount, Is.EqualTo(deckBefore + 2), "the other two go back");
            Assert.That(game.Phase, Is.EqualTo(GamePhase.AwaitingAction));
        }

        [Test]
        public void Exchange_WithOneCardLeft_KeepsOnlyOne()
        {
            var game = NewGame();
            SetHand(game, 0, Role.Ambassador);

            game.Declare(0, ActionKind.Exchange);
            PassAll(game);

            Assert.That(game.ExchangeKeepCount, Is.EqualTo(1));
            Assert.That(game.ExchangePool().Count, Is.EqualTo(3));
        }

        // ------------------------------------------------------ elimination / win

        [Test]
        public void LosingBothInfluences_EliminatesThePlayer()
        {
            var game = NewGame(3);
            SetHand(game, 1, Role.Duke);  // already down to one card
            SetCoins(game, 0, 7);

            game.Declare(0, ActionKind.Coup, 1);

            Assert.That(game.Players[1].IsAlive, Is.False);
            Assert.That(game.Events.Any(e =>
                e.Kind == GameEventKind.PlayerEliminated && e.PlayerIndex == 1));
            Assert.That(game.Phase, Is.EqualTo(GamePhase.AwaitingAction));
            Assert.That(game.CurrentPlayerIndex, Is.EqualTo(2), "the dead seat is skipped");
        }

        [Test]
        public void LastPlayerStanding_WinsTheGame()
        {
            var game = NewGame();
            SetHand(game, 1, Role.Duke);
            SetCoins(game, 0, 7);

            game.Declare(0, ActionKind.Coup, 1);

            Assert.That(game.Phase, Is.EqualTo(GamePhase.GameOver));
            Assert.That(game.Winner, Is.Not.Null);
            Assert.That(game.Winner.Index, Is.EqualTo(0));
        }

        [Test]
        public void EliminatedPlayer_CannotBeTargeted()
        {
            var game = NewGame(3);
            SetHand(game, 1, Role.Duke);
            SetCoins(game, 0, 7);
            game.Declare(0, ActionKind.Coup, 1);

            Assert.That(game.CanDeclare(2, ActionKind.Steal, 1), Is.False);
        }

        // -------------------------------------------------------------- validation

        [Test]
        public void ActionsOutOfTurn_AreRejected()
        {
            var game = NewGame();
            Assert.That(game.CanDeclare(1, ActionKind.Income), Is.False);
            Assert.Throws<System.InvalidOperationException>(() => game.Declare(1, ActionKind.Income));
        }

        [Test]
        public void CannotCoupWithoutSevenCoins()
        {
            var game = NewGame();
            SetCoins(game, 0, 6);
            Assert.That(game.CanDeclare(0, ActionKind.Coup, 1), Is.False);
        }

        [Test]
        public void CannotAssassinateWithoutThreeCoins()
        {
            var game = NewGame();
            SetCoins(game, 0, 2);
            Assert.That(game.CanDeclare(0, ActionKind.Assassinate, 1), Is.False);
        }

        [Test]
        public void CannotTargetYourself()
        {
            var game = NewGame();
            SetCoins(game, 0, 7);
            Assert.That(game.CanDeclare(0, ActionKind.Coup, 0), Is.False);
        }

        [Test]
        public void CannotBlockWithARoleThatDoesNotBlockThatAction()
        {
            var game = NewGame();
            game.Declare(0, ActionKind.ForeignAid);
            Assert.Throws<System.InvalidOperationException>(() => game.Block(1, Role.Captain));
        }

        // --------------------------------------------------- official rulebook cases

        [Test]
        public void TwoPlayerGame_StartsTheFirstPlayerWithOneCoin()
        {
            var game = new CoupGame(new List<string> { "A", "B" }, seed: 1);
            Assert.That(game.Players[0].Coins, Is.EqualTo(1), "starting player");
            Assert.That(game.Players[1].Coins, Is.EqualTo(2), "second player");
        }

        [Test]
        public void ThreePlayerGame_StartsEveryoneWithTwoCoins()
        {
            var game = new CoupGame(new List<string> { "A", "B", "C" }, seed: 1);
            foreach (var p in game.Players)
                Assert.That(p.Coins, Is.EqualTo(2), p.Name);
        }

        [Test]
        public void BluffedAssassin_Challenged_RefundsTheThreeCoins()
        {
            // A successfully challenged action never happened, so its fee returns.
            var game = NewGame();
            SetCoins(game, 0, 5);
            SetHand(game, 0, Role.Captain, Role.Duke);   // no Assassin
            SetHand(game, 1, Role.Contessa, Role.Duke);

            game.Declare(0, ActionKind.Assassinate, 1);
            Assert.That(game.Players[0].Coins, Is.EqualTo(2), "fee paid up front");

            game.Challenge(1);
            game.ChooseInfluenceToLose(0, Role.Captain);

            Assert.That(game.Players[0].Coins, Is.EqualTo(5), "fee refunded on a caught bluff");
            Assert.That(game.Players[1].InfluenceCount, Is.EqualTo(2), "target untouched");
        }

        [Test]
        public void BlockedAssassination_DoesNotRefund()
        {
            // Contrast with a caught bluff: a blocked action was still taken.
            var game = NewGame();
            SetCoins(game, 0, 5);
            SetHand(game, 0, Role.Assassin, Role.Duke);
            SetHand(game, 1, Role.Contessa, Role.Duke);

            game.Declare(0, ActionKind.Assassinate, 1);
            PassAll(game);
            game.Block(1, Role.Contessa);
            PassAll(game);

            Assert.That(game.Players[0].Coins, Is.EqualTo(2), "fee stays spent when blocked");
        }

        [Test]
        public void TruthfulAssassin_Challenged_KeepsTheFeeSpent()
        {
            var game = NewGame(3);
            SetCoins(game, 0, 5);
            SetHand(game, 0, Role.Assassin, Role.Duke);
            SetHand(game, 1, Role.Captain, Role.Captain);

            game.Declare(0, ActionKind.Assassinate, 1);
            game.Challenge(2);
            game.ChooseInfluenceToLose(2, game.Players[2].Influence[0]);

            Assert.That(game.Players[0].Coins, Is.EqualTo(2), "a proven claim is not refunded");
        }

        [Test]
        public void EliminatedPlayer_ReturnsCoinsToTheTreasury()
        {
            var game = NewGame(3);
            SetHand(game, 1, Role.Duke);
            SetCoins(game, 1, 6);
            SetCoins(game, 0, 7);

            game.Declare(0, ActionKind.Coup, 1);

            Assert.That(game.Players[1].IsAlive, Is.False);
            Assert.That(game.Players[1].Coins, Is.Zero, "an exiled player hands back their coins");
        }

        [Test]
        public void StealTakesOneCoin_WhenTheTargetHasOnlyOne()
        {
            // "Take 2 coins from another player, or 1 if they have only one."
            var game = NewGame();
            SetCoins(game, 1, 1);

            game.Declare(0, ActionKind.Steal, 1);
            PassAll(game);
            PassAll(game);

            Assert.That(game.Players[0].Coins, Is.EqualTo(3));
            Assert.That(game.Players[1].Coins, Is.Zero);
        }

        [Test]
        public void AnyPlayerMayChallenge_NotJustTheTarget()
        {
            var game = NewGame(4);
            SetHand(game, 0, Role.Captain, Role.Duke);
            SetCoins(game, 0, 3);

            game.Declare(0, ActionKind.Steal, 1);
            // Seats 1, 2 and 3 are all invited to challenge, not only the target.
            Assert.That(game.PendingResponders, Is.EquivalentTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void StealTarget_MayBlockWhileTheClaimIsStillBeingChallenged()
        {
            var game = NewGame(4);
            game.Declare(0, ActionKind.Steal, 2);

            Assert.That(game.Phase, Is.EqualTo(GamePhase.AwaitingChallenge));
            Assert.That(game.MayBlock(2), Is.True, "the victim can choose to block during the challenge");
            Assert.That(game.MayBlock(1), Is.False, "a bystander cannot block a steal");
            Assert.That(game.MayBlock(0), Is.False, "the thief cannot block their own steal");
        }

        [Test]
        public void OnlyTheTargetMayBlockATargetedAction()
        {
            var game = NewGame(4);
            game.Declare(0, ActionKind.Steal, 2);
            PassAll(game); // clear the challenge window

            Assert.That(game.Phase, Is.EqualTo(GamePhase.AwaitingBlock));
            Assert.That(game.PendingResponders, Is.EquivalentTo(new[] { 2 }), "only the victim");
        }

        [Test]
        public void AnyPlayerMayBlockForeignAid()
        {
            var game = NewGame(4);
            game.Declare(0, ActionKind.ForeignAid);

            Assert.That(game.Phase, Is.EqualTo(GamePhase.AwaitingBlock));
            Assert.That(game.PendingResponders, Is.EquivalentTo(new[] { 1, 2, 3 }),
                "Foreign Aid harms the table, so anyone may claim Duke");
        }

        [Test]
        public void StealMayBeBlockedByCaptainOrAmbassador()
        {
            var game = NewGame();
            SetHand(game, 1, Role.Ambassador, Role.Duke);

            game.Declare(0, ActionKind.Steal, 1);
            PassAll(game);
            game.Block(1, Role.Ambassador);
            PassAll(game);

            Assert.That(game.Players[0].Coins, Is.EqualTo(2), "steal blocked by Ambassador");
        }

    }
}