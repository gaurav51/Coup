using UnityEngine;
using UnityEngine.UI;

namespace Coup.Game.UI
{
    /// <summary>Canvas-native clock geometry: crisp at any UI scale, with no sliced texture.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TurnClockRing : MaskableGraphic
    {
        private float _progress = 1f;
        public float thickness = 4f;
        public float Progress {
            get => _progress;
            set { value = Mathf.Clamp01(value); if (Mathf.Approximately(value, _progress)) return; _progress = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper vh) {
            vh.Clear();
            if (_progress <= 0f) return;
            var rect = rectTransform.rect;
            var radius = Mathf.Min(rect.width, rect.height) * .5f;
            var inner = Mathf.Max(0f, radius - thickness);
            var steps = Mathf.Max(1, Mathf.CeilToInt(120f * _progress));
            for (var i = 0; i < steps; i++) {
                var a = Mathf.PI * .5f - Mathf.PI * 2f * _progress * i / steps;
                var b = Mathf.PI * .5f - Mathf.PI * 2f * _progress * (i + 1) / steps;
                var da = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var db = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                var n = vh.currentVertCount;
                vh.AddVert(rect.center + da * inner, color, Vector2.zero);
                vh.AddVert(rect.center + da * radius, color, Vector2.zero);
                vh.AddVert(rect.center + db * radius, color, Vector2.zero);
                vh.AddVert(rect.center + db * inner, color, Vector2.zero);
                vh.AddTriangle(n, n + 1, n + 2);
                vh.AddTriangle(n, n + 2, n + 3);
            }
        }
    }
}
