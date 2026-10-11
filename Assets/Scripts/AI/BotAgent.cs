using System;
using System.Collections.Generic;
using System.Linq;
using Coup.Core;

namespace Coup.AI
{
    /// <summary>
    /// A Coup opponent that reasons about what others can plausibly hold, rather
    /// than flipping a coin. Challenge decisions come from card-counting (how many
    /// copies of a claimed role are still unseen) nudged by personality.
    /// </summary>
    public class BotAgent : IAgent
    {
        /// <summary>How much each role is worth keeping, used for discards and exchanges.</summary>
        private static readonly Dictionary<Role, int> RoleValue = new()
        {
            { Role.Duke, 5 },        // best economy in the game
            { Role.Captain, 4 },     // income plus theft defence
            { Role.Contessa, 3 },    // the only answer to an Assassin
            { Role.Assassin, 3 },
            { Role.Ambassador, 2 }
        };

        private readonly Random _rng;
        private readonly BotPersonality _p;

        public int Seat { get; }
        public BotPersonality Personality => _p;

        public BotAgent(int seat, BotPersonality personality, int seed = 0)
        {
            Seat = seat;
            _p = personality ?? BotPersonality.Opportunist;
            _rng = new Random(seed == 0 ? seat * 7919 + 13 : seed);
        }

        // ------------------------------------------------------------- turn action

        /// <summary>Turns after which a bot starts actively trying to end the game.</summary>
        private const int PatienceTurns = 40;

        public ActionChoice ChooseAction(GameView view)
        {
            var coins = view.MyCoins;
            var hand = view.MyInfluence;

            if (coins >= Rules.ForcedCoupThreshold)
                return new ActionChoice(ActionKind.Coup, PickTarget(view));

            // Two Captains can steal the same coins back and forth indefinitely, so
            // patience decays with the turn count: a dragging game pushes every bot
            // towards banking coins and ending it with a Coup, which nothing blocks.
            var impatience = System.Math.Min(1.0, (double)view.TurnNumber / PatienceTurns);
            var aggression = _p.Aggression + impatience * (1.0 - _p.Aggression);
            var hoarding = impatience > 0.5;

            // A Coup cannot be challenged or blocked, so it is the reliable way to
            // convert a coin lead into a kill.
            if (coins >= Rules.CoupCost && _rng.NextDouble() < aggression)
                return new ActionChoice(ActionKind.Coup, PickTarget(view));

            // Honest plays first: they cost nothing if challenged.
            if (hand.Contains(Role.Assassin) && coins >= Rules.AssassinateCost)
            {
                var victim = PickAssassinationTarget(view);
                if (victim >= 0) return new ActionChoice(ActionKind.Assassinate, victim);
            }

            if (hand.Contains(Role.Duke))
                return new ActionChoice(ActionKind.Tax);

            // Stealing trades coins around the table; while hoarding, build instead.
            if (hand.Contains(Role.Captain) && !hoarding)
            {
                var rich = RichestOpponent(view);
                if (rich >= 0 && view.Players[rich].Coins > 0)
                    return new ActionChoice(ActionKind.Steal, rich);
            }

            if (hand.Contains(Role.Ambassador) && HandIsWeak(hand) && !hoarding)
                return new ActionChoice(ActionKind.Exchange);

            // Nothing honest is attractive — consider a bluff.
            if (!hoarding && _rng.NextDouble() < _p.BluffChance)
            {
                var bluff = PickBluff(view);
                if (bluff.HasValue) return bluff.Value;
            }

            // Foreign Aid earns double Income but invites a Duke block.
            if (_rng.NextDouble() < 0.55 && LiveOpponents(view) <= 2)
                return new ActionChoice(ActionKind.ForeignAid);

            return new ActionChoice(ActionKind.Income);
        }

        private ActionChoice? PickBluff(GameView view)
        {
            var options = new List<ActionChoice>();

            // Claiming Duke is the classic low-risk bluff: no target, no grudge.
            if (!view.MyInfluence.Contains(Role.Duke))
                options.Add(new ActionChoice(ActionKind.Tax));

            if (!view.MyInfluence.Contains(Role.Captain))
            {
                var rich = RichestOpponent(view);
                if (rich >= 0 && view.Players[rich].Coins >= Rules.StealAmount)
                    options.Add(new ActionChoice(ActionKind.Steal, rich));
            }

            if (!view.MyInfluence.Contains(Role.Assassin) &&
                view.MyCoins >= Rules.AssassinateCost &&
                _p.Aggression > 0.6f)
            {
                var victim = PickAssassinationTarget(view);
                if (victim >= 0) options.Add(new ActionChoice(ActionKind.Assassinate, victim));
            }

            if (options.Count == 0) return null;
            return options[_rng.Next(options.Count)];
        }

