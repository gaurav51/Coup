using System;
using System.Collections.Generic;
using System.Linq;

namespace Coup.Core
{
    /// <summary>
    /// The complete rules engine for standard Coup. Deliberately free of any
    /// UnityEngine dependency so it can be unit tested and, later, run
    /// server-authoritatively without a rewrite.
    ///
    /// Drive it by calling the Declare*/Choose* methods; observe it through
    /// <see cref="Events"/> and the public state properties.
    /// </summary>
    public class CoupGame
    {
        /// <summary>What the engine should do once every queued influence loss has been settled.</summary>
        private enum PostLoss
        {
            None,
            ProceedAfterChallenge, // the actor proved their claim; carry on with the action
            ResolveAction,
            FailAction,
            EndTurn
        }

        private sealed class PendingAction
        {
            public int ActorIndex;
            public ActionKind Kind;
            public int TargetIndex = -1;
            public Role? ClaimedRole;
            public int BlockerIndex = -1;
            public Role? BlockClaim;
        }

        private readonly List<PlayerState> _players = new();
        private readonly Deck _deck;
        private readonly List<GameEvent> _events = new();
        private readonly HashSet<int> _responders = new();
        private readonly Queue<(int player, LossReason reason)> _pendingLosses = new();
        private readonly List<Role> _exchangeDrawn = new();
        private readonly List<Role> _presentedPool = new();

        private PendingAction _pending;
        private PostLoss _postLoss = PostLoss.None;
        private int _exchangeKeepCount;

        public IReadOnlyList<PlayerState> Players => _players;
        public GamePhase Phase { get; private set; }
        public int CurrentPlayerIndex { get; private set; }

        /// <summary>Turns begun so far. Lets agents notice a game that is dragging.</summary>
        public int TurnNumber { get; private set; }
        private int? _presentedDeckCount;
        public int DeckCount => _presentedDeckCount ?? _deck.Count;

        /// <summary>
        /// Every copy of a role across hands, revealed cards, the deck and an
        /// exchange in progress. Always <see cref="Rules.CopiesPerRole"/>.
        /// </summary>
        public int CopiesOf(Role role)
        {
            var n = _deck.CountOf(role);
            for (var i = 0; i < _exchangeDrawn.Count; i++)
                if (_exchangeDrawn[i] == role) n++;
            foreach (var player in _players)
            {
                foreach (var card in player.Influence)
                    if (card == role) n++;
                foreach (var card in player.Revealed)
                    if (card == role) n++;
            }
            return n;
        }

        /// <summary>Every event since the game began, in order.</summary>
        public IReadOnlyList<GameEvent> Events => _events;

        /// <summary>Raised as each event occurs, for live presentation.</summary>
        public event Action<GameEvent> EventRaised;

        /// <summary>Players the engine is still waiting on in the current response window.</summary>
        public IReadOnlyCollection<int> PendingResponders => _responders;

        public int ActingPlayerIndex => _pending?.ActorIndex ?? -1;
        public ActionKind? PendingActionKind => _pending?.Kind;
        public int PendingTargetIndex => _pending?.TargetIndex ?? -1;
        public Role? PendingClaim => _pending?.ClaimedRole;
        public int PendingBlockerIndex => _pending?.BlockerIndex ?? -1;
        public Role? PendingBlockClaim => _pending?.BlockClaim;

        /// <summary>The player who must currently reveal a card, or -1.</summary>
        public int PendingLoserIndex =>
            Phase == GamePhase.AwaitingInfluenceLoss && _pendingLosses.Count > 0
                ? _pendingLosses.Peek().player
                : -1;

        /// <summary>The two cards an Ambassador has drawn and must choose from.</summary>
        public IReadOnlyList<Role> ExchangeDrawn => _exchangeDrawn;

        /// <summary>How many cards the exchanging player must keep.</summary>
        public int ExchangeKeepCount => _exchangeKeepCount;

        public IEnumerable<PlayerState> AlivePlayers => _players.Where(p => p.IsAlive);

        public PlayerState Winner =>
            Phase == GamePhase.GameOver ? _players.FirstOrDefault(p => p.IsAlive) : null;

        /// <summary>The board as one seat may legally see it.</summary>
        public GameView ViewFor(int seat) => new GameView(this, seat);

