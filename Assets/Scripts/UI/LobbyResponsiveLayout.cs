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
                _actions.sizeDelta = new Vector2(0, 800);
                _actions.anchoredPosition = Vector2.zero;
            }
            if (_logo != null)
            {
                // On shorter tablets, leave room above the season/ranked cards.
                var desired = Mathf.Max(_lastSize.y * .5f - 12, bottom * _lastSize.y + 915);
                _logo.anchorMin = _logo.anchorMax = new Vector2(.5f, 0);
                var position = new Vector2(0, desired);
                var motion = GetComponent<LobbyMotion>();
                if (motion != null) motion.SetRestingPosition(_logo, position);
                else _logo.anchoredPosition = position;
            }
        }
    }
}
