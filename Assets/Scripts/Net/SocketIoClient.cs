using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Coup.Net
{
    /// <summary>
    /// A Socket.io client over a single websocket. Engine.IO 4, default namespace.
    /// Callbacks are delivered on the main thread through the queue passed in.
    /// </summary>
    public sealed class SocketIoClient : IDisposable
    {
        private readonly Action<Action> _main;
        private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonValue>> _acks = new();
        private readonly SemaphoreSlim _send = new SemaphoreSlim(1, 1);
        private ClientWebSocket _socket;
        private CancellationTokenSource _life;
        private TaskCompletionSource<bool> _opened;
        private int _ackId;

        public bool IsOpen { get; private set; }
        public event Action<string, JsonValue> EventReceived;
        public event Action<string> Closed;

        public SocketIoClient(Action<Action> main) => _main = main ?? (_ => { });

        public async Task Connect(string serverUrl)
        {
            DisposeSocket();
            _life = new CancellationTokenSource();
            _opened = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _socket = new ClientWebSocket();
            await _socket.ConnectAsync(SocketUri(serverUrl), _life.Token);
            _ = Receive(_life.Token);

            var opened = await Task.WhenAny(_opened.Task, Task.Delay(8000, _life.Token));
            if (opened != _opened.Task || !_opened.Task.Result)
                throw new InvalidOperationException("The Coup server did not accept the connection.");
        }

        public async Task<JsonValue> Emit(string name, JsonValue payload)
        {
            if (!IsOpen) throw new InvalidOperationException("Not connected to the Coup server.");

            var id = Interlocked.Increment(ref _ackId);
            var pending = new TaskCompletionSource<JsonValue>(TaskCreationOptions.RunContinuationsAsynchronously);
            _acks[id] = pending;

            var body = payload == null
                ? "[\"" + name + "\"]"
                : "[\"" + name + "\"," + payload.Encode() + "]";
            _main(() => Debug.Log("[Coup] -> " + body));
            await Send("42" + id + body);

            var finished = await Task.WhenAny(pending.Task, Task.Delay(8000));
            if (finished != pending.Task)
            {
                _acks.TryRemove(id, out _);
                throw new TimeoutException("The Coup server did not answer.");
            }
            return await pending.Task;
        }

        public void Dispose()
        {
            IsOpen = false;
            DisposeSocket();
        }

        private async Task Receive(CancellationToken token)
        {
            var buffer = new byte[16384];
            try
            {
                while (_socket != null && _socket.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    var message = new StringBuilder();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            Finish("The server closed the connection.");
                            return;
                        }
                        message.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    }
                    while (!result.EndOfMessage);

                    var text = message.ToString();
                    _main(() => OnPacket(text));
                }
            }
            catch (OperationCanceledException)
            {
                Finish(null);
            }
            catch (Exception ex)
            {
                Finish(ex.Message);
            }
        }

        private void OnPacket(string packet)
        {
            if (string.IsNullOrEmpty(packet)) return;
            if (packet[0] == '0')
            {
                _ = Send("40");
                return;
            }
            if (packet == "2")
            {
                _ = Send("3");
                return;
            }
            if (packet.StartsWith("40", StringComparison.Ordinal))
            {
                IsOpen = true;
                _opened?.TrySetResult(true);
                return;
            }
            if (packet.StartsWith("44", StringComparison.Ordinal))
            {
                Finish(packet.Length > 2 ? packet.Substring(2) : "Connection rejected.");
                return;
            }

            // 42 = event, 43 = ack. An optional numeric id sits between the type and the JSON.
            if (packet.Length < 2 || packet[0] != '4' || (packet[1] != '2' && packet[1] != '3')) return;
            var isAck = packet[1] == '3';
            var index = 2;
            var id = -1;
            if (index < packet.Length && char.IsDigit(packet[index]))
            {
                var start = index;
                while (index < packet.Length && char.IsDigit(packet[index])) index++;
                id = int.Parse(packet.Substring(start, index - start));
            }
            if (index >= packet.Length) return;

            var json = packet.Substring(index);
            Debug.Log("[Coup] <- " + json);
            JsonValue data;
            try { data = JsonValue.Parse(json); }
            catch (FormatException) { return; }

            if (isAck)
            {
                if (_acks.TryRemove(id, out var pending))
                    pending.TrySetResult(Unwrap(data));
                return;
            }

            if (data.Type != JsonValue.Kind.Array || data.Array == null || data.Array.Count == 0) return;
            var name = data.Array[0].Type == JsonValue.Kind.String ? data.Array[0].String : null;
            var payload = data.Array.Count > 1 ? data.Array[1] : JsonValue.Null();
            if (!string.IsNullOrEmpty(name)) EventReceived?.Invoke(name, payload);
        }

        private static JsonValue Unwrap(JsonValue data)
        {
            if (data.Type == JsonValue.Kind.Array && data.Array != null && data.Array.Count > 0)
                return data.Array[0];
            return data;
        }

        private async Task Send(string text)
        {
            var socket = _socket;
            if (socket == null || socket.State != WebSocketState.Open) return;
            var bytes = Encoding.UTF8.GetBytes(text);
            await _send.WaitAsync();
            try
            {
                if (socket.State == WebSocketState.Open)
                    await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            finally
            {
                _send.Release();
            }
        }

        private void Finish(string reason)
        {
            if (!IsOpen && _opened != null && !_opened.Task.IsCompleted)
                _opened.TrySetResult(false);
            IsOpen = false;
            foreach (var pending in _acks.Values) pending.TrySetCanceled();
            _acks.Clear();
            if (!string.IsNullOrEmpty(reason)) _main(() => Closed?.Invoke(reason));
        }

        private void DisposeSocket()
        {
            try { _life?.Cancel(); } catch (ObjectDisposedException) { }
            try
            {
                if (_socket != null && _socket.State == WebSocketState.Open)
                    _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            }
            catch (Exception) { }
            _socket?.Dispose();
            _socket = null;
            _life?.Dispose();
            _life = null;
        }

        public static Uri SocketUri(string serverUrl)
        {
            var root = (serverUrl ?? "").Trim().TrimEnd('/');
            if (root.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                root = "wss://" + root.Substring("https://".Length);
            else if (root.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                root = "ws://" + root.Substring("http://".Length);
            else if (!root.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) &&
                     !root.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
                root = "ws://" + root;

            if (root.IndexOf("socket.io", StringComparison.OrdinalIgnoreCase) < 0)
                root += "/socket.io/?EIO=4&transport=websocket";
            return new Uri(root);
        }
    }
}