        public CoupGame(IReadOnlyList<string> playerNames, int seed = 0)
        {
            if (playerNames == null || playerNames.Count < 2)
                throw new ArgumentException("Coup needs at least two players.", nameof(playerNames));

            _deck = new Deck(seed);

            // Two-player games hand the starting player one fewer coin to offset
            // the advantage of acting first.
            var twoPlayer = playerNames.Count == 2;

            for (var i = 0; i < playerNames.Count; i++)
            {
                var coins = twoPlayer && i == 0
                    ? Rules.TwoPlayerStartingCoins
                    : Rules.StartingCoins;
                var p = new PlayerState(i, playerNames[i]) { Coins = coins };
                for (var c = 0; c < Rules.StartingInfluence; c++)
                    p.GainCard(_deck.Draw());
                _players.Add(p);
            }

            CurrentPlayerIndex = 0;
            Phase = GamePhase.AwaitingAction;
            TurnNumber = 1;
            Raise(new GameEvent { Kind = GameEventKind.GameStarted });
            Raise(new GameEvent { Kind = GameEventKind.TurnStarted, PlayerIndex = CurrentPlayerIndex });
        }

        // ---------------------------------------------------------------- actions

        public bool CanDeclare(int actor, ActionKind kind, int target = -1) =>
            DeclareError(actor, kind, target) == null;

        /// <summary>
        /// Whether this player may claim a block against the action in progress.
        /// This stays true during the challenge window: the victim of a steal can
        /// choose to block while everyone else is still deciding whether to challenge.
        /// </summary>
        public bool MayBlock(int player)
        {
            if (_pending == null || player < 0 || player >= _players.Count) return false;
            if (!_players[player].IsAlive) return false;
            if (!Rules.IsBlockable(_pending.Kind)) return false;
            if (Rules.OnlyTargetMayBlock(_pending.Kind))
                return _pending.TargetIndex == player;
            return player != _pending.ActorIndex;
        }

        /// <summary>Returns a human-readable reason the action is illegal, or null if it is legal.</summary>
        public string DeclareError(int actor, ActionKind kind, int target = -1)
        {
            if (Phase != GamePhase.AwaitingAction) return "Not accepting actions right now.";
            if (actor != CurrentPlayerIndex) return "Not your turn.";

            var p = _players[actor];
            if (p.MustCoup && kind != ActionKind.Coup)
                return $"Holding {p.Coins} coins forces a Coup.";

            var cost = Rules.CoinCost(kind);
            if (p.Coins < cost) return $"{kind} costs {cost} coins.";

            if (Rules.RequiresTarget(kind))
            {
                if (target < 0 || target >= _players.Count) return "A target is required.";
                if (target == actor) return "You cannot target yourself.";
                if (!_players[target].IsAlive) return "That player is already out.";
            }

            return null;
        }

        /// <summary>
        /// Every action the given seat could legally declare right now. Drives button
        /// enablement in the UI and keeps bots from proposing illegal moves.
        /// </summary>
        public List<ActionChoice> LegalActions(int seat)
        {
            var legal = new List<ActionChoice>();
            if (Phase != GamePhase.AwaitingAction || seat != CurrentPlayerIndex) return legal;

            foreach (ActionKind kind in Enum.GetValues(typeof(ActionKind)))
            {
                if (Rules.RequiresTarget(kind))
                {
                    foreach (var t in _players)
                        if (CanDeclare(seat, kind, t.Index))
                            legal.Add(new ActionChoice(kind, t.Index));
                }
                else if (CanDeclare(seat, kind))
                {
                    legal.Add(new ActionChoice(kind));
                }
            }

            return legal;
        }

        public void Declare(int actor, ActionKind kind, int target = -1)
        {
            var error = DeclareError(actor, kind, target);
            if (error != null) throw new InvalidOperationException(error);

            var cost = Rules.CoinCost(kind);
            if (cost > 0) AdjustCoins(actor, -cost);

            _pending = new PendingAction
            {
                ActorIndex = actor,
                Kind = kind,
                TargetIndex = Rules.RequiresTarget(kind) ? target : -1,
                ClaimedRole = Rules.ClaimedRole(kind)
            };

            Raise(new GameEvent
            {
                Kind = GameEventKind.ActionDeclared,
                PlayerIndex = actor,
                TargetIndex = _pending.TargetIndex,
                Action = kind,
                Role = _pending.ClaimedRole
            });

            if (Rules.IsChallengeable(kind)) OpenChallengeWindow();
            else if (Rules.IsBlockable(kind)) OpenBlockWindow();
            else ResolveAction();
        }

        // ------------------------------------------------------- challenge window

