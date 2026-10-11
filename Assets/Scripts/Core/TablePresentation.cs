using System.Collections.Generic;

namespace Coup.Core
{
    /// <summary>One seat as the server described it. Hidden is null for every seat except our own.</summary>
    public sealed class SeatPresentation
    {
        public string Name;
        public int Coins;
        public List<Role> Hidden;
        public int HiddenCount;
        public List<Role> Revealed;
    }

    /// <summary>A full table snapshot the client can draw without asking the server for hidden cards.</summary>
    public sealed class TablePresentation
    {
        public GamePhase Phase;
        public int CurrentPlayer;
        public int Turn = 1;
        public int DeckCount;
        public int Actor = -1;
        public ActionKind? Action;
        public int Target = -1;
        public Role? Claim;
        public int Blocker = -1;
        public Role? BlockClaim;
        public List<int> Waiting;
        public int Loser = -1;
        public LossReason? LossReason;
        public List<Role> ExchangePool;
        public int ExchangeKeep;
        public List<SeatPresentation> Seats;
    }
}
