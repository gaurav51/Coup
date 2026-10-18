using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Coup.Core;
using Coup.Game.UI;
using Coup.Net;
using UnityEngine;
using UnityEngine.UI;

namespace Coup.Game
{
    /// <summary>
    /// Connects this table to the Socket.io server and keeps the board in step
    /// with the private <c>game:state</c> snapshots. Moves go out as socket events.
    /// </summary>
    public sealed class OnlineSession : IDisposable
    {
        private readonly MatchController _match;
        private readonly ConcurrentQueue<Action> _main = new ConcurrentQueue<Action>();
        private readonly SocketIoClient _socket;
        private List<JsonValue> _openingLog;
        private GameObject _banner;

        public OnlineSession(MatchController match)
        {
            _match = match;
            _socket = new SocketIoClient(_main.Enqueue);
            _socket.EventReceived += OnSocketEvent;
            _socket.Closed += message => _main.Enqueue(() => Show(message));
        }

        public void Pump()
        {
            while (_main.TryDequeue(out var action)) action();
        }

        public async void Begin()
        {
            try
            {
                Show("Connecting to the Coup server…");
                await _socket.Connect(_match.serverUrl);
                _main.Enqueue(OpenTable);
            }
            catch (Exception ex)
            {
                _main.Enqueue(() => Fail(ex.Message));
            }
        }

        public void Declare(ActionKind kind, int target)
        {
            var payload = JsonValue.FromObject(("action", JsonValue.FromString(kind.ToString())));
            if (target >= 0) payload.Object["target"] = JsonValue.FromNumber(target);
            Send("game:declare", payload);
        }

        public void Challenge() => Send("game:challenge", JsonValue.FromObject());
        public void Pass() => Send("game:pass", JsonValue.FromObject());
        public void Block(Role role) => Send("game:block", JsonValue.FromObject(("role", JsonValue.FromString(role.ToString()))));
        public void Lose(Role role) => Send("game:lose", JsonValue.FromObject(("role", JsonValue.FromString(role.ToString()))));

        public void Exchange(IReadOnlyList<Role> keep)
        {
            var cards = new List<JsonValue>();
            for (var i = 0; i < keep.Count; i++) cards.Add(JsonValue.FromString(keep[i].ToString()));
            Send("game:exchange", JsonValue.FromObject(("keep", JsonValue.FromArray(cards))));
        }

        public void Dispose()
        {
            _socket.EventReceived -= OnSocketEvent;
            _socket.Dispose();
            if (_banner != null) UnityEngine.Object.Destroy(_banner);
        }

