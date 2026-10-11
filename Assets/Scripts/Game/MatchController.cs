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
        private float _beat;

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
            var view = RemoteTable.Read(state);
            if (view.Seats == null || view.Seats.Count < 2) return;

            humanSeat = state.Int("you", 0);
            playerCount = view.Seats.Count;
            if (Game == null || Game.Players.Count != view.Seats.Count)
                Game = CoupGame.FromPresentation(view);
            else
                Game.Apply(view);

            StateChanged?.Invoke();
            if (openingLog == null) return;
            for (var i = 0; i < openingLog.Count; i++) ShowServerEvent(openingLog[i]);
        }

        public void ShowServerEvent(JsonValue payload)
        {
            if (payload == null || Game == null) return;
            var seq = RemoteTable.Sequence(payload);
            if (seq > 0 && seq <= _lastServerEvent) return;
            if (seq > _lastServerEvent) _lastServerEvent = seq;
            EventOccurred?.Invoke(RemoteTable.ReadEvent(payload));
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
            EventOccurred?.Invoke(e);

            // A challenge has to stay on screen long enough to show the card,
            // and a proven claim then has to finish its swap back into the deck.
            if (e.Kind == GameEventKind.CardReplaced) _beat = Mathf.Max(_beat, 2.3f);
            else if (e.Kind == GameEventKind.ChallengeWon) _beat = Mathf.Max(_beat, 1.3f);
            else if (e.Kind == GameEventKind.ChallengeDeclared) _beat = Mathf.Max(_beat, 0.95f);
            else if (e.Kind == GameEventKind.InfluenceLost && e.Reason == LossReason.FailedChallenge)
                _beat = Mathf.Max(_beat, 1.45f);
        }

        private IEnumerator Pump()
        {
            StateChanged?.Invoke();

            while (Game.Phase != GamePhase.GameOver)
            {
                _beat = resolveBeat;
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
                StateChanged?.Invoke();
                yield return new WaitForSecondsRealtime(_beat);
            }

            StateChanged?.Invoke();
        }

        // ------------------------------------------------------------ human input

        public void HumanDeclare(ActionKind kind, int target = -1)
        {
            if (_online != null)
            {
                _online.Declare(kind, target);
                return;
            }

            Submit(() => Game.Declare(humanSeat, kind, target));
        }

        public void HumanChallenge()
        {
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
            if (_online != null)
            {
                _online.Lose(role);
                return;
            }

            Submit(() => Game.ChooseInfluenceToLose(humanSeat, role));
        }

        public void HumanExchangeKeep(IReadOnlyList<Role> keep)
        {
            if (_online != null)
            {
                _online.Exchange(keep);
                return;
            }

            Submit(() => Game.ChooseExchangeKeep(keep));
        }

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
