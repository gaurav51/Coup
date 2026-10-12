using System;
using System.Linq;
using TMPro;
using UnityEngine;
using Coup.Core;

namespace Coup.Game
{
    /// <summary>
    /// Every sprite and bit of card copy in one asset, so the scene never hunts for
    /// files and the five roles stay consistent wherever they are drawn.
    /// </summary>
    [CreateAssetMenu(fileName = "CoupArt", menuName = "Coup/Art Library")]
    public class CoupArt : ScriptableObject
    {
        [Serializable]
        public class RoleArt
        {
            public Role role;
            public string displayName;
            [TextArea] public string ability;
            public Color accent = Color.white;
            public Sprite cardFace;   // original art, captions baked in
            public Sprite portrait;   // generated cut-out used on the card prefab
            public Sprite backdrop;   // faction environment used as the card's background
        }

        [Serializable]
        public class ActionArt
        {
            public ActionKind action;
            public string label;
            public string subtitle;
            public Sprite icon;
        }

        [Header("Roles")]
        public RoleArt[] roles = Array.Empty<RoleArt>();

        [Header("Actions")]
        public ActionArt[] actions = Array.Empty<ActionArt>();

        [Header("Cards")]
        public Sprite cardBack;
        public Sprite opponentCardBack;
        public Sprite drawDeck;
        public Sprite discardDeck;

        [Header("Table")]
        public Sprite tableBackground;
        public Sprite coin;
        public Sprite coinPile;

        [Header("Generated")]
        public Sprite roundedPanel;   // 9-slice fill
        public Sprite roundedBorder;  // 9-slice outline
        public Sprite bottomScrim;
        public Sprite vfxSpark;       // soft round particle
        public Sprite vfxRing;        // expanding impact ring

        [Header("Panels")]
        public Sprite opponentFrame;
        public Sprite localHudFrame;
        public Sprite roundBanner;
        public Sprite blankButton;

        [Header("Buttons")]
        public Sprite blockButton;
        public Sprite challengeButton;
        public Sprite menuButton;
        public Sprite settingsButton;

        [Header("Type")]
        public TMP_FontAsset displayFont;
        public TMP_FontAsset bodyFont;

        public RoleArt For(Role role) => roles.FirstOrDefault(r => r.role == role);

        public ActionArt For(ActionKind action) => actions.FirstOrDefault(a => a.action == action);

        public Sprite CardFace(Role role) => For(role)?.cardFace;

        public Sprite Portrait(Role role)
        {
            var entry = For(role);
            if (entry == null) return null;
            return entry.portrait != null ? entry.portrait : entry.cardFace;
        }

        public Sprite Backdrop(Role role) => For(role)?.backdrop;

        public string Ability(Role role) => For(role)?.ability ?? "";

        public Color Accent(Role role) => For(role)?.accent ?? Color.white;

        public string DisplayName(Role role) => For(role)?.displayName ?? role.ToString();
    }
}
