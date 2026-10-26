using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Coup.Game.UI
{
    public enum ButtonTone { Auto, Primary, Row, Quiet }

    /// <summary>
    /// Buttons inside panels used the panels' own navy marble and gold frame, so a
    /// button read as a smaller panel. Choices and actions now use the art pack's gold
    /// bar with dark Cinzel capitals; the way out (Back, Close, Cancel) is the quieter
    /// navy pill the table's RULES button uses. Sprites and font come from PanelTheme.
    /// The skin is re-applied if other code swaps the sprite or restyles the label.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class PanelButtonSkin : MonoBehaviour
    {
        static readonly Color Ink = new Color(.17f, .09f, .02f);
        static readonly Color QuietInk = new Color(.86f, .8f, .66f);
        static readonly Color QuietFill = new Color(.06f, .07f, .10f, 1f);
        static readonly Color QuietRim = new Color(.84f, .69f, .33f, .5f);
        static Sprite _gold;

        Button _button;
        Image _image, _rim;
        ButtonTone _tone;
        float _height = -1;
        TMP_Text[] _labels;
        int _childCount = -1;

        /// <summary>The gold bar, cropped to its visible band and 9-sliced so its diamond tips keep their shape.</summary>
        public static Sprite Gold
        {
            get
            {
                if (_gold != null) return _gold;
                var source = PanelTheme.Current != null ? PanelTheme.Current.goldButton : null;
                if (source == null) return null;
                // Measured on the 2172x724 source: the bar spans x 18..2152, y 142..593 from the bottom,
                // and its tips end 241px in from each side.
                var texture = source.texture;
                float kx = texture.width / 2172f, ky = texture.height / 724f;
                var rect = new Rect(16 * kx, 142 * ky, 2140 * kx, 451 * ky);
                var border = new Vector4(262 * kx, 110 * ky, 262 * kx, 110 * ky);
                _gold = Sprite.Create(texture, rect, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, border);
                _gold.hideFlags = HideFlags.HideAndDontSave;
                return _gold;
            }
        }

        public static void Apply(Button button, ButtonTone tone)
        {
            if (button == null || Gold == null) return;
            var skin = button.GetComponent<PanelButtonSkin>();
            if (skin == null) skin = button.gameObject.AddComponent<PanelButtonSkin>();
            skin._button = button;
            skin._image = button.GetComponent<Image>();
            skin._tone = tone == ButtonTone.Auto ? Guess(button) : tone;
            skin._height = -1;
            foreach (var name in new[] { "Rim", "Outline", "Border", "ThemeFrame" })
            {
                var old = button.transform.Find(name)?.GetComponent<Image>();
                if (old != null) old.enabled = false;
            }
            skin.Refresh();
        }

        static ButtonTone Guess(Button button)
        {
            var name = button.name.ToLowerInvariant();
            var label = button.GetComponentInChildren<TMP_Text>(true);
            var text = label != null ? label.text.ToLowerInvariant() : "";
            foreach (var quiet in new[] { "close", "cancel", "back", "dismiss", "lobby", "leave" })
                if (name.Contains(quiet) || text.StartsWith(quiet) || text.Contains("‹")) return ButtonTone.Quiet;
            return ButtonTone.Primary;
        }

        bool Quiet => _tone == ButtonTone.Quiet;
        Sprite Face => Quiet && PanelTheme.Current.quietFill != null ? PanelTheme.Current.quietFill : Gold;
        Color Tint => Quiet ? QuietFill : Color.white;

        void Refresh()
        {
            if (_image == null) return;
            _image.sprite = Face;
            _image.type = Image.Type.Sliced;
            _image.preserveAspect = false;
            _image.color = Tint;
            if (Quiet && _rim == null && PanelTheme.Current.quietRim != null)
            {
                _rim = UIFactory.Image("SkinRim", transform, PanelTheme.Current.quietRim, Vector2.zero, Vector2.zero, false);
                var rect = _rim.rectTransform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                _rim.type = Image.Type.Sliced; _rim.color = QuietRim;
                _rim.transform.SetSiblingIndex(0);
            }
            if (_rim != null) _rim.enabled = Quiet;
            Fit();
        }

        // The gold bar's tips are drawn for its full height; scale the slices to the button's.
        void Fit()
        {
            var height = ((RectTransform)transform).rect.height;
            if (height <= 1 || Mathf.Approximately(height, _height)) return;
            _height = height;
            _image.pixelsPerUnitMultiplier = Quiet ? 1f : Gold.rect.height / height;
        }

        void OnRectTransformDimensionsChange() { if (_image != null) Fit(); }

        void LateUpdate()
        {
            if (_image == null) return;
            if (_image.sprite != Face || _image.color != Tint) Refresh();
            Fit();
            // Labels are often added or restyled after the skin, so keep them in the button's type.
            var ink = Quiet ? QuietInk : Ink;
            if (_button != null && !_button.IsInteractable()) ink.a = .5f;
            if (_labels == null || _childCount != transform.childCount)
            {
                _labels = GetComponentsInChildren<TMP_Text>(true);
                _childCount = transform.childCount;
            }
            var font = PanelTheme.Current.buttonFont;
            // The gold bar's tips are about 0.58 of its height wide; text must stay between them.
            var room = ((RectTransform)transform).rect.width - (Quiet ? 40f : _height * 1.25f);
            foreach (var label in _labels)
            {
                if (label == null) continue;
                if (label.color != ink) label.color = ink;
                if (font != null && label.font != font) Style(label, font);
                var rect = label.rectTransform;
                if (room > 40 && rect.sizeDelta.x > room) rect.sizeDelta = new Vector2(room, rect.sizeDelta.y);
            }
        }

        static void Style(TMP_Text label, TMP_FontAsset font)
        {
            label.font = font;
            label.fontStyle = FontStyles.UpperCase | FontStyles.Bold;
            label.characterSpacing = 3;
            // Cinzel has no ellipsis glyph; autosizing keeps the text inside instead.
            label.overflowMode = TextOverflowModes.Overflow;
            // Cinzel capitals run wider than Rajdhani, so cap the size and let long labels shrink.
            var size = Mathf.Min(label.fontSize, label.name == "Coins" ? 20 : 29);
            label.enableAutoSizing = true;
            label.fontSizeMax = size;
            label.fontSizeMin = Mathf.Min(size, 16);
        }
    }
}