        private void OpenChallengeWindow()
        {
            _responders.Clear();
            foreach (var p in _players)
                if (p.IsAlive && p.Index != _pending.ActorIndex)
                    _responders.Add(p.Index);

            if (_responders.Count == 0) { AfterChallengeWindow(); return; }
            Phase = GamePhase.AwaitingChallenge;
        }

        public void PassChallenge(int player)
        {
            RequirePhase(GamePhase.AwaitingChallenge);
            if (!_responders.Remove(player))
                throw new InvalidOperationException("You are not being asked to respond.");
            if (_responders.Count == 0) AfterChallengeWindow();
        }

        public void Challenge(int challenger)
        {
            RequirePhase(GamePhase.AwaitingChallenge);
            if (!_responders.Contains(challenger))
                throw new InvalidOperationException("You are not being asked to respond.");

            var claim = _pending.ClaimedRole.Value;
            _responders.Clear();
            Raise(new GameEvent
            {
                Kind = GameEventKind.ChallengeDeclared,
                PlayerIndex = challenger,
                TargetIndex = _pending.ActorIndex,
                Role = claim
            });

            // A caught bluff means the action never happened, so its fee comes back.
            // A *blocked* action is different: there the fee stays spent.
            ResolveChallenge(_pending.ActorIndex, challenger, claim,
                onClaimProven: PostLoss.ProceedAfterChallenge,
                onClaimBluffed: PostLoss.FailAction,
                refundCostOnBluff: true);
        }

        private void AfterChallengeWindow()
        {
            if (Rules.IsBlockable(_pending.Kind)) OpenBlockWindow();
            else ResolveAction();
        }

        // ----------------------------------------------------------- block window

        private void OpenBlockWindow()
        {
            _responders.Clear();

            if (Rules.OnlyTargetMayBlock(_pending.Kind))
            {
                if (_pending.TargetIndex >= 0 && _players[_pending.TargetIndex].IsAlive)
                    _responders.Add(_pending.TargetIndex);
            }
            else
            {
                foreach (var p in _players)
                    if (p.IsAlive && p.Index != _pending.ActorIndex)
                        _responders.Add(p.Index);
            }

            if (_responders.Count == 0) { ResolveAction(); return; }
            Phase = GamePhase.AwaitingBlock;
        }

        public void PassBlock(int player)
        {
            RequirePhase(GamePhase.AwaitingBlock);
            if (!_responders.Remove(player))
                throw new InvalidOperationException("You are not being asked to respond.");
            if (_responders.Count == 0) ResolveAction();
        }

        public void Block(int blocker, Role withRole)
        {
            RequirePhase(GamePhase.AwaitingBlock);
            if (!_responders.Contains(blocker))
                throw new InvalidOperationException("You are not being asked to respond.");
            if (!Rules.CanBlockWith(_pending.Kind, withRole))
                throw new InvalidOperationException($"{withRole} cannot block {_pending.Kind}.");

            _pending.BlockerIndex = blocker;
            _pending.BlockClaim = withRole;
            _responders.Clear();

            Raise(new GameEvent
            {
                Kind = GameEventKind.BlockDeclared,
                PlayerIndex = blocker,
                TargetIndex = _pending.ActorIndex,
                Action = _pending.Kind,
                Role = withRole
            });

            OpenBlockChallengeWindow();
        }

        // -------------------------------------------------- block challenge window

        private void OpenBlockChallengeWindow()
        {
            _responders.Clear();
            foreach (var p in _players)
                if (p.IsAlive && p.Index != _pending.BlockerIndex)
                    _responders.Add(p.Index);

            if (_responders.Count == 0) { FailAction(); return; }
            Phase = GamePhase.AwaitingBlockChallenge;
        }

        public void PassBlockChallenge(int player)
        {
            RequirePhase(GamePhase.AwaitingBlockChallenge);
            if (!_responders.Remove(player))
                throw new InvalidOperationException("You are not being asked to respond.");
            if (_responders.Count == 0) FailAction(); // the block stands
        }

        public void ChallengeBlock(int challenger)
        {
            RequirePhase(GamePhase.AwaitingBlockChallenge);
            if (!_responders.Contains(challenger))
                throw new InvalidOperationException("You are not being asked to respond.");

            var claim = _pending.BlockClaim.Value;
            _responders.Clear();
            Raise(new GameEvent
            {
                Kind = GameEventKind.ChallengeDeclared,
                PlayerIndex = challenger,
                TargetIndex = _pending.BlockerIndex,
                Role = claim
            });

            // A proven block defeats the action; a bluffed block lets it through.
            ResolveChallenge(_pending.BlockerIndex, challenger, claim,
                onClaimProven: PostLoss.FailAction,
                onClaimBluffed: PostLoss.ResolveAction,
                refundCostOnBluff: false);
        }

