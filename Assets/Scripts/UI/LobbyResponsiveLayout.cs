using UnityEngine;

namespace Coup.Game.UI
{
    /// <summary>Keeps controls in the device safe area and the wordmark above the action stack.</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class LobbyResponsiveLayout : MonoBehaviour
    {
        private RectTransform _root, _hud, _actions, _logo;
        private Rect _lastSafe;
        private Vector2 _lastSize;
        private void Awake()
        {
            _root = (RectTransform)transform;
            _hud = transform.Find("Hud") as RectTransform;
            _actions = transform.Find("Actions") as RectTransform;
            _logo = transform.Find("Hall/LobbyLogo") as RectTransform;
            Apply();
        }
        private void Update()
        {
            if (_lastSafe != Screen.safeArea || _lastSize != _root.rect.size) Apply();
        }
        private void Apply()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            _lastSafe = Screen.safeArea;
            _lastSize = _root.rect.size;
            var left = _lastSafe.xMin / Screen.width;
            var right = _lastSafe.xMax / Screen.width;
            var bottom = _lastSafe.yMin / Screen.height;
            var top = _lastSafe.yMax / Screen.height;
            var safeHeight = _lastSize.y * (top - bottom);
            var gap = Mathf.Lerp(22, 55, Mathf.InverseLerp(1920, 2400, safeHeight));
            // The bottom row's centre; 73 left it about 29 above the screen edge, against the gesture bar.
            const float rowY = 97;
            var eventY = rowY + 44 + gap + 51;
            var matchY = eventY + 51 + gap + 54;
            var playY = matchY + 54 + gap + 87;
            var featureY = playY + 87 + gap + 105;
            if (_hud != null)
            {
                _hud.anchorMin = new Vector2(left, top);
                _hud.anchorMax = new Vector2(right, top);
                _hud.pivot = new Vector2(.5f, 1);
                _hud.sizeDelta = new Vector2(0, 200);
                _hud.anchoredPosition = Vector2.zero;
            }
            if (_actions != null)
            {
                _actions.anchorMin = new Vector2(left, bottom);
                _actions.anchorMax = new Vector2(right, bottom);
                _actions.pivot = new Vector2(.5f, 0);
                _actions.sizeDelta = new Vector2(0, featureY + 105);
                SpaceRow("Collection", rowY);
                SpaceRow("Shop", rowY);
                SpaceRow("Missions", rowY);
                SpaceRow("Event", eventY);
                SpaceRow("Casual", matchY);
                SpaceRow("Ranked", matchY);
                SpaceRow("Play", playY);
                SpaceRow("SeasonPass", featureY);
                SpaceRow("RankedTier", featureY);
                _actions.anchoredPosition = Vector2.zero;
            }
            if (_logo != null)
            {
                // Measure clearance from the action stack, including the logo half-height.
                var desired = Mathf.Max(_lastSize.y * .5f - 12, bottom * _lastSize.y + featureY + 105 + gap + _logo.rect.height * .5f);
                _logo.anchorMin = _logo.anchorMax = new Vector2(.5f, 0);
                var position = new Vector2(0, desired);
                var motion = GetComponent<LobbyMotion>();
                if (motion != null) motion.SetRestingPosition(_logo, position);
                else _logo.anchoredPosition = position;
            }
        }
        private void SpaceRow(string name, float y)
        {
            var rect = _actions.Find(name) as RectTransform;
            if (rect == null) return;
            var position = new Vector2(rect.anchoredPosition.x, y);
            var motion = GetComponent<LobbyMotion>();
            if (motion != null) motion.SetRestingPosition(rect, position);
            else rect.anchoredPosition = position;
        }
    }
}
