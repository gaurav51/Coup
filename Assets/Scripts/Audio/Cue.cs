namespace Coup.Game
{
    /// <summary>
    /// Every sound the game can ask for. Callers name the moment, not the file, so a
    /// clip can be re-synthesised or replaced with a recording without touching the
    /// call sites.
    /// </summary>
    public enum Cue
    {
        None = 0,

        // Interface
        UiClick,
        UiBack,
        UiDenied,

        // Cards and coins
        CardDeal,
        CardFlip,
        CardSlam,
        Shuffle,
        CoinGain,
        CoinPay,

        // Turn order
        TurnYours,
        TurnOther,

        // Actions, one per ActionKind
        Income,
        ForeignAid,
        Tax,
        Steal,
        Assassinate,
        Exchange,
        Coup,

        // The bluffing layer
        Block,
        Challenge,
        ChallengeWon,
        ChallengeLost,

        // Losing ground
        InfluenceLost,
        Eliminated,

        // End of match
        Victory,
        Defeat
    }
}
