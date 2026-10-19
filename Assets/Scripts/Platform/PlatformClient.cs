using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
namespace Coup.Net
{
    public sealed class ApiException : Exception
    {
        public readonly long Status;
        public ApiException(long status, string message) : base(message) { Status = status; }
    }
    public sealed class PlatformClient : MonoBehaviour
    {
        private static PlatformClient _instance;
        public static PlatformClient Instance {
            get {
                if (_instance == null) { var go = new GameObject("CoupPlatform"); _instance = go.AddComponent<PlatformClient>(); DontDestroyOnLoad(go); }
                return _instance;
            }
        }
        // Resolved in Awake: an override, else the PlatformEndpoint asset, else local.
        public string BaseUrl { get; private set; } = PlatformEndpoint.Fallback;
        public JsonValue Profile { get; private set; }
        public string AccountId => _credentials?.Str("accountId");
        public event Action Changed;
        private JsonValue _credentials;
        private bool _storageError;
        private string _accessToken;
        private DateTime _expires;
        private readonly SemaphoreSlim _auth = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _life = new CancellationTokenSource();
        public bool HasAccount => _credentials != null;
        private void Awake() {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            BaseUrl = PlatformEndpoint.Resolve();
            try { var saved = SessionVault.Read(); if (!string.IsNullOrEmpty(saved)) _credentials = JsonValue.Parse(saved); }
            catch { _storageError = true; }
        }
        private void OnDestroy() { _life.Cancel(); if (_instance == this) _instance = null; }
        public async Task<string> AccessToken(bool force = false) {
            if (_storageError) throw new InvalidOperationException("Secure account storage is unavailable. Unlock or restart your device before signing in.");
            await _auth.WaitAsync(_life.Token);
            try {
                if (!force && !string.IsNullOrEmpty(_accessToken) && _expires > DateTime.UtcNow.AddSeconds(60)) return _accessToken;
                JsonValue response;
                if (_credentials == null) response = await Raw("POST", "/v1/auth/guest", JsonValue.FromObject(("name", JsonValue.FromString("Player"))), null, _life.Token);
                else response = await Raw("POST", "/v1/auth/refresh", _credentials, null, _life.Token);
                AcceptSession(response); return _accessToken;
            } finally { _auth.Release(); }
        }
        public void AcceptSession(JsonValue response) {
            var credentials = JsonValue.FromObject(("sessionId", JsonValue.FromString(response.Str("sessionId"))),
                ("refreshToken", JsonValue.FromString(response.Str("refreshToken"))), ("accountId", JsonValue.FromString(response.Str("accountId"))));
            SessionVault.Write(credentials.Encode()); // Do not replace the previous session until secure storage succeeds.
            _credentials = credentials; _accessToken = response.Str("accessToken");
            _expires = DateTime.UtcNow.AddSeconds(response.Int("expiresIn", 0));
            if (response.Get("profile") != null) SetProfile(response.Get("profile"));
        }
        public void SetProfile(JsonValue value) { Profile = value; Changed?.Invoke(); }
        public async Task<JsonValue> Request(string method, string path, JsonValue body = null, CancellationToken token = default) {
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _life.Token)) {
                var access = await AccessToken();
                JsonValue result;
                try { result = await Raw(method, path, body, access, linked.Token); }
                catch (ApiException error) when (error.Status == 401) {
                    access = await AccessToken(true);
                    result = await Raw(method, path, body, access, linked.Token);
                }
                if (result.Get("profile") != null) SetProfile(result.Get("profile"));
                return result;
            }
        }
        public Task<JsonValue> PublicRequest(string path, JsonValue body, CancellationToken token) => Raw("POST", path, body, null, token);
        public async Task RefreshProfile(CancellationToken token = default) => await Request("GET", "/v1/profile", null, token);
        public async Task SignOut() {
            if (HasAccount) await Request("POST", "/v1/auth/logout", JsonValue.FromObject());
            Forget();
        }
        public void Forget() { SessionVault.Write(null); _credentials = null; _accessToken = null; Profile = null; _expires = default; Changed?.Invoke(); }
        private async Task<JsonValue> Raw(string method, string path, JsonValue body, string token, CancellationToken cancellation) {
            cancellation.ThrowIfCancellationRequested();
            using (var request = new UnityWebRequest(BaseUrl.TrimEnd('/') + path, method)) {
                request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 15;
                request.SetRequestHeader("ngrok-skip-browser-warning", "1");
                if (token != null) request.SetRequestHeader("Authorization", "Bearer " + token);
                if (method != "GET" && method != "DELETE") {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes((body ?? JsonValue.FromObject()).Encode()));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                var operation = request.SendWebRequest();
                while (!operation.isDone) {
                    if (cancellation.IsCancellationRequested) { request.Abort(); cancellation.ThrowIfCancellationRequested(); }
                    await Task.Yield();
                }
                cancellation.ThrowIfCancellationRequested();
                if (request.result == UnityWebRequest.Result.ConnectionError) throw new ApiException(0, "Unable to reach the server. Check your connection and try again.");
                JsonValue result;
                try { result = JsonValue.Parse(request.downloadHandler.text); }
                catch { throw new ApiException(request.responseCode, "The server returned an unexpected response. Please try again."); }
                if (request.responseCode >= 400 || !result.Flag("ok")) throw new ApiException(request.responseCode, result.Str("error") ?? "The request could not be completed.");
                return result;
            }
        }
    }
}