        // ------------------------------------------------------ challenge plumbing

        /// <summary>
        /// Shared resolution for both challenge windows. A claimant who really holds
        /// the card reveals it, shuffles it back and redraws — the rule that makes
        /// honest claims genuinely rewarding.
        /// </summary>
        private void ResolveChallenge(int claimant, int challenger, Role claim,
                                      PostLoss onClaimProven, PostLoss onClaimBluffed,
                                      bool refundCostOnBluff)
        {
            var holder = _players[claimant];

            if (holder.HasRole(claim))
            {
                Raise(new GameEvent
                {
                    Kind = GameEventKind.ChallengeWon,
                    PlayerIndex = claimant,
                    TargetIndex = challenger,
                    Role = claim
                });

                holder.TakeCard(claim);
                _deck.ReturnAndShuffle(claim);
                holder.GainCard(_deck.Draw());
                Raise(new GameEvent
                {
                    Kind = GameEventKind.CardReplaced,
                    PlayerIndex = claimant,
                    Role = claim
                });

                QueueLoss(challenger, LossReason.LostChallenge);
                _postLoss = onClaimProven;
            }
            else
            {
                Raise(new GameEvent
                {
                    Kind = GameEventKind.ChallengeLost,
                    PlayerIndex = claimant,
                    TargetIndex = challenger,
                    Role = claim
                });

                // The action is cancelled outright, so anything paid up front is refunded.
                var cost = Rules.CoinCost(_pending.Kind);
                if (refundCostOnBluff && cost > 0) AdjustCoins(claimant, cost);

                QueueLoss(claimant, LossReason.FailedChallenge);
                _postLoss = onClaimBluffed;
            }

            ProcessLossQueue();
        }

        // ------------------------------------------------------------- resolution

        private void ResolveAction()
        {
            var actor = _players[_pending.ActorIndex];

            // The actor may have been eliminated by a challenge they lost.
            if (!actor.IsAlive) { FailAction(); return; }

            switch (_pending.Kind)
            {
                case ActionKind.Income:
                    AdjustCoins(actor.Index, Rules.IncomeAmount);
                    break;

                case ActionKind.ForeignAid:
                    AdjustCoins(actor.Index, Rules.ForeignAidAmount);
                    break;

                case ActionKind.Tax:
                    AdjustCoins(actor.Index, Rules.TaxAmount);
                    break;

                case ActionKind.Steal:
                {
                    var target = _players[_pending.TargetIndex];
                    var amount = Math.Min(Rules.StealAmount, target.Coins);
                    if (target.IsAlive && amount > 0)
                    {
                        AdjustCoins(target.Index, -amount);
                        AdjustCoins(actor.Index, amount);
                    }
                    break;
                }

                case ActionKind.Assassinate:
                case ActionKind.Coup:
                {
                    var target = _players[_pending.TargetIndex];
                    if (target.IsAlive)
                    {
                        QueueLoss(target.Index, _pending.Kind == ActionKind.Coup
                            ? LossReason.Couped
                            : LossReason.Assassinated);
                    }
                    RaiseResolved();
                    _postLoss = PostLoss.EndTurn;
                    ProcessLossQueue();
                    return;
                }

                case ActionKind.Exchange:
                    RaiseResolved();
                    BeginExchange(actor);
                    return;
            }

            RaiseResolved();
            EndTurn();
        }

        private void RaiseResolved() => Raise(new GameEvent
        {
            Kind = GameEventKind.ActionResolved,
            PlayerIndex = _pending.ActorIndex,
            TargetIndex = _pending.TargetIndex,
            Action = _pending.Kind
        });

        private void FailAction()
        {
            Raise(new GameEvent
            {
                Kind = GameEventKind.ActionFailed,
                PlayerIndex = _pending.ActorIndex,
                TargetIndex = _pending.TargetIndex,
                Action = _pending.Kind
            });
            EndTurn();
        }

        // --------------------------------------------------------------- exchange

        private void BeginExchange(PlayerState actor)
        {
            _exchangeDrawn.Clear();
            _presentedPool.Clear();
            for (var i = 0; i < Rules.ExchangeDrawCount; i++)
                _exchangeDrawn.Add(_deck.Draw());

            _exchangeKeepCount = actor.InfluenceCount;
            Phase = GamePhase.AwaitingExchangeSelection;

            Raise(new GameEvent
            {
                Kind = GameEventKind.ExchangeDrawn,
                PlayerIndex = actor.Index,
                Amount = Rules.ExchangeDrawCount
            });
        }

