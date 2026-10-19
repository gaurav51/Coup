using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Coup.Game.UI
{
    /// <summary>
    /// Home screen. Play and This Device deal a match on this phone.
    /// Friends hosts a table or joins one with a code, then opens the table scene.
    /// </summary>
    public sealed class LobbyScreen : MonoBehaviour
    {
        private GameObject _friends;
        private TMP_InputField _code;
        private GameObject _information;
        private TMP_Text _informationTitle, _informationBody, _audioLabel;
        private GameObject _audio;
        private GameObject _settingsFriends, _settingsRules;

        private void Awake()
        {
            _friends = transform.Find("FriendsSheet")?.gameObject;
            _code = transform.Find("FriendsSheet/Card/Code")?.GetComponent<TMP_InputField>();
            if (_friends != null) _friends.SetActive(false);
            _information = transform.Find("InformationSheet")?.gameObject;
            _informationTitle = transform.Find("InformationSheet/Card/Title")?.GetComponent<TMP_Text>();
            _informationBody = transform.Find("InformationSheet/Card/Body")?.GetComponent<TMP_Text>();
            _audio = transform.Find("InformationSheet/Card/Audio")?.gameObject;
            _audioLabel = transform.Find("InformationSheet/Card/Audio/Label")?.GetComponent<TMP_Text>();
            _settingsFriends = transform.Find("InformationSheet/Card/SettingsFriends")?.gameObject;
            _settingsRules = transform.Find("InformationSheet/Card/SettingsRules")?.gameObject;
            if (_information != null) _information.SetActive(false);
            AudioListener.volume = PlayerPrefs.GetInt("Coup.AudioMuted", 0) == 1 ? 0f : 1f;

            Wire("Actions/Play", PlayHere);
            Wire("Actions/ThisDevice", PlayHere);
            Wire("Actions/Friends", ShowFriends);
            Wire("Actions/Table", ShowFriends);
            Wire("FriendsSheet/Dim", HideFriends);
            Wire("FriendsSheet/Card/Host", Host);
            Wire("FriendsSheet/Card/Join", Join);
            Wire("FriendsSheet/Card/Back", HideFriends);
            Wire("Actions/Rules", ShowRules);
            Wire("Actions/Roles", ShowRoles);
            Wire("Actions/Court", ShowRoles);
            Wire("Actions/Settings", ShowSettings);
            Wire("Hud/Settings", ShowSettings);
            Wire("InformationSheet/Dim", HideInformation);
            Wire("InformationSheet/Card/Back", HideInformation);
            Wire("InformationSheet/Card/Audio", ToggleAudio);
            Wire("InformationSheet/Card/SettingsFriends", ShowFriends);
            Wire("InformationSheet/Card/SettingsRules", ShowRules);
            Wire("Actions/Casual", PlayHere);
            Wire("Actions/Collection", ShowRoles);
            Wire("Actions/Ranked", ShowRanked);
            Wire("Actions/RankedTier", ShowRanked);
            Wire("Actions/SeasonPass", () => ShowInformation("SEASON PASS", "Coming soon.\n\nSeason rewards and progression are not available yet."));
            Wire("Actions/Event", () => ShowInformation("CROWN OF NATIONS", "Coming soon.\n\nThis event is not available yet."));
            Wire("Actions/Shop", () => ShowInformation("SHOP", "Coming soon.\n\nPurchases and premium currency are not available yet."));
            Wire("Actions/Missions", () => ShowInformation("MISSIONS", "Coming soon.\n\nMission rewards are not available yet."));
            Wire("Hud/Gems", () => ShowInformation("GEMS", "Premium currency is not available yet."));
            Wire("Hud/Currency", () => ShowInformation("COINS", "The lobby currency display is a UI placeholder. Purchases and rewards are not available yet."));
            Wire("Hud/Profile", () => ShowInformation("PLAYER", "Your profile\n\nAccount progression is coming soon. You can already play a local match with Casual or create a private table from Settings."));
        }

        private void ShowInformation(string title, string body, bool settings = false)
        {
            HideFriends();
            if (_information == null) return;
            _informationTitle.text = title;
            _informationBody.text = body;
            _audio.SetActive(settings);
            if (_settingsFriends != null) _settingsFriends.SetActive(settings);
            if (_settingsRules != null) _settingsRules.SetActive(settings);
            UpdateAudioLabel();
            _information.SetActive(true);
            _information.transform.SetAsLastSibling();
        }

        private void HideInformation() { if (_information != null) _information.SetActive(false); }

        private void ShowRules()
        {
            ShowInformation("HOW TO PLAY", "Be the last player with influence.\n\nBegin with two hidden role cards and two coins. On your turn, choose an action. You may claim any role, even one you do not hold.\n\nINCOME  +1 coin\nFOREIGN AID  +2 coins; the Duke can block.\nCOUP  Pay 7 coins to remove influence. At 10 coins, you must coup.\n\nChallenge a role claim if you suspect a bluff. A failed challenge costs influence; a successful one exposes the bluffer.\n\nLose both influence cards and you are out.");
        }

        private void ShowRoles()
        {
            ShowInformation("THE FIVE ROLES", "<color=#D7B565>DUKE</color>\nTake 3 coins. Blocks foreign aid.\n\n<color=#D7B565>CAPTAIN</color>\nSteal 2 coins. Blocks stealing.\n\n<color=#D7B565>ASSASSIN</color>\nPay 3 coins to assassinate. Can be blocked by the Contessa.\n\n<color=#D7B565>CONTESSA</color>\nBlocks assassination.\n\n<color=#D7B565>AMBASSADOR</color>\nExchange cards. Blocks stealing.");
        }

        private void ShowSettings()
        {
            ShowInformation("SETTINGS", "Music and sound effects.\n\nHost a private table, join friends with a code, or read the rules.", true);
        }

        private void ShowRanked() => ShowInformation("RANKED", "Coming soon.\n\nCompetitive matchmaking and ranked progression are not available yet. Choose Casual to play a local match.");

        private void ToggleAudio()
        {
            AudioListener.volume = AudioListener.volume > 0f ? 0f : 1f;
            PlayerPrefs.SetInt("Coup.AudioMuted", AudioListener.volume == 0f ? 1 : 0);
            PlayerPrefs.Save();
            UpdateAudioLabel();
        }

        private void UpdateAudioLabel()
        {
            if (_audioLabel != null) _audioLabel.text = AudioListener.volume > 0f ? "SOUND: ON" : "SOUND: OFF";
        }

        private void PlayHere()
        {
            MatchLaunch.Local();
            SceneManager.LoadScene(MatchLaunch.GameScene);
        }

        private void ShowFriends()
        {
            HideInformation();
            if (_friends != null) _friends.SetActive(true);
        }

        private void HideFriends()
        {
            if (_friends != null) _friends.SetActive(false);
        }

        private void Host()
        {
            MatchLaunch.Host();
            SceneManager.LoadScene(MatchLaunch.GameScene);
        }

        private void Join()
        {
            var code = _code != null ? _code.text.Trim() : "";
            if (code.Length == 0) return;
            MatchLaunch.Join(code);
            SceneManager.LoadScene(MatchLaunch.GameScene);
        }

        private void Wire(string path, UnityEngine.Events.UnityAction action)
        {
            var child = transform.Find(path);
            if (child == null) return;
            var button = child.GetComponent<Button>();
            if (button == null) return;
            button.onClick.AddListener(action);
        }
    }
}
