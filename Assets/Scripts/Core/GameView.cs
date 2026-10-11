using System.Collections.Generic;
using System.Linq;

namespace Coup.Core
{
    /// <summary>What one seat may legally know about another player.</summary>
    public class PlayerView
    {
        public int Index { get; }
        public string Name { get; }
        public int Coins { get; }
        public int InfluenceCount { get; }
        public IReadOnlyList<Role> Revealed { get; }
        public bool IsAlive => InfluenceCount > 0;

        public PlayerView(PlayerState p)
        {
            Index = p.Index;
            Name = p.Name;
            Coins = p.Coins;
            InfluenceCount = p.InfluenceCount;
            Revealed = p.Revealed.ToList();
        }
    }

    /// <summary>
    /// The game as a single seat sees it: own hand, public board, nothing else.
    /// Agents take only this, so an AI cannot accidentally read hidden hands — and
    /// the same boundary is what a server would send over the wire.
    /// </summary>
    public class GameView
    {
        public int SelfIndex { get; }
        public IReadOnlyList<Role> MyInfluence { get; }
        public IReadOnlyList<PlayerView> Players { get; }
        public int DeckCount { get; }
        public GamePhase Phase { get; }
        public int CurrentPlayerIndex { get; }
        public int TurnNumber { get; }

        public int ActingPlayerIndex { get; }
        public ActionKind? PendingAction { get; }
        public int PendingTargetIndex { get; }
        public Role? PendingClaim { get; }
        public int PendingBlockerIndex { get; }
        public Role? PendingBlockClaim { get; }

        public PlayerView Me => Players[SelfIndex];
        public int MyCoins => Me.Coins;

        public IEnumerable<PlayerView> Opponents =>
            Players.Where(p => p.Index != SelfIndex && p.IsAlive);

        /// <summary>Every card this seat can see: its own hand plus all revealed cards.</summary>
        public IReadOnlyList<Role> KnownCards { get; }

        public GameView(CoupGame game, int seat)
        {
            SelfIndex = seat;
            MyInfluence = game.Players[seat].Influence.ToList();
            Players = game.Players.Select(p => new PlayerView(p)).ToList();
            DeckCount = game.DeckCount;
            Phase = game.Phase;
            CurrentPlayerIndex = game.CurrentPlayerIndex;
            TurnNumber = game.TurnNumber;

            ActingPlayerIndex = game.ActingPlayerIndex;
            PendingAction = game.PendingActionKind;
            PendingTargetIndex = game.PendingTargetIndex;
            PendingClaim = game.PendingClaim;
            PendingBlockerIndex = game.PendingBlockerIndex;
            PendingBlockClaim = game.PendingBlockClaim;

            var known = new List<Role>(MyInfluence);
            foreach (var p in game.Players) known.AddRange(p.Revealed);
            KnownCards = known;
        }

        /// <summary>Copies of a role that could still be in someone's hand or the deck.</summary>
        public int UnseenCopies(Role role) =>
            Rules.CopiesPerRole - KnownCards.Count(c => c == role);

        /// <summary>Cards whose identity this seat cannot determine.</summary>
        public int UnseenCardCount =>
            Rules.CopiesPerRole * Rules.AllRoles.Length - KnownCards.Count;

        /// <summary>
        /// Probability that a player holds at least one copy of a role, from this
        /// seat's information. Drives whether a challenge is worth the risk.
        /// </summary>
        public double ProbabilityHolds(int playerIndex, Role role)
        {
            if (playerIndex == SelfIndex)
                return MyInfluence.Contains(role) ? 1.0 : 0.0;

            var hand = Players[playerIndex].InfluenceCount;
            if (hand <= 0) return 0.0;

            var unseen = UnseenCardCount;
            var copies = UnseenCopies(role);
            if (copies <= 0) return 0.0;
            if (unseen <= 0) return 0.0;
            if (unseen - copies < hand) return 1.0;

            // P(at least one) = 1 - P(hand drawn entirely from the non-matching cards)
            var pNone = 1.0;
            for (var i = 0; i < hand; i++)
                pNone *= (double)(unseen - copies - i) / (unseen - i);

            return 1.0 - pNone;
        }
    }
}
