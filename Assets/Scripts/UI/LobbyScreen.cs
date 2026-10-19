using UnityEngine;

namespace Coup.Game.UI
{
    /// <summary>Bootstraps the authored lobby and its server-backed panels.</summary>
    public sealed class LobbyScreen : MonoBehaviour
    {
        public CoupArt art;
        private void Awake()
        {
            foreach (var name in new[] { "FriendsSheet", "InformationSheet" }) {
                var sheet=transform.Find(name); if(sheet!=null)sheet.gameObject.SetActive(false);
            }
            AudioListener.volume=PlayerPrefs.GetInt("Coup.AudioMuted",0)==1?0f:1f;
            if(GetComponent<PlatformPanels>()==null)gameObject.AddComponent<PlatformPanels>();
        }
    }
}
