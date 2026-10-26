using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Coup.Core;
using Coup.Game.UI;
using Coup.Net;
using UnityEngine;
using UnityEngine.UI;
namespace Coup.Game
{
    public sealed class OnlineSession : IDisposable
    {
        private readonly MatchController _match;
        private readonly ConcurrentQueue<Action> _main = new ConcurrentQueue<Action>();
        private readonly SocketIoClient _socket;
        private readonly CancellationTokenSource _life = new CancellationTokenSource();
        private List<JsonValue> _openingLog;
        private GameObject _overlay;
        private RectTransform _card;
        private TMPro.TMP_Text _message;
        private bool _disposed, _connecting, _ready, _sending, _terminal;
        private string _code, _token;
        private float _deadlineAt = -1, _windowSeconds = -1, _queueDeadlineAt = -1;
        private string _queueHeading;
        private bool _switchingTable, _menuOpen;
        /// <summary>Seconds until the matchmaking window lapses, or -1 when not queueing.</summary>
        public int QueueSecondsRemaining => _queueDeadlineAt < 0 ? -1 : Mathf.Max(0, Mathf.CeilToInt(_queueDeadlineAt - Time.realtimeSinceStartup));
        /// <summary>How much of this turn's clock is left, 0..1, or -1 when there is no clock.</summary>
        public float TurnProgress => _deadlineAt < 0 || _windowSeconds <= 0 ? -1f
            : Mathf.Clamp01((_deadlineAt - Time.realtimeSinceStartup) / _windowSeconds);
        public int SecondsRemaining => _deadlineAt < 0 ? -1 : Mathf.Max(0, Mathf.CeilToInt(_deadlineAt - Time.realtimeSinceStartup));
        private JsonValue _lobby;
        public bool Connected => _ready && _socket.IsOpen;
        public OnlineSession(MatchController match) {
            _match = match; _socket = new SocketIoClient(_main.Enqueue);
            _socket.EventReceived += OnSocketEvent;
            _socket.Closed += OnClosed;
        }
        public void Pump() {
            while (_main.TryDequeue(out var action)) if (!_disposed) action();
            TickQueue();
        }

