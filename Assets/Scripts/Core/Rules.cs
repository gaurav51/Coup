using System;

namespace Coup.Core
{
    /// <summary>
    /// Static rules data for standard Coup. Everything here is pure lookup so the
    /// engine, the AI and the UI all agree on costs, claims and block rights.
    /// </summary>
    public static class Rules
    {
        public const int StartingCoins = 2;

        /// <summary>The starting player in a two-player game begins with one coin.</summary>
        public const int TwoPlayerStartingCoins = 1;
        public const int StartingInfluence = 2;
        public const int CopiesPerRole = 3;
        public const int ForcedCoupThreshold = 10;
        public const int CoupCost = 7;
        public const int AssassinateCost = 3;
        public const int StealAmount = 2;
        public const int TaxAmount = 3;
        public const int IncomeAmount = 1;
        public const int ForeignAidAmount = 2;
        public const int ExchangeDrawCount = 2;

        public static readonly Role[] AllRoles =
        {
            Role.Duke, Role.Captain, Role.Assassin, Role.Contessa, Role.Ambassador
        };

        /// <summary>Coins that must be paid up front to declare the action.</summary>
        public static int CoinCost(ActionKind kind) => kind switch
        {
            ActionKind.Coup => CoupCost,
            ActionKind.Assassinate => AssassinateCost,
            _ => 0
        };

        /// <summary>The role the actor implicitly claims, or null if the action claims nothing.</summary>
        public static Role? ClaimedRole(ActionKind kind) => kind switch
        {
            ActionKind.Tax => Role.Duke,
            ActionKind.Steal => Role.Captain,
            ActionKind.Assassinate => Role.Assassin,
            ActionKind.Exchange => Role.Ambassador,
            _ => null
        };

        /// <summary>Actions that claim a role can be challenged; Income, Foreign Aid and Coup cannot.</summary>
        public static bool IsChallengeable(ActionKind kind) => ClaimedRole(kind).HasValue;

        public static bool RequiresTarget(ActionKind kind) =>
            kind is ActionKind.Coup or ActionKind.Steal or ActionKind.Assassinate;

        /// <summary>Roles that may be claimed to block this action. Empty when unblockable.</summary>
        public static Role[] BlockingRoles(ActionKind kind) => kind switch
        {
            ActionKind.ForeignAid => new[] { Role.Duke },
            ActionKind.Assassinate => new[] { Role.Contessa },
            ActionKind.Steal => new[] { Role.Captain, Role.Ambassador },
            _ => Array.Empty<Role>()
        };

        public static bool IsBlockable(ActionKind kind) => BlockingRoles(kind).Length > 0;

        /// <summary>
        /// Foreign Aid harms the whole table, so anyone may block it. Targeted actions
        /// may only be blocked by the player being targeted.
        /// </summary>
        public static bool OnlyTargetMayBlock(ActionKind kind) =>
            kind is ActionKind.Assassinate or ActionKind.Steal;

        public static bool CanBlockWith(ActionKind kind, Role role)
        {
            foreach (var r in BlockingRoles(kind))
                if (r == role) return true;
            return false;
        }
    }
}
