namespace Coup.Game
{
    /// <summary>
    /// How the lobby asked the table to open. The match scene reads it once on load.
    /// </summary>
    public static class MatchLaunch
    {
        public const string GameScene = "Game";
        public const string LobbyScene = "Lobby";

        public static string QueueMode { get; private set; } = "";
        public static bool ResumeRequested { get; private set; }
        public static void Queue(string mode) { Host(); QueueMode = mode == "ranked" ? "ranked" : "casual"; }
        public static void Resume() { Host(); ResumeRequested = true; }

        public static bool TutorialRequested { get; set; }

        public static bool Pending { get; private set; }
        public static bool Online { get; private set; }
        public static bool AutoStart { get; private set; } = true;
        public static string JoinCode { get; private set; } = "";

        public static void Tutorial() { Local(); TutorialRequested = true; }

        public static void Local()
        {
            TutorialRequested = false;
            QueueMode = "";
            ResumeRequested = false;
            Pending = true;
            Online = false;
            AutoStart = true;
            JoinCode = "";
        }

        public static void Host()
        {
            TutorialRequested = false;
            QueueMode = "";
            ResumeRequested = false;
            Pending = true;
            Online = true;
            AutoStart = false;
            JoinCode = "";
        }

        public static void Join(string code)
        {
            TutorialRequested = false;
            QueueMode = "";
            ResumeRequested = false;
            Pending = true;
            Online = true;
            AutoStart = true;
            JoinCode = code ?? "";
        }

        public static void Consume(out bool online, out bool autoStart, out string joinCode)
        {
            online = Online;
            autoStart = AutoStart;
            joinCode = JoinCode ?? "";
            Pending = false;
        }
    }
}