        /// <summary>
        /// The waiting card is rebuilt only when a lobby:state arrives, which is far
        /// too rarely to count anything down, so the last line is rewritten here each
        /// frame while the search is up.
        /// </summary>
        private void TickQueue() {
            if (_disposed || _queueHeading == null || _message == null) return;
            if (_overlay == null || !_overlay.activeSelf) return;
            var left = QueueSecondsRemaining;
            _message.text = _queueHeading + "\n\n" + (left > 0
                ? "Starting in " + left + "s, or sooner if the table fills."
                : "Seating the court…");
        }
        public Task<JsonValue> VoiceAccess(string action) => Ack("voice:access", JsonValue.FromObject(("action", JsonValue.FromString(action))));
        public void Begin() => Reconnect();
        public void CheckConnection() { if (!_socket.IsOpen && !_terminal) Reconnect(); }
        private void OnClosed(string message) { _ready = false; if (!_terminal) Reconnect(); }
        private async void Reconnect() {
            if (_disposed || _connecting || _terminal) return;
            _connecting = true; _ready = false;
            try {
                for (var attempt = 0; attempt < 6 && !_disposed; attempt++) {
                    _menuOpen = false;
                    Show(attempt == 0 ? "Connecting to the court…" : "Connection lost. Reconnecting…\nAttempt " + (attempt + 1) + " of 6");
                    AddButton("Back to lobby", new Vector2(0,-265), () => _match.ReturnToLobby());
                    try {
                        if (attempt > 0) await Task.Delay(Math.Min(15000, 1000 * (1 << (attempt - 1))) + UnityEngine.Random.Range(0, 400), _life.Token);
                        var access = await PlatformClient.Instance.AccessToken(attempt > 0);
                        await _socket.Connect(PlatformClient.Instance.BaseUrl, access, _life.Token);
                        _life.Token.ThrowIfCancellationRequested();
                        JsonValue response;
                        if (!string.IsNullOrEmpty(_code)) response = await Ack("session:resume", JsonValue.FromObject(("code", JsonValue.FromString(_code)), ("token", JsonValue.FromString(_token))));
                        else {
                            var active = (await PlatformClient.Instance.Request("GET", "/v1/session/active", null, _life.Token)).Get("active");
                            if (active != null && active.Type != JsonValue.Kind.Null && !MatchLaunch.ResumeRequested
                                && (!string.IsNullOrEmpty(MatchLaunch.QueueMode) || !string.IsNullOrWhiteSpace(_match.joinCode)))
                            {
                                var choice = new TaskCompletionSource<bool>();
                                using (_life.Token.Register(() => choice.TrySetCanceled()))
                                {
                                    Show("SAVED TABLE " + active.Str("code") + "\nYou already have a seat. Resume it, or leave it to find a new table. Leaving abandons that seat.");
                                    AddButton("Resume saved table", new Vector2(-205,-185), () => choice.TrySetResult(true));
                                    AddButton("Leave & find new", new Vector2(205,-185), () => choice.TrySetResult(false));
                                    var resume = await choice.Task;
                                    if (resume) MatchLaunch.Resume();
                                    Show("Connecting to your table…");
                                    if (!resume)
                                    {
                                        _switchingTable = true;
                                        try { await Ack("session:recover", JsonValue.FromObject()); await Ack("lobby:leave", JsonValue.FromObject()); }
                                        finally { _switchingTable = false; }
                                        active = null;
                                    }
                                }
                            }
                            if (active != null && active.Type != JsonValue.Kind.Null) response = await Ack("session:recover", JsonValue.FromObject());
                            else if (!string.IsNullOrEmpty(MatchLaunch.QueueMode)) response = await Ack("queue:join", JsonValue.FromObject(("name", JsonValue.FromString(CleanName())), ("mode", JsonValue.FromString(MatchLaunch.QueueMode))));
                            else if (!string.IsNullOrWhiteSpace(_match.joinCode)) response = await Ack("lobby:join", JsonValue.FromObject(("code", JsonValue.FromString(_match.joinCode.Trim())), ("name", JsonValue.FromString(CleanName()))));
                            else response = await Ack("lobby:create", JsonValue.FromObject(("name", JsonValue.FromString(CleanName()))));
                        }
                        _code = response.Str("code"); _token = response.Str("token"); _ready = true;
                        if (_lobby != null && _lobby.Str("status") == "lobby") {
                            await Ack("lobby:ready", JsonValue.FromObject(("ready", JsonValue.FromBool(true))));
                            if (_match.autoStartOnline && (_lobby.Str("mode") ?? "private") == "private" && string.IsNullOrEmpty(MatchLaunch.QueueMode) && _lobby.Get("you")?.Flag("host") == true) await StartMatch(true);
                            else ShowLobby();
                        }
                        return;
                    } catch (OperationCanceledException) { return; }
                    catch (ApiException error) when (error.Status == 401) { Fail("Your sign-in expired. Return to the lobby to reconnect your account."); return; }
                    catch (InvalidOperationException error) {
                        if (!string.IsNullOrEmpty(MatchLaunch.QueueMode) &&
                            (error.Message == "That seat does not exist." || error.Message == "That table does not exist."))
                        { _code = null; _token = null; _lobby = null; _socket.Dispose(); continue; }
                        Fail(error.Message, true); return;
                    }
                    catch (Exception) { _socket.Dispose(); }
                }
                if (!_disposed) Fail("The server is unavailable. Your table can be resumed when the connection returns.", true);
            } finally { _connecting = false; }
        }
        public void Declare(ActionKind kind, int target) {
            var body = JsonValue.FromObject(("action", JsonValue.FromString(kind.ToString())));
            if (target >= 0) body.Object["target"] = JsonValue.FromNumber(target);
            Send("game:declare", body);
        }
        public void Challenge() => Send("game:challenge", JsonValue.FromObject());
        public void Pass() => Send("game:pass", JsonValue.FromObject());
        public void Block(Role role) => Send("game:block", JsonValue.FromObject(("role", JsonValue.FromString(role.ToString()))));
        public void Lose(Role role) => Send("game:lose", JsonValue.FromObject(("role", JsonValue.FromString(role.ToString()))));
        public void Exchange(IReadOnlyList<Role> keep) {
            var cards = new List<JsonValue>(); foreach (var role in keep) cards.Add(JsonValue.FromString(role.ToString()));
            Send("game:exchange", JsonValue.FromObject(("keep", JsonValue.FromArray(cards))));
        }
        private async void Send(string name, JsonValue body) {
            if (!Connected || _sending || _disposed) return;
            _sending = true;
            try { await Ack(name, body); }
            catch (InvalidOperationException error) { if (!_disposed) { _match.Rejected(); Show(error.Message); AddButton("Continue", new Vector2(0,-185), () => _overlay.SetActive(false)); } }
            catch { if (!_disposed) { _socket.Dispose(); _ready = false; Reconnect(); } }
            finally { _sending = false; }
        }
        private async Task<JsonValue> Ack(string name, JsonValue body) {
            var value = await _socket.Emit(name, body);
            if (!value.Flag("ok")) throw new InvalidOperationException(value.Str("error") ?? "The server could not complete that action.");
            return value;
        }
        private async Task StartMatch(bool fillBots) {
            await Ack("lobby:start", JsonValue.FromObject(("bots", JsonValue.FromNumber(fillBots ? Mathf.Max(0,_match.playerCount - (_lobby?.Arr("players")?.Count ?? 1)) : 0))));
        }
        private async void HostStart(bool fillBots) {
            if (_sending || !Connected) return; _sending = true;
            try { await StartMatch(fillBots); }
            catch (Exception error) { if (!_disposed) { ShowLobby(error.Message); } }
            finally { _sending = false; }
        }
        private string CleanName() => PlatformClient.Instance.Profile?.Str("name") ?? "Player";
        private void OnSocketEvent(string name, JsonValue payload) {
            if (_disposed || _switchingTable) return;
            switch(name) {
                case "lobby:state":
                    _lobby = payload;
                    var queueEnd = payload.Get("queueDeadline"); var lobbyNow = payload.Get("serverTime");
                    _queueDeadlineAt = queueEnd?.Type == JsonValue.Kind.Number && lobbyNow?.Type == JsonValue.Kind.Number
                        ? Time.realtimeSinceStartup + (float)((queueEnd.Number - lobbyNow.Number) / 1000) : -1f;
                    if (payload.Str("status") == "lobby" && !_connecting) ShowLobby();
                    break;
                case "game:log": _openingLog = payload.Arr("events"); break;
                case "game:state":
                    var deadline=payload.Get("deadline"); var serverTime=payload.Get("serverTime");
                    if(deadline?.Type==JsonValue.Kind.Number && serverTime?.Type==JsonValue.Kind.Number) {
                        var next=Time.realtimeSinceStartup+(float)((deadline.Number-serverTime.Number)/1000);
                        // The server sends when the turn ends, never how long it was,
                        // and the length differs by phase: thirty seconds to act,
                        // twenty to answer. So the first sight of a new deadline is
                        // its full length, and a bar has something to drain from.
                        // The tolerance keeps repeated states for the same deadline
                        // from re-baselining it to whatever is left.
                        if(Mathf.Abs(next-_deadlineAt)>0.75f) _windowSeconds=Mathf.Max(1f,next-Time.realtimeSinceStartup);
                        _deadlineAt=next;
                    }
                    _ready = true; var log = _openingLog; _openingLog = null;
                    _queueHeading = null; _queueDeadlineAt = -1f;
                    _match.ShowServerState(payload, log); if (_overlay != null && !_menuOpen) _overlay.SetActive(false); break;
                case "game:event": _match.ShowServerEvent(payload); break;
                case "action:rejected":
                    if (payload.Str("message") == "This seat was resumed somewhere else.") { _terminal = true; _ready = false; _socket.Dispose(); Fail("Your table is open on another device."); }
                    break;
            }
        }
        private void ShowLobby(string error = null) {
            if (_lobby == null) return;
            var mode = _lobby.Str("mode") ?? MatchLaunch.QueueMode;
            var queue = mode == "casual" || mode == "ranked";
            var players = _lobby.Arr("players") ?? new List<JsonValue>();
            // Casual deals at four, ranked at two. Saying which makes the wait legible.
            var target = mode == "ranked" ? 2 : 4;
            var text = queue
                ? "FINDING PLAYERS\n" + mode.ToUpperInvariant() + "  ·  " + players.Count + " of " + target + " seated\nTABLE " + _lobby.Str("code")
                : "PRIVATE TABLE  " + _lobby.Str("code");
            foreach (var player in players) text += "\n" + player.Str("name") + (player.Flag("host") ? "  · HOST" : "") + (!player.Flag("connected") ? "  · RECONNECTING" : player.Flag("ready") ? "  · READY" : "  · WAITING");
            Show(text + (error == null ? "" : "\n\n" + error));
            _queueHeading = queue && error == null && _queueDeadlineAt >= 0 ? text : null;
            if (!queue && _lobby.Get("you")?.Flag("host") == true) {
                AddButton("Start match", new Vector2(-205,-150), () => HostStart(false));
                AddButton("Fill with bots", new Vector2(205,-150), () => HostStart(true));
            }
            AddButton(queue ? "Cancel search" : "Leave table", new Vector2(0,-265), Leave);
        }
        public void ShowMenu() {
            _menuOpen = true;
            if (!Connected) { Fail("Reconnecting to your table…", true); return; }
            Show("ONLINE MATCH\nYour seat is saved when you return to the lobby. Leaving abandons your seat.");
            AddButton("Continue",new Vector2(0,-120),()=>{_menuOpen=false;_overlay.SetActive(false);});
            AddButton("Back to lobby",new Vector2(-205,-245),()=>_match.ReturnToLobby());
            AddButton("Leave match",new Vector2(205,-245),()=>{
                Show("LEAVE MATCH?\nYou will abandon this seat. You can start a new match afterwards.");
                AddButton("Cancel",new Vector2(-205,-185),ShowMenu);
                AddButton("Leave",new Vector2(205,-185),Leave);
            });
        }
        private async void Leave() {
            if (_sending) return; _sending = true;
            try { if (_socket.IsOpen) await Ack("lobby:leave", JsonValue.FromObject()); }
            catch { /* Keep server recovery available when an explicit leave could not be confirmed. */ }
            if (!_disposed) _match.ReturnToLobby();
        }
        private void Fail(string message, bool retry = false) {
            if (_disposed) return; _ready = false; Show(message);
            if (retry) AddButton("Try again", new Vector2(-205,-185), () => { _terminal = false; Reconnect(); });
            AddButton("Back to lobby", new Vector2(retry ? 205 : 0,-185), () => _match.ReturnToLobby());
        }
        private void Show(string text) {
            if (_disposed) return;
            if (_overlay == null) {
                // Never pick an arbitrary Canvas: ScreenTransition is persistent and fades
                // its CanvasGroup to zero, hiding any network UI parented beneath it.
                Canvas canvas = null;
                foreach (var table in UnityEngine.Object.FindObjectsByType<TableView>(FindObjectsSortMode.None))
                    if (table.match == _match) { canvas = table.GetComponentInParent<Canvas>(); break; }
                if (canvas == null) return;
                var dim = UIFactory.Rect("NetworkOverlay",canvas.transform,Vector2.zero,Vector2.zero);
                dim.anchorMin = Vector2.zero; dim.anchorMax = Vector2.one; dim.offsetMin = dim.offsetMax = Vector2.zero;
                _overlay = dim.gameObject; var image = _overlay.AddComponent<Image>(); image.color = new Color(0.01f,0.02f,0.04f,.92f);
                _card = UIFactory.Rect("Card",dim,new Vector2(920,900),Vector2.zero);
                var bg = _card.gameObject.AddComponent<Image>(); bg.color = UIFactory.Navy;
                PanelTheme.Surface(bg);
                _message = UIFactory.Text("Message",_card,text,32,new Vector2(820,500),new Vector2(0,160)); _message.richText = false;
            }
            for (int i=_card.childCount-1;i>=0;i--) { var child = _card.GetChild(i); if(child.GetComponent<Button>()!=null) { child.gameObject.SetActive(false); UnityEngine.Object.Destroy(child.gameObject); } }
            _queueHeading = null;
            _message.text = text; _overlay.SetActive(true); _overlay.transform.SetAsLastSibling();
        }
        private void AddButton(string label, Vector2 position, Action action) {
            if (_card == null) return;
            var button = UIFactory.Button(label,_card,null,new Vector2(380,85),position);
            button.GetComponent<Image>().color = new Color(.16f,.21f,.29f);
            PanelTheme.Button(button);
            UIFactory.Text("Label",button.transform,label.ToUpperInvariant(),28,new Vector2(350,75),Vector2.zero);
            button.onClick.AddListener(() => action());
        }
        public void Dispose() {
            if (_disposed) return; _disposed = true; _life.Cancel();
            _socket.EventReceived -= OnSocketEvent; _socket.Closed -= OnClosed; _socket.Dispose();
            while (_main.TryDequeue(out _)) { }
            if (_overlay != null) UnityEngine.Object.Destroy(_overlay);
        }
    }
}
