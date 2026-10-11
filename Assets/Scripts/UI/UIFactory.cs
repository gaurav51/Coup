using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Coup.Game.UI
{
    /// <summary>Small helpers for assembling the portrait HUD from code.</summary>
    public static class UIFactory
    {
        public static readonly Color Gold = new Color(0.84f, 0.69f, 0.33f);
        public static readonly Color Parchment = new Color(0.96f, 0.93f, 0.85f);
        public static readonly Color Navy = new Color(0.07f, 0.11f, 0.20f);
        public static readonly Color Dim = new Color(1f, 1f, 1f, 0.35f);

        public static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        public static Image Image(string name, Transform parent, Sprite sprite,
                                  Vector2 size, Vector2 pos, bool preserveAspect = true)
        {
            var rt = Rect(name, parent, size, pos);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = preserveAspect;
            img.raycastTarget = false;
            if (sprite == null) img.color = new Color(1, 1, 1, 0);
            return img;
        }

        /// <summary>A full-bleed image that covers the canvas regardless of aspect.</summary>
        public static Image FullScreen(string name, Transform parent, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = false;
            img.raycastTarget = false;
            img.type = UnityEngine.UI.Image.Type.Simple;
            return img;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string content,
                                           float fontSize, Vector2 size, Vector2 pos,
                                           TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = Rect(name, parent, size, pos);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = content;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = Parchment;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        public static Button Button(string name, Transform parent, Sprite sprite,
                                    Vector2 size, Vector2 pos)
        {
            var rt = Rect(name, parent, size, pos);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = true;
            if (sprite == null) img.color = new Color(1, 1, 1, 0.08f);

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            return btn;
        }

        /// <summary>Sets interactable and visibly dims the whole button when disabled.</summary>
        public static void SetEnabled(Button button, bool enabled)
        {
            if (button == null) return;
            button.interactable = enabled;
            Group(button.gameObject).alpha = enabled ? 1f : 0.4f;
        }

        /// <summary>
        /// Fetches or adds a CanvasGroup. Uses TryGetComponent rather than `??`,
        /// which bypasses Unity's overloaded null check and can hand back a
        /// destroyed wrapper instead of adding the component.
        /// </summary>
        /// <summary>
        /// Lifts a colour until it is legible on the dark UI. Faction accents are
        /// chosen for the card art, and the darkest of them (the Assassin's near-black
        /// navy) disappears entirely when used as text on a dark panel.
        /// </summary>
        public static Color Readable(Color accent, float minLuminance = 0.55f)
        {
            float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

            var lum = Luminance(accent);
            if (lum >= minLuminance) return accent;

            // Blend towards white just far enough to clear the threshold.
            var t = Mathf.InverseLerp(lum, 1f, minLuminance);
            var lifted = Color.Lerp(accent, Color.white, Mathf.Clamp01(t));
            lifted.a = accent.a;
            return lifted;
        }

        public static CanvasGroup Group(GameObject go)
        {
            return go.TryGetComponent<CanvasGroup>(out var group)
                ? group
                : go.AddComponent<CanvasGroup>();
        }
    }
}
