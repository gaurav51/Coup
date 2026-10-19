using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace Coup.Net
{
    // Engine.IO 4 websocket transport. All callbacks are delivered by the owner on Unity's main thread.
    public sealed class SocketIoClient : IDisposable
    {
        private readonly Action<Action> _main;
        private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonValue>> _acks = new();
        private readonly SemaphoreSlim _send = new SemaphoreSlim(1, 1);
        private ClientWebSocket _socket;
        private CancellationTokenSource _life;
        private TaskCompletionSource<bool> _opened;
        private int _generation, _ackId;
        private string _accessToken;
        private DateTime _lastPacket;
        public bool IsOpen { get; private set; }
        public event Action<string, JsonValue> EventReceived;
        public event Action<string> Closed;
        public SocketIoClient(Action<Action> main) => _main = main ?? (_ => { });
        public async Task Connect(string serverUrl, string accessToken = null, CancellationToken cancellation = default) {
            Dispose();
            var generation = _generation;
            _accessToken = accessToken;
            _life = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            var life = _life;
            _opened = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var opened = _opened;
            var socket = _socket = new ClientWebSocket();
            try {
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(life.Token)) {
                    timeout.CancelAfter(15000);
                    await socket.ConnectAsync(SocketUri(serverUrl), timeout.Token);
                }
                _lastPacket = DateTime.UtcNow;
                _ = Receive(socket, generation, life.Token);
                var result = await Task.WhenAny(opened.Task, Task.Delay(12000, life.Token));
                if (result != opened.Task || !await opened.Task) throw new InvalidOperationException("The server did not accept the connection.");
                _ = Watchdog(generation, life.Token);
            } catch { if (generation == _generation) Dispose(); throw; }
        }
        public async Task<JsonValue> Emit(string name, JsonValue payload) {
            if (!IsOpen) throw new InvalidOperationException("Reconnecting. Please wait for your table.");
            var id = Interlocked.Increment(ref _ackId);
            var pending = new TaskCompletionSource<JsonValue>(TaskCreationOptions.RunContinuationsAsynchronously);
            _acks[id] = pending;
            try {
                var array = JsonValue.FromArray(new System.Collections.Generic.List<JsonValue> { JsonValue.FromString(name), payload ?? JsonValue.FromObject() });
                await Send("42" + id + array.Encode(), _generation);
                if (await Task.WhenAny(pending.Task, Task.Delay(12000)) != pending.Task) throw new TimeoutException("The server did not answer. Reconnecting to check your action.");
                return await pending.Task;
            } finally { _acks.TryRemove(id, out _); }
        }
        private async Task Receive(ClientWebSocket socket, int generation, CancellationToken token) {
            var buffer = new byte[16384];
            try {
                while (!token.IsCancellationRequested && socket.State == WebSocketState.Open) {
                    using (var message = new MemoryStream()) {
                        WebSocketReceiveResult result;
                        do {
                            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                            if (result.MessageType == WebSocketMessageType.Close) { Finish(generation, "Connection interrupted."); return; }
                            if (result.MessageType != WebSocketMessageType.Text || message.Length + result.Count > 1048576) throw new IOException("Invalid server message.");
                            message.Write(buffer, 0, result.Count);
                        } while (!result.EndOfMessage);
                        _lastPacket = DateTime.UtcNow;
                        var packet = Encoding.UTF8.GetString(message.ToArray());
                        if (packet == "2") { await Send("3", generation); continue; }
                        _main(() => { if (generation == _generation) OnPacket(packet, generation); });
                    }
                }
            } catch (OperationCanceledException) { if (!token.IsCancellationRequested) Finish(generation, "Connection interrupted. Reconnecting…"); }
            catch { Finish(generation, "Connection interrupted. Reconnecting…"); }
        }
        private async Task Watchdog(int generation, CancellationToken token) {
            try {
                while (!token.IsCancellationRequested && generation == _generation) {
                    await Task.Delay(5000, token);
                    if (DateTime.UtcNow - _lastPacket > TimeSpan.FromSeconds(65)) { Finish(generation, "Connection timed out. Reconnecting…"); return; }
                }
            } catch (OperationCanceledException) { }
        }
        private void OnPacket(string packet, int generation) {
            if (string.IsNullOrEmpty(packet)) return;
            try {
                if (packet[0] == '0') { _ = SendSafe("40" + JsonValue.FromObject(("accessToken", JsonValue.FromString(_accessToken))).Encode(), generation); return; }
                if (packet == "1" || packet == "41") { Finish(generation, "The server disconnected. Reconnecting…"); return; }
                if (packet.StartsWith("40", StringComparison.Ordinal)) { IsOpen = true; _opened.TrySetResult(true); return; }
                if (packet.StartsWith("44", StringComparison.Ordinal)) { Finish(generation, "Your session could not connect. Please sign in again."); return; }
                if (packet.Length < 3 || packet[0] != '4' || (packet[1] != '2' && packet[1] != '3')) return;
                var index = 2;
                while (index < packet.Length && char.IsDigit(packet[index])) index++;
                var id = index > 2 ? int.Parse(packet.Substring(2, index - 2)) : -1;
                var data = JsonValue.Parse(packet.Substring(index));
                if (data.Type != JsonValue.Kind.Array || data.Array.Count == 0) return;
                if (packet[1] == '3') { if (_acks.TryRemove(id, out var pending)) pending.TrySetResult(data.Array[0]); }
                else if (data.Array[0].Type == JsonValue.Kind.String) EventReceived?.Invoke(data.Array[0].String, data.Array.Count > 1 ? data.Array[1] : JsonValue.Null());
            } catch (FormatException) { Finish(generation, "The server sent an invalid response."); }
        }
        private async Task SendSafe(string text, int generation) { try { await Send(text, generation); } catch { Finish(generation, "Connection interrupted."); } }
        private async Task Send(string text, int generation) {
            var socket = _socket; var token = _life?.Token ?? CancellationToken.None;
            await _send.WaitAsync(token);
            try {
                if (generation != _generation || socket == null || socket.State != WebSocketState.Open) throw new IOException("Connection interrupted.");
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token)) {
                    timeout.CancelAfter(10000);
                    await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(text)), WebSocketMessageType.Text, true, timeout.Token);
                }
            } finally { _send.Release(); }
        }
        private void Finish(int generation, string reason) {
            _main(() => {
                if (generation != _generation) return;
                Dispose();
                Closed?.Invoke(reason);
            });
        }
        public void Dispose() {
            Interlocked.Increment(ref _generation); IsOpen = false;
            _opened?.TrySetResult(false);
            _life?.Cancel(); _socket?.Abort(); _socket?.Dispose(); _socket = null;
            _life?.Dispose(); _life = null;
            foreach (var pending in _acks.Values) pending.TrySetException(new IOException("Connection interrupted."));
            _acks.Clear();
        }
        public static Uri SocketUri(string serverUrl) {
            if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https" && uri.Scheme != "ws" && uri.Scheme != "wss")) throw new ArgumentException("Invalid server URL.");
            var builder = new UriBuilder(uri) { Scheme = uri.Scheme == "https" || uri.Scheme == "wss" ? "wss" : "ws", Path = "/socket.io/", Query = "EIO=4&transport=websocket" };
            return builder.Uri;
        }
    }
}