        private async void OpenTable()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_match.joinCode))
                {
                    await Join(_match.joinCode.Trim());
                    Show($"Joined {_match.joinCode.Trim()}.\nWaiting for the host to start.");
                    return;
                }

                var created = await Ack("lobby:create", JsonValue.FromObject(
                    ("name", JsonValue.FromString(CleanName()))));
                var code = created.Str("code");
                Show($"Table {code}");
                Debug.Log($"[Coup] Online table {code} at {_match.serverUrl}");

                if (!_match.autoStartOnline)
                {
                    Show($"Table {code}\nPress Start when everyone has joined.");
                    AddButton("Start", "START MATCH", new Vector2(-190f, -140f), StartHostedMatch);
                    AddButton("Offline", "PLAY HERE", new Vector2(190f, -140f), () =>
                    {
                        if (_banner != null) _banner.SetActive(false);
                        _match.PlayOffline();
                    });
                    return;
                }

                await ReadyAndStart();
            }
            catch (Exception ex)
            {
                Fail(ex.Message);
            }
        }

        private async void StartHostedMatch()
        {
            try
            {
                await ReadyAndStart();
            }
            catch (Exception ex)
            {
                Fail(ex.Message);
            }
        }

        private async System.Threading.Tasks.Task Join(string code)
        {
            await Ack("lobby:join", JsonValue.FromObject(
                ("code", JsonValue.FromString(code)),
                ("name", JsonValue.FromString(CleanName()))));
            await Ack("lobby:ready", JsonValue.FromObject(("ready", JsonValue.FromBool(true))));
        }

        private async System.Threading.Tasks.Task ReadyAndStart()
        {
            await Ack("lobby:ready", JsonValue.FromObject(("ready", JsonValue.FromBool(true))));
            var bots = Mathf.Max(0, _match.playerCount - 1);
            await Ack("lobby:start", JsonValue.FromObject(("bots", JsonValue.FromNumber(bots))));
        }

        private async System.Threading.Tasks.Task<JsonValue> Ack(string name, JsonValue payload)
        {
            var response = await _socket.Emit(name, payload);
            if (response == null || response.Type != JsonValue.Kind.Object || !response.Flag("ok"))
            {
                var message = response != null ? response.Str("error") : null;
                throw new InvalidOperationException(string.IsNullOrEmpty(message) ? "The server rejected that." : message);
            }
            return response;
        }

        private async void Send(string name, JsonValue payload)
        {
            try
            {
                await Ack(name, payload);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Coup] {ex.Message}");
                _match.Rejected();
            }
        }

        private void OnSocketEvent(string name, JsonValue payload)
        {
            switch (name)
            {
                case "game:log":
                    _openingLog = payload?.Arr("events");
                    break;
                case "game:state":
                    var log = _openingLog;
                    _openingLog = null;
                    _match.ShowServerState(payload, log);
                    if (_banner != null) _banner.SetActive(false);
                    break;
                case "game:event":
                    _match.ShowServerEvent(payload);
                    break;
                case "action:rejected":
                    Debug.LogWarning($"[Coup] {payload?.Str("message")}");
                    break;
            }
        }

        private string CleanName()
        {
            var name = string.IsNullOrWhiteSpace(_match.onlineName) ? "You" : _match.onlineName.Trim();
            return name.Length > 16 ? name.Substring(0, 16) : name;
        }

        private void Fail(string message)
        {
            Debug.LogWarning($"[Coup] {message}");
            Show(message + "\n\nIn the server folder, run npm start.\nOr play this match on this device.");
            AddButton("Offline", "PLAY HERE", Vector2.zero + new Vector2(0f, -140f), () =>
            {
                if (_banner != null) _banner.SetActive(false);
                _match.PlayOffline();
            });
        }

        private void Show(string message)
        {
            var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            if (_banner == null)
            {
                var panel = UIFactory.Rect("OnlineBanner", canvas.transform, new Vector2(920, 420), Vector2.zero);
                var image = panel.gameObject.AddComponent<Image>();
                image.color = new Color(0.07f, 0.09f, 0.14f, 0.94f);
                image.raycastTarget = true;
                _banner = panel.gameObject;
            }

            _banner.SetActive(true);
            _banner.transform.SetAsLastSibling();
            var label = _banner.transform.Find("Message")?.GetComponent<TMPro.TextMeshProUGUI>();
            if (label == null)
            {
                label = UIFactory.Text("Message", _banner.transform, message, 32, new Vector2(820, 280), new Vector2(0, 40));
                label.alignment = TMPro.TextAlignmentOptions.Center;
            }
            else label.text = message;
        }

        private void AddButton(string name, string caption, Vector2 position, Action click)
        {
            if (_banner == null || _banner.transform.Find(name) != null) return;
            var button = UIFactory.Button(name, _banner.transform, null, new Vector2(330, 90), position);
            button.GetComponent<Image>().color = UIFactory.Gold;
            var text = UIFactory.Text("Label", button.transform, caption, 28, new Vector2(300, 70), Vector2.zero);
            text.color = new Color(0.12f, 0.09f, 0.05f);
            text.fontStyle = TMPro.FontStyles.Bold;
            button.onClick.AddListener(() => click());
        }
    }
}
