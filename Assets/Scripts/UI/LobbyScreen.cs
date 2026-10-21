using UnityEngine;

namespace Coup.Game.UI
{
    /// <summary>Bootstraps the authored lobby and its server-backed panels.</summary>
    public sealed class LobbyScreen : MonoBehaviour
    {
        public CoupArt art;
        public Sprite loginPanel, loginButtonFill, loginButtonFrame;
        private void Awake()
        {
            foreach (var name in new[] { "FriendsSheet", "InformationSheet" }) {
                var sheet=transform.Find(name); if(sheet!=null)sheet.gameObject.SetActive(false);
            }
            if(GetComponent<LobbyResponsiveLayout>()==null)gameObject.AddComponent<LobbyResponsiveLayout>();
            AudioDirector.Ensure();   // boots the mix and starts the lobby theme
            if(GetComponent<LoginScreen>()==null)gameObject.AddComponent<LoginScreen>();
            if(GetComponent<PlatformPanels>()==null)gameObject.AddComponent<PlatformPanels>();
            if(GetComponent<LobbyMotion>()==null)gameObject.AddComponent<LobbyMotion>();
        }
    }
}