        // --------------------------------------------------------------- challenge

        public bool ShouldChallenge(GameView view)
        {
            var isBlockChallenge = view.Phase == GamePhase.AwaitingBlockChallenge;
            var claimant = isBlockChallenge ? view.PendingBlockerIndex : view.ActingPlayerIndex;
            var claim = isBlockChallenge ? view.PendingBlockClaim : view.PendingClaim;

            if (!claim.HasValue || claimant < 0 || claimant == Seat) return false;

            var pHolds = view.ProbabilityHolds(claimant, claim.Value);

            // Every copy accounted for means the claim is certainly a bluff.
            if (pHolds <= 0.0) return true;

            // Base threshold: challenge when the claim is more likely false than true.
            var threshold = 0.40 + (_p.Suspicion - 0.5) * 0.35;

            // If this action would knock me out anyway, a challenge is nearly free.
            if (!isBlockChallenge &&
                view.PendingAction == ActionKind.Assassinate &&
                view.PendingTargetIndex == Seat &&
                view.Me.InfluenceCount == 1 &&
                !view.MyInfluence.Contains(Role.Contessa))
                threshold += 0.35;

            // Challenging costs a card; be stingier when down to the last one.
            if (view.Me.InfluenceCount == 1) threshold -= 0.15;

            return pHolds < threshold;
        }

        // ------------------------------------------------------------------- block

        public Role? ChooseBlock(GameView view)
        {
            var action = view.PendingAction;
            if (!action.HasValue) return null;

            var blockers = Rules.BlockingRoles(action.Value);
            if (blockers.Length == 0) return null;

            // Hold the real card? Use it.
            foreach (var role in blockers.OrderByDescending(r => RoleValue[r]))
                if (view.MyInfluence.Contains(role))
                    return role;

            // About to be assassinated off the table: claiming Contessa is the only
            // line that can survive, so take it even without the card.
            if (action.Value == ActionKind.Assassinate &&
                view.PendingTargetIndex == Seat &&
                view.Me.InfluenceCount == 1)
                return Role.Contessa;

            if (_rng.NextDouble() < _p.BlockBluffChance)
                return blockers[_rng.Next(blockers.Length)];

            return null;
        }

        // ------------------------------------------------------- losses & exchange

        public Role ChooseInfluenceToLose(GameView view)
        {
            // Give up the least useful card, but keep a Contessa when an Assassin is loose.
            return view.MyInfluence
                .OrderBy(r => RoleValue[r])
                .ThenBy(_ => _rng.Next())
                .First();
        }

        public IReadOnlyList<Role> ChooseExchangeKeep(GameView view, IReadOnlyList<Role> pool, int keep)
        {
            // Prefer high value, and break ties towards holding two different roles
            // so one lucky challenge cannot strip the whole hand.
            var ordered = pool.OrderByDescending(r => RoleValue[r]).ToList();
            var picked = new List<Role>();

            foreach (var role in ordered)
            {
                if (picked.Count >= keep) break;
                if (picked.Contains(role) && ordered.Count(r => !picked.Contains(r)) > keep - picked.Count)
                    continue;
                picked.Add(role);
            }

            // Top up if the diversity preference left us short.
            foreach (var role in ordered)
            {
                if (picked.Count >= keep) break;
                picked.Add(role);
            }

            return picked.Take(keep).ToList();
        }

        // ----------------------------------------------------------------- helpers

        private static int LiveOpponents(GameView view) => view.Opponents.Count();

        private static bool HandIsWeak(IReadOnlyList<Role> hand) =>
            hand.All(r => RoleValue[r] <= 2);

        /// <summary>The biggest threat: most influence first, then the deepest pockets.</summary>
        private int PickTarget(GameView view)
        {
            var best = view.Opponents
                .OrderByDescending(p => p.InfluenceCount)
                .ThenByDescending(p => p.Coins)
                .FirstOrDefault();
            return best?.Index ?? -1;
        }

        /// <summary>Prefer finishing someone off over chipping at a full hand.</summary>
        private int PickAssassinationTarget(GameView view)
        {
            var finishable = view.Opponents
                .Where(p => p.InfluenceCount == 1)
                .OrderByDescending(p => p.Coins)
                .FirstOrDefault();
            if (finishable != null) return finishable.Index;
            return PickTarget(view);
        }

        private int RichestOpponent(GameView view)
        {
            var best = view.Opponents.OrderByDescending(p => p.Coins).FirstOrDefault();
            return best?.Index ?? -1;
        }
    }
}
