using System.Collections.Generic;

namespace Coup.Core
{
    public readonly struct ActionChoice
    {
        public ActionKind Kind { get; }
        public int Target { get; }

        public ActionChoice(ActionKind kind, int target = -1)
        {
            Kind = kind;
            Target = target;
        }

        public override string ToString() =>
            Target >= 0 ? $"{Kind}->p{Target}" : Kind.ToString();
    }

    /// <summary>
    /// A decision maker for one seat. Bots and the local human UI both implement this,
    /// so the turn driver never needs to know which kind of player it is talking to.
    /// Every method receives only that seat's legal view of the board.
    /// </summary>
    public interface IAgent
    {
        int Seat { get; }

        ActionChoice ChooseAction(GameView view);

        /// <summary>True to challenge the pending claim (action or block).</summary>
        bool ShouldChallenge(GameView view);

        /// <summary>The role to block with, or null to pass.</summary>
        Role? ChooseBlock(GameView view);

        Role ChooseInfluenceToLose(GameView view);

        IReadOnlyList<Role> ChooseExchangeKeep(GameView view, IReadOnlyList<Role> pool, int keep);
    }
}