        /// <summary>The pool an exchanging player picks from: their hand plus the drawn cards.</summary>
        public IReadOnlyList<Role> ExchangePool()
        {
            if (Phase != GamePhase.AwaitingExchangeSelection)
                return Array.Empty<Role>();
            // A server snapshot names the exact cards. Rebuilding the pool from the
            // visible hand can invent a copy the table does not have.
            if (_presentedPool.Count > 0)
                return _presentedPool;
            if (_pending == null) return Array.Empty<Role>();
            var pool = new List<Role>(_players[_pending.ActorIndex].Influence);
            pool.AddRange(_exchangeDrawn);
            return pool;
        }

        public void ChooseExchangeKeep(IReadOnlyList<Role> keep)
        {
            RequirePhase(GamePhase.AwaitingExchangeSelection);
            if (keep == null || keep.Count != _exchangeKeepCount)
                throw new InvalidOperationException($"You must keep exactly {_exchangeKeepCount} card(s).");

            var pool = new List<Role>(ExchangePool());
            foreach (var role in keep)
                if (!pool.Remove(role))
                    throw new InvalidOperationException($"{role} is not available to keep.");

            var actor = _players[_pending.ActorIndex];
            actor.ClearInfluence();
            foreach (var role in keep) actor.GainCard(role);
            foreach (var role in pool) _deck.ReturnAndShuffle(role);

            _exchangeDrawn.Clear();
            Raise(new GameEvent
            {
                Kind = GameEventKind.ExchangeCompleted,
                PlayerIndex = actor.Index
            });

            EndTurn();
        }

        // --------------------------------------------------------- influence loss

        private void QueueLoss(int player, LossReason reason) =>
            _pendingLosses.Enqueue((player, reason));

        /// <summary>
        /// Settles queued losses, auto-revealing when a player has no choice left,
        /// and runs the follow-up step once the queue drains.
        /// </summary>
        private void ProcessLossQueue()
        {
            while (_pendingLosses.Count > 0)
            {
                var (index, reason) = _pendingLosses.Peek();
                var player = _players[index];

                if (!player.IsAlive) { _pendingLosses.Dequeue(); continue; }

                if (player.InfluenceCount == 1)
                {
                    ApplyLoss(player, player.Influence[0], reason);
                    _pendingLosses.Dequeue();
                    continue;
                }

                Phase = GamePhase.AwaitingInfluenceLoss;
                return;
            }

            RunPostLoss();
        }

        public void ChooseInfluenceToLose(int player, Role role)
        {
            RequirePhase(GamePhase.AwaitingInfluenceLoss);
            if (_pendingLosses.Count == 0 || _pendingLosses.Peek().player != player)
                throw new InvalidOperationException("It is not your card to lose.");

            var state = _players[player];
            if (!state.HasRole(role))
                throw new InvalidOperationException($"You do not hold {role}.");

            ApplyLoss(state, role, _pendingLosses.Peek().reason);
            _pendingLosses.Dequeue();
            ProcessLossQueue();
        }

        private void ApplyLoss(PlayerState player, Role role, LossReason reason)
        {
            player.RevealCard(role);
            Raise(new GameEvent
            {
                Kind = GameEventKind.InfluenceLost,
                PlayerIndex = player.Index,
                Role = role,
                Reason = reason
            });

            if (!player.IsAlive)
            {
                // "A player with no face-down cards is exiled and returns all coins
                // to the Treasury."
                if (player.Coins != 0) AdjustCoins(player.Index, -player.Coins);

                Raise(new GameEvent
                {
                    Kind = GameEventKind.PlayerEliminated,
                    PlayerIndex = player.Index
                });
            }
        }

        private void RunPostLoss()
        {
            var step = _postLoss;
            _postLoss = PostLoss.None;

            if (CheckGameOver()) return;

            switch (step)
            {
                case PostLoss.ProceedAfterChallenge: AfterChallengeWindow(); break;
                case PostLoss.ResolveAction: ResolveAction(); break;
                case PostLoss.FailAction: FailAction(); break;
                case PostLoss.EndTurn: EndTurn(); break;
                default: EndTurn(); break;
            }
        }

        // ------------------------------------------------------------- turn order

