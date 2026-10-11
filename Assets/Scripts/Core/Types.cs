namespace Coup.Core
{
    /// <summary>The five influence cards. Three copies of each form the 15-card deck.</summary>
    public enum Role
    {
        Duke,
        Captain,
        Assassin,
        Contessa,
        Ambassador
    }

    public enum ActionKind
    {
        Income,
        ForeignAid,
        Coup,
        Tax,
        Steal,
        Assassinate,
        Exchange
    }

    public enum GamePhase
    {
        /// <summary>Current player must declare an action.</summary>
        AwaitingAction,

        /// <summary>Opponents may challenge the actor's claimed role.</summary>
        AwaitingChallenge,

        /// <summary>Eligible players may block the action.</summary>
        AwaitingBlock,

        /// <summary>Players may challenge the blocker's claimed role.</summary>
        AwaitingBlockChallenge,

        /// <summary>A player must choose which influence card to reveal and lose.</summary>
        AwaitingInfluenceLoss,

        /// <summary>An Ambassador must choose which cards to keep after drawing.</summary>
        AwaitingExchangeSelection,

        GameOver
    }

    /// <summary>Why the engine is asking a player to give up an influence card.</summary>
    public enum LossReason
    {
        FailedChallenge,
        LostChallenge,
        Assassinated,
        Couped
    }
}
