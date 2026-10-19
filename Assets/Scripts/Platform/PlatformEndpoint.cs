using System;
using UnityEngine;
namespace Coup.Net
{
    /// <summary>
    /// Where the client looks for the server, in order:
    ///
    ///   1. <c>-coupServer &lt;url&gt;</c> on the command line, or the
    ///      <c>COUP_SERVER_URL</c> environment variable. Development and QA only —
    ///      ignored in a release player so a shipped build cannot be repointed.
    ///   2. A <c>PlatformEndpoint</c> asset in Resources. This is the build-time
    ///      setting: aim it at staging or production before building.
    ///   3. <see cref="Fallback"/>, compiled in, used only when no asset is present.
    ///
    /// The address used to be a const on PlatformClient, which bound every build
    /// permanently to one tunnel with no way to repoint it.
    /// </summary>
    [CreateAssetMenu(fileName = "PlatformEndpoint", menuName = "Coup/Platform Endpoint")]
    public sealed class PlatformEndpoint : ScriptableObject
    {
        public const string ResourcePath = "PlatformEndpoint";
        private const string Argument = "-coupServer";
        private const string Variable = "COUP_SERVER_URL";

        /// <summary>
        /// Used only when the asset is missing. Local development, so a build that
        /// lost its endpoint fails obviously here instead of quietly reaching
        /// whatever host happened to be compiled in last.
        /// </summary>
        public const string Fallback = "http://127.0.0.1:3001";

        [Tooltip("Base URL of the Coup server, for example https://play.example.com")]
        [SerializeField] private string serverUrl = Fallback;

        public string ServerUrl => serverUrl;

        public static string Resolve()
        {
            var requested = Override();
            if (IsValid(requested)) return requested.TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(requested))
                Debug.LogWarning($"[Coup] Ignoring server override '{requested}': not an absolute http or https URL.");

            var asset = Resources.Load<PlatformEndpoint>(ResourcePath);
            if (asset == null)
            {
                Debug.LogWarning($"[Coup] No {ResourcePath} asset in Resources; using {Fallback}.");
                return Fallback;
            }
            if (IsValid(asset.serverUrl)) return asset.serverUrl.TrimEnd('/');
            Debug.LogWarning($"[Coup] {ResourcePath} holds an invalid URL; using {Fallback}.");
            return Fallback;
        }

        /// <summary>The override is a development affordance, so release players ignore it.</summary>
        private static string Override()
        {
            if (!Debug.isDebugBuild && !Application.isEditor) return null;
            try
            {
                var args = Environment.GetCommandLineArgs();
                for (var i = 0; i < args.Length - 1; i++)
                    if (string.Equals(args[i], Argument, StringComparison.Ordinal)) return args[i + 1];
                return Environment.GetEnvironmentVariable(Variable);
            }
            catch { return null; }   // some platforms deny arguments, the environment, or both
        }

        public static bool IsValid(string value) =>
            !string.IsNullOrWhiteSpace(value)
            && Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