        private void EndTurn()
        {
            if (CheckGameOver()) return;

            _pending = null;
            _responders.Clear();

            var next = CurrentPlayerIndex;
            do { next = (next + 1) % _players.Count; }
            while (!_players[next].IsAlive);

            CurrentPlayerIndex = next;
            TurnNumber++;
            Phase = GamePhase.AwaitingAction;
            Raise(new GameEvent { Kind = GameEventKind.TurnStarted, PlayerIndex = next });
        }

        private bool CheckGameOver()
        {
            if (_players.Count(p => p.IsAlive) > 1) return false;

            Phase = GamePhase.GameOver;
            _responders.Clear();
            _pendingLosses.Clear();
            Raise(new GameEvent
            {
                Kind = GameEventKind.GameOver,
                PlayerIndex = _players.FirstOrDefault(p => p.IsAlive)?.Index ?? -1
            });
            return true;
        }

        // ------------------------------------------------------------- utilities

        private void AdjustCoins(int player, int delta)
        {
            _players[player].Coins += delta;
            Raise(new GameEvent
            {
                Kind = GameEventKind.CoinsChanged,
                PlayerIndex = player,
                Amount = delta
            });
        }

        /// <summary>
        /// Overwrites the visible board with a server snapshot. The server remains
        /// the rules authority; this only lets the table draw what that seat may see.
        /// </summary>
        public void Apply(TablePresentation view)
        {
            if (view?.Seats == null || view.Seats.Count != _players.Count)
                throw new InvalidOperationException("The server table does not match this game.");

            for (var i = 0; i < _players.Count; i++)
            {
                var seat = view.Seats[i];
                var player = _players[i];
                if (!string.IsNullOrEmpty(seat.Name)) player.Name = seat.Name;
                player.Coins = seat.Coins;

                var hidden = seat.Hidden;
                if (hidden == null)
                {
                    hidden = new List<Role>(seat.HiddenCount);
                    for (var c = 0; c < seat.HiddenCount; c++) hidden.Add(Role.Ambassador);
                }

                player.Show(hidden, seat.Revealed ?? new List<Role>());
            }

            Phase = view.Phase;
            CurrentPlayerIndex = view.CurrentPlayer;
            TurnNumber = view.Turn;
            _presentedDeckCount = view.DeckCount;

            if (view.Action.HasValue)
            {
                _pending = new PendingAction
                {
                    ActorIndex = view.Actor,
                    Kind = view.Action.Value,
                    TargetIndex = view.Target,
                    ClaimedRole = view.Claim,
                    BlockerIndex = view.Blocker,
                    BlockClaim = view.BlockClaim
                };
            }
            else
            {
                _pending = null;
            }

            _responders.Clear();
            if (view.Waiting != null)
                for (var i = 0; i < view.Waiting.Count; i++) _responders.Add(view.Waiting[i]);

            _pendingLosses.Clear();
            if (Phase == GamePhase.AwaitingInfluenceLoss && view.Loser >= 0)
                _pendingLosses.Enqueue((view.Loser, view.LossReason ?? LossReason.Couped));

            _exchangeDrawn.Clear();
            _presentedPool.Clear();
            _exchangeKeepCount = 0;
            if (Phase == GamePhase.AwaitingExchangeSelection && view.ExchangePool != null && _pending != null)
            {
                _presentedPool.AddRange(view.ExchangePool);
                var pool = new List<Role>(view.ExchangePool);
                var hand = _players[_pending.ActorIndex].Influence;
                for (var i = 0; i < hand.Count; i++) pool.Remove(hand[i]);
                _exchangeDrawn.AddRange(pool);
                _exchangeKeepCount = view.ExchangeKeep;
            }
        }

        public static CoupGame FromPresentation(TablePresentation view)
        {
            if (view?.Seats == null || view.Seats.Count < 2)
                throw new ArgumentException("Coup needs at least two players.", nameof(view));

            var names = new List<string>(view.Seats.Count);
            for (var i = 0; i < view.Seats.Count; i++)
                names.Add(string.IsNullOrEmpty(view.Seats[i].Name) ? $"P{i}" : view.Seats[i].Name);

            var game = new CoupGame(names, 1);
            game.Apply(view);
            return game;
        }

        private void RequirePhase(GamePhase expected)
        {
            if (Phase != expected)
                throw new InvalidOperationException($"Expected {expected} but the game is in {Phase}.");
        }

        private void Raise(GameEvent e)
        {
            _events.Add(e);
            EventRaised?.Invoke(e);
        }
    }
}
