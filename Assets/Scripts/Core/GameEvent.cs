namespace Coup.Core
{
    public enum GameEventKind
    {
        GameStarted,
        TurnStarted,
        ActionDeclared,
        ChallengeDeclared,
        ChallengeWon,      // the claim was genuine; the challenger pays
        ChallengeLost,     // the claim was a bluff; the claimant pays
        CardReplaced,      // a proven claim is shuffled back and redrawn
        BlockDeclared,
        ActionResolved,
        ActionFailed,
        CoinsChanged,
        InfluenceLost,
        PlayerEliminated,
        ExchangeDrawn,
        ExchangeCompleted,
        GameOver
    }

    /// <summary>
    /// A record of something the rules engine did. The presentation layer animates
    /// from this stream, which keeps animation entirely out of the rules.
    /// </summary>
    public class GameEvent
    {
        public GameEventKind Kind { get; set; }
        public int PlayerIndex { get; set; } = -1;
        public int TargetIndex { get; set; } = -1;
        public ActionKind? Action { get; set; }
        public Role? Role { get; set; }
        public int Amount { get; set; }
        public LossReason? Reason { get; set; }

        public override string ToString()
        {
            var s = Kind.ToString();
            if (PlayerIndex >= 0) s += $" p{PlayerIndex}";
            if (TargetIndex >= 0) s += $" -> p{TargetIndex}";
            if (Action.HasValue) s += $" [{Action}]";
            if (Role.HasValue) s += $" ({Role})";
            if (Amount != 0) s += $" {Amount:+#;-#;0}";
            return s;
        }
    }
}
