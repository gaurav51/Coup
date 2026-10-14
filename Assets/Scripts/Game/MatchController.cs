using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Coup.Core;
using Coup.AI;
using Coup.Net;

namespace Coup.Game
{
    /// <summary>
    /// Owns the match and pumps it. Bot seats are stepped on a timer so their moves
    /// are readable; the human seat halts the pump until the UI submits a decision.
    /// All rules live in Coup.Core — this class only handles pacing and input.
    /// </summary>
    public class MatchController : MonoBehaviour
    {
        [Header("Match")]
        [Range(2, 6)] public int playerCount = 6;
        public int humanSeat = 0;
        public int seed = 0;

        [Header("Pacing (seconds)")]
        public float botThinkTime = 0.9f;
        public float resolveBeat = 0.45f;

        [Header("Online")]
        public bool playOnline = true;
        public string serverUrl = "http://localhost:3000";
        public string onlineName = "You";
        public string joinCode = "";
        public bool autoStartOnline = true;

        public CoupGame Game { get; private set; }

        /// <summary>Raised whenever the UI should redraw.</summary>
        public event Action StateChanged;

        /// <summary>Raised per rules event, for the log and animations.</summary>
        public event Action<GameEvent> EventOccurred;

        private GameDriver _driver;
        private bool _humanActed;
        private Coroutine _pump;
        private Coroutine _playback;
        private readonly Queue<Beat> _beats = new();

        /// <summary>True while a title or a challenge is still on screen. Input waits.</summary>
        public bool Presenting { get; private set; }

        private struct Beat
        {
            public GameEvent Event;
            public bool HasEvent;
            public bool Instant;
            public JsonValue State;
            public bool ApplyState;
            public List<JsonValue> Log;
        }

        /// <summary>
        /// A block chosen while the claim is still being challenged. It is played
        /// the moment the block window opens, so the victim does not have to catch
        /// a second prompt after everyone else has passed.
        /// </summary>
        private Role? _queuedBlock;
        private OnlineSession _online;
        private int _lastServerEvent;

        public bool IsHumanTurn =>
            Game != null && Game.Phase != GamePhase.GameOver &&
            _driver != null && _driver.SeatToAct() == humanSeat;

        public GameView HumanView => Game?.ViewFor(humanSeat);

        private void Start()
        {
            if (playOnline) BeginOnline();
            else StartMatch();
        }

        private void Update() => _online?.Pump();

        private void OnDestroy()
        {
            _online?.Dispose();
            _online = null;
        }

        private void BeginOnline()
        {
            _online?.Dispose();
            _online = new OnlineSession(this);
            _online.Begin();
        }

        /// <summary>Drops the server session and deals a match on this device.</summary>
        public void PlayOffline()
        {
            _online?.Dispose();
            _online = null;
            playOnline = false;
            StartMatch();
        }

        public void ShowServerState(JsonValue state, List<JsonValue> openingLog)
        {
            if (state == null) return;
            // The board updates after the events that caused it, so a challenge
            // can finish before the cards and coins jump to the result.
            Enqueue(new Beat { ApplyState = true, State = state, Log = openingLog });
        }

        public void ShowServerEvent(JsonValue payload)
        {
            if (payload == null) return;
            var seq = RemoteTable.Sequence(payload);
            if (seq > 0 && seq <= _lastServerEvent) return;
            if (seq > _lastServerEvent) _lastServerEvent = seq;
            Enqueue(new Beat { HasEvent = true, Event = RemoteTable.ReadEvent(payload) });
        }

        private void ApplyServerState(JsonValue state)
        {
            if (state == null) return;
            var view = RemoteTable.Read(state);
            if (view.Seats == null || view.Seats.Count < 2) return;

            humanSeat = state.Int("you", 0);
            playerCount = view.Seats.Count;
            if (Game == null || Game.Players.Count != view.Seats.Count)
                Game = CoupGame.FromPresentation(view);
            else
                Game.Apply(view);

            StateChanged?.Invoke();
        }

        public void StartMatch()
        {
            if (_pump != null) StopCoroutine(_pump);

            var actualSeed = seed != 0 ? seed : UnityEngine.Random.Range(1, int.MaxValue);
            var names = BuildNames();

            Game = new CoupGame(names, actualSeed);
            Game.EventRaised += OnRulesEvent;

            var presets = BotPersonality.Presets;
            var agents = Enumerable.Range(0, playerCount)
                .Where(i => i != humanSeat)
                .Select(i => (IAgent)new BotAgent(i, presets[i % presets.Length], actualSeed * 31 + i))
                .ToList();

            _driver = new GameDriver(Game, agents);
            _humanActed = false;
            _queuedBlock = null;
            Time.timeScale = 1f;

            _pump = StartCoroutine(Pump());
        }

