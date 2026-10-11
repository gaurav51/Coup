using System;
using System.Collections.Generic;
using System.Linq;

namespace Coup.Core
{
    public enum DriverStatus
    {
        /// <summary>An agent made a decision; call Step again.</summary>
        Acted,

        /// <summary>The next decision belongs to a seat with no agent — a human.</summary>
        WaitingForHuman,

        GameOver
    }

    /// <summary>
    /// Advances the game one agent decision at a time. Seats without an agent are
    /// left alone so the UI can drive them, which lets the identical driver run a
    /// headless all-bot game in tests and the real match in Unity.
    /// </summary>
    public class GameDriver
    {
        private readonly CoupGame _game;
        private readonly Dictionary<int, IAgent> _agents = new();

        public CoupGame Game => _game;

        /// <summary>Counts bot proposals the rules rejected. Should stay at zero.</summary>
        public int IllegalProposals { get; private set; }

        public GameDriver(CoupGame game, IEnumerable<IAgent> agents)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            foreach (var a in agents) _agents[a.Seat] = a;
        }

        /// <summary>The seat that owes the next decision, or -1 if the game is over.</summary>
        public int SeatToAct() => _game.Phase switch
        {
            GamePhase.AwaitingAction => _game.CurrentPlayerIndex,
            GamePhase.AwaitingChallenge or
            GamePhase.AwaitingBlock or
            GamePhase.AwaitingBlockChallenge => NextResponder(),
            GamePhase.AwaitingInfluenceLoss => _game.PendingLoserIndex,
            GamePhase.AwaitingExchangeSelection => _game.ActingPlayerIndex,
            _ => -1
        };

        /// <summary>Responders answer in seat order so replays are deterministic.</summary>
        private int NextResponder() =>
            _game.PendingResponders.Count == 0 ? -1 : _game.PendingResponders.Min();

        public DriverStatus Step()
        {
            if (_game.Phase == GamePhase.GameOver) return DriverStatus.GameOver;

            var seat = SeatToAct();
            if (seat < 0) return DriverStatus.GameOver;
            if (!_agents.TryGetValue(seat, out var agent)) return DriverStatus.WaitingForHuman;

            var view = _game.ViewFor(seat);

            switch (_game.Phase)
            {
                case GamePhase.AwaitingAction:
                    DoAction(seat, agent, view);
                    break;

                case GamePhase.AwaitingChallenge:
                    if (agent.ShouldChallenge(view)) _game.Challenge(seat);
                    else _game.PassChallenge(seat);
                    break;

                case GamePhase.AwaitingBlock:
                {
                    var role = agent.ChooseBlock(view);
                    if (role.HasValue && Rules.CanBlockWith(_game.PendingActionKind.Value, role.Value))
                        _game.Block(seat, role.Value);
                    else
                        _game.PassBlock(seat);
                    break;
                }

                case GamePhase.AwaitingBlockChallenge:
                    if (agent.ShouldChallenge(view)) _game.ChallengeBlock(seat);
                    else _game.PassBlockChallenge(seat);
                    break;

                case GamePhase.AwaitingInfluenceLoss:
                {
                    var role = agent.ChooseInfluenceToLose(view);
                    if (!view.MyInfluence.Contains(role)) role = view.MyInfluence[0];
                    _game.ChooseInfluenceToLose(seat, role);
                    break;
                }

                case GamePhase.AwaitingExchangeSelection:
                {
                    var pool = _game.ExchangePool();
                    var keep = agent.ChooseExchangeKeep(view, pool, _game.ExchangeKeepCount);
                    if (!IsValidKeep(pool, keep, _game.ExchangeKeepCount))
                        keep = pool.Take(_game.ExchangeKeepCount).ToList();
                    _game.ChooseExchangeKeep(keep);
                    break;
                }
            }

            return _game.Phase == GamePhase.GameOver ? DriverStatus.GameOver : DriverStatus.Acted;
        }

        private void DoAction(int seat, IAgent agent, GameView view)
        {
            var choice = agent.ChooseAction(view);

            if (!_game.CanDeclare(seat, choice.Kind, choice.Target))
            {
                IllegalProposals++;
                var legal = _game.LegalActions(seat);
                choice = legal.FirstOrDefault(c => c.Kind == ActionKind.Income);
                if (legal.Count > 0 && choice.Kind != ActionKind.Income) choice = legal[0];
                else if (legal.Count == 0) throw new InvalidOperationException("No legal action available.");
            }

            _game.Declare(seat, choice.Kind, choice.Target);
        }

        private static bool IsValidKeep(IReadOnlyList<Role> pool, IReadOnlyList<Role> keep, int required)
        {
            if (keep == null || keep.Count != required) return false;
            var working = new List<Role>(pool);
            foreach (var r in keep)
                if (!working.Remove(r)) return false;
            return true;
        }

        /// <summary>Runs until a human is needed or the game ends. Returns the final status.</summary>
        public DriverStatus RunUntilHumanOrEnd(int maxSteps = 10000)
        {
            for (var i = 0; i < maxSteps; i++)
            {
                var status = Step();
                if (status != DriverStatus.Acted) return status;
            }
            throw new InvalidOperationException($"Game did not settle within {maxSteps} steps.");
        }
    }
}
