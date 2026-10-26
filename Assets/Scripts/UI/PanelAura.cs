using UnityEngine;
using UnityEngine.UI;

namespace Coup.Game.UI
{
    /// <summary>
    /// Ambient motion around a panel: a soft outer glow that breathes, and two glints that
    /// travel the frame in opposite directions. Runs on unscaled time so it keeps moving
    /// while practice is paused, holds still under reduced motion, and never takes input.
    /// It only adds its own children, so the panel's authored layout is untouched.
    /// </summary>
    public sealed class PanelAura : MonoBehaviour
    {
        RectTransform _panel;
        Image _glow;
        RectTransform[] _glints;
        Image[] _glintImages;
        Color _tint;
        float _inset, _phase;

        /// <summary>Adds the aura once; calling again only retints it.</summary>
        public static PanelAura Attach(RectTransform panel, CoupArt art, Color tint, float inset = 10)
        {
            if (panel == null || art == null) return null;
            var aura = panel.GetComponent<PanelAura>();
            if (aura == null)
            {
                aura = panel.gameObject.AddComponent<PanelAura>();
                aura.Build(art);
            }
            aura._tint = tint;
            aura._inset = inset;
            return aura;
        }

        void Build(CoupArt art)
        {
            _panel = (RectTransform)transform;
            _phase = Random.value;
            _glow = UIFactory.Image("AuraGlow", _panel, SoftEdge(), Vector2.zero, Vector2.zero, false);
            _glow.type = Image.Type.Sliced;
            // Edge band only: a filled centre tinted the whole panel gold.
            _glow.fillCenter = false;
            Stretch(_glow.rectTransform, -30);
            _glow.transform.SetAsFirstSibling();
            _glints = new RectTransform[2];
            _glintImages = new Image[4];
            for (int i = 0; i < 2; i++)
            {
                var halo = UIFactory.Image("AuraGlint", _panel, art.vfxSpark, new Vector2(120, 120), Vector2.zero);
                var core = UIFactory.Image("Core", halo.transform, art.vfxSpark, new Vector2(34, 34), Vector2.zero);
                _glints[i] = halo.rectTransform;
                _glintImages[i * 2] = halo;
                _glintImages[i * 2 + 1] = core;
            }
        }

        static Sprite _softEdge;

        /// <summary>
        /// A soft halo around a rounded rectangle, 9-sliced so it fits any panel. The shared
        /// outline sprite is crisp, which drew a second frame line rather than a glow.
        /// </summary>
        static Sprite SoftEdge()
        {
            if (_softEdge != null) return _softEdge;
            const int size = 96, edge = 30, radius = 14;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Signed distance to a rounded rectangle inset by `edge`.
                float qx = Mathf.Abs(x + .5f - size / 2f) - (size / 2f - edge - radius);
                float qy = Mathf.Abs(y + .5f - size / 2f) - (size / 2f - edge - radius);
                float outside = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
                float alpha = Mathf.Exp(-(outside * outside) / 90f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            _softEdge = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0,
                SpriteMeshType.FullRect, new Vector4(47, 47, 47, 47));
            _softEdge.hideFlags = HideFlags.HideAndDontSave;
            return _softEdge;
        }

        static void Stretch(RectTransform rect, float outset)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(outset, outset); rect.offsetMax = new Vector2(-outset, -outset);
        }

        void LateUpdate()
        {
            if (_panel == null) return;
            // Content added after the aura would cover the glints; keep them on top of the frame.
            if (_glints[1].GetSiblingIndex() != _panel.childCount - 1)
            {
                _glints[0].SetAsLastSibling();
                _glints[1].SetAsLastSibling();
            }
            var reduced = PlayerPrefs.GetInt("Coup.ReducedMotion", 0) != 0;
            var time = Time.unscaledTime;
            var breath = reduced ? .5f : (Mathf.Sin(time * 1.6f + _phase * 6.28f) + 1) * .5f;
            _glow.color = new Color(_tint.r, _tint.g, _tint.b, Mathf.Lerp(.22f, .55f, breath));

            // Glints stay on the frame line, whatever size the panel is drawn at this frame.
            var rect = _panel.rect;
            float w = Mathf.Max(1, rect.width - _inset * 2), h = Mathf.Max(1, rect.height - _inset * 2);
            var perimeter = 2 * (w + h);
            for (int i = 0; i < _glints.Length; i++)
            {
                var visible = !reduced;
                _glints[i].gameObject.SetActive(visible);
                if (!visible) continue;
                var travel = Mathf.Repeat((i == 0 ? 1 : -1) * time * .07f + _phase + i * .5f, 1f) * perimeter;
                _glints[i].anchoredPosition = rect.center + Along(travel, w, h);
                // Each glint swells and fades over its lap so the loop never reads as mechanical.
                var swell = Mathf.Sin(Mathf.Repeat(time * .21f + i * .5f + _phase, 1f) * Mathf.PI);
                _glintImages[i * 2].color = new Color(_tint.r, _tint.g, _tint.b, .55f * swell);
                _glintImages[i * 2 + 1].color = new Color(1, .97f, .86f, .9f * swell);
            }
        }

        /// <summary>A point on the rectangle's edge, walked clockwise from the top-left corner.</summary>
        static Vector2 Along(float d, float w, float h)
        {
            if (d < w) return new Vector2(-w / 2 + d, h / 2);
            d -= w;
            if (d < h) return new Vector2(w / 2, h / 2 - d);
            d -= h;
            if (d < w) return new Vector2(w / 2 - d, -h / 2);
            d -= w;
            return new Vector2(-w / 2, -h / 2 + d);
        }
    }
}