        private List<string> BuildNames()
        {
            var botNames = new[] { "Valca", "Orsin", "Meridia", "Tarron", "Sable" };
            var names = new List<string>();
            var b = 0;
            for (var i = 0; i < playerCount; i++)
                names.Add(i == humanSeat ? "You" : botNames[b++ % botNames.Length]);
            return names;
        }

        private void OnRulesEvent(GameEvent e)
        {
            Enqueue(new Beat { HasEvent = true, Event = e });
        }

        /// <summary>
        /// Plays queued beats one at a time. Each event's hold — from the server,
        /// or the same table locally — passes before the next event or the board update.
        /// </summary>
        private void Enqueue(Beat beat)
        {
            _beats.Enqueue(beat);
            Presenting = true;
            if (_playback == null) _playback = StartCoroutine(PlayBeats());
        }

        private IEnumerator PlayBeats()
        {
            while (_beats.Count > 0)
            {
                var beat = _beats.Dequeue();
                if (beat.HasEvent && beat.Event != null)
                {
                    EventOccurred?.Invoke(beat.Event);
                    var hold = HoldSeconds(beat.Event, beat.Instant);
                    if (hold > 0f) yield return new WaitForSecondsRealtime(hold);
                }

                if (!beat.ApplyState) continue;

                ApplyServerState(beat.State);
                var log = beat.Log;
                if (log == null || log.Count == 0) continue;

                // A resumed seat already lived these events. Replay the log, but
                // don't make the player watch the whole match again.
                var instant = log.Count > 4;
                for (var i = 0; i < log.Count; i++)
                {
                    var replay = RemoteTable.ReadEvent(log[i]);
                    EventOccurred?.Invoke(replay);
                    var hold = HoldSeconds(replay, instant);
                    if (hold > 0f) yield return new WaitForSecondsRealtime(hold);
                }
            }

            Presenting = false;
            _playback = null;
        }

        /// <summary>Same pauses as server/src/pacing.js. A server hold, when present, wins.</summary>
        private float HoldSeconds(GameEvent e, bool instant)
        {
            if (instant || e == null) return 0f;
            if (e.HoldMs > 0) return e.HoldMs / 1000f;

            switch (e.Kind)
            {
                case GameEventKind.ActionDeclared:
                    return e.Action is ActionKind.Tax or ActionKind.Steal or ActionKind.Assassinate
                        or ActionKind.Exchange or ActionKind.Coup ? 1.3f : 0.45f;
                case GameEventKind.BlockDeclared: return 1.1f;
                case GameEventKind.ChallengeDeclared: return 1.3f;
                case GameEventKind.ChallengeWon: return 1.5f;
                case GameEventKind.ChallengeLost: return 0.8f;
                case GameEventKind.CardReplaced: return 1.7f;
                case GameEventKind.InfluenceLost:
                    return e.Reason == LossReason.FailedChallenge ? 1.45f : 1.1f;
                case GameEventKind.CoinsChanged: return 0.7f;
                case GameEventKind.PlayerEliminated: return 0.9f;
                case GameEventKind.ExchangeCompleted: return 0.9f;
                case GameEventKind.ExchangeDrawn: return 0.4f;
                case GameEventKind.ActionResolved:
                case GameEventKind.ActionFailed: return 0.5f;
                case GameEventKind.TurnStarted: return 0.45f;
                case GameEventKind.GameOver: return 0.8f;
                default: return 0f;
            }
        }

        private IEnumerator Pump()
        {
            yield return new WaitUntil(() => !Presenting);
            StateChanged?.Invoke();

            while (Game.Phase != GamePhase.GameOver)
            {
                var seat = _driver.SeatToAct();
                if (seat < 0) break;

                if (seat == humanSeat)
                {
                    yield return new WaitUntil(() => _humanActed);
                    _humanActed = false;
                }
                else
                {
                    yield return new WaitForSecondsRealtime(botThinkTime);
                    _driver.Step();
                }

                // A block picked during the challenge resolves here, before the UI
                // draws, so the steal cannot slip through while that choice waits.
                ApplyQueuedBlock();
                if (Presenting) yield return new WaitUntil(() => !Presenting);
                else yield return new WaitForSecondsRealtime(resolveBeat);
                StateChanged?.Invoke();
            }

            if (Presenting) yield return new WaitUntil(() => !Presenting);
            StateChanged?.Invoke();
        }

        // ------------------------------------------------------------ human input

        public void HumanDeclare(ActionKind kind, int target = -1)
        {
            if (Presenting) return;
            if (_online != null)
            {
                _online.Declare(kind, target);
                return;
            }

            Submit(() => Game.Declare(humanSeat, kind, target));
        }

        public void HumanChallenge()
        {
            if (Presenting) return;
            if (_online != null)
            {
                _online.Challenge();
                return;
            }

            Submit(() =>
            {
                if (Game.Phase == GamePhase.AwaitingBlockChallenge) Game.ChallengeBlock(humanSeat);
                else Game.Challenge(humanSeat);
            });
        }

        public void HumanBlock(Role role)
        {
            if (Presenting) return;
            if (_online != null)
            {
                _online.Block(role);
                return;
            }
            if (Game == null || !Game.PendingActionKind.HasValue) return;
            if (!Rules.CanBlockWith(Game.PendingActionKind.Value, role)) return;

            // The challenge window comes first, but the victim already knows they
            // want to block. Remember it and step out of the challenge; the pump
            // plays the block once every other player has had their say.
            if (Game.Phase == GamePhase.AwaitingChallenge)
            {
                if (!Game.MayBlock(humanSeat) || !Game.PendingResponders.Contains(humanSeat)) return;
                _queuedBlock = role;
                HumanPass();
                return;
            }

            Submit(() => Game.Block(humanSeat, role));
        }

        private void ApplyQueuedBlock()
        {
            if (!_queuedBlock.HasValue || Game == null) return;

            if (Game.Phase == GamePhase.AwaitingBlock &&
                Game.PendingResponders.Contains(humanSeat) &&
                Game.MayBlock(humanSeat))
            {
                var role = _queuedBlock.Value;
                _queuedBlock = null;
                try
                {
                    Game.Block(humanSeat, role);
                }
                catch (InvalidOperationException ex)
                {
                    Debug.LogWarning($"[Coup] Could not play the queued block: {ex.Message}");
                }
                return;
            }

            // A loss still has to be revealed before the action continues. Anything
            // else means the steal was settled and the saved block no longer applies.
            if (Game.Phase is GamePhase.AwaitingChallenge or GamePhase.AwaitingInfluenceLoss)
                return;

            _queuedBlock = null;
        }

        public void HumanPass()
        {
            if (Presenting) return;
            if (_online != null)
            {
                _online.Pass();
                return;
            }

            Submit(() =>
            {
                switch (Game.Phase)
                {
                    case GamePhase.AwaitingChallenge: Game.PassChallenge(humanSeat); break;
                    case GamePhase.AwaitingBlock: Game.PassBlock(humanSeat); break;
                    case GamePhase.AwaitingBlockChallenge: Game.PassBlockChallenge(humanSeat); break;
                }
            });
        }

        public void HumanLoseInfluence(Role role)
        {
            if (Presenting) return;
            if (_online != null)
            {
                _online.Lose(role);
                return;
            }

            Submit(() => Game.ChooseInfluenceToLose(humanSeat, role));
        }

        public void HumanExchangeKeep(IReadOnlyList<Role> keep)
        {
            if (Presenting) return;
            if (_online != null)
            {
                _online.Exchange(keep);
                return;
            }

            Submit(() => Game.ChooseExchangeKeep(keep));
        }

        /// <summary>The server refused the last move. Redraw so a closed picker can be tried again.</summary>
        public void Rejected() => StateChanged?.Invoke();

        /// <summary>
        /// Applies a human decision. Illegal input is reported rather than thrown at
        /// the UI, so a mis-click never wedges the pump.
        /// </summary>
        private void Submit(Action apply)
        {
            if (Game == null || Game.Phase == GamePhase.GameOver) return;

            try
            {
                apply();
                _humanActed = true;
            }
            catch (InvalidOperationException ex)
            {
                Debug.LogWarning($"[Coup] Rejected input: {ex.Message}");
                StateChanged?.Invoke();
            }
        }
    }
}
