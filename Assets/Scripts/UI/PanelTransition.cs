using System.Collections;
using UnityEngine;

namespace Coup.Game.UI
{
    /// <summary>Interruptible, unscaled panel motion; never changes authored anchors.</summary>
    public sealed class PanelTransition : MonoBehaviour
    {
        CanvasGroup _group;
        RectTransform _sheet;
        Vector3 _home;
        Coroutine _motion;
        bool _visible;

        public static PanelTransition For(GameObject root, RectTransform sheet)
        {
            var motion = root.GetComponent<PanelTransition>();
            if (motion != null) return motion;
            motion = root.AddComponent<PanelTransition>();
            motion._group = root.GetComponent<CanvasGroup>();
            if (motion._group == null) motion._group = root.AddComponent<CanvasGroup>();
            motion._sheet = sheet;
            motion._home = sheet.localScale;
            return motion;
        }

        public void Show()
        {
            var opening = !_visible;
            _visible = true;
            gameObject.SetActive(true);
            if (_motion != null) StopCoroutine(_motion);
            if (opening && _group.alpha >= .999f) { _group.alpha = 0; _sheet.localScale = _home * .96f; }
            _motion = StartCoroutine(Animate(true));
        }

        public void Hide()
        {
            _visible = false;
            if (!gameObject.activeInHierarchy) return;
            if (_motion != null) StopCoroutine(_motion);
            _motion = StartCoroutine(Animate(false));
        }

        IEnumerator Animate(bool show)
        {
            _group.interactable = false;
            // Keep the backdrop blocking the underlying screen throughout dismissal.
            _group.blocksRaycasts = true;
            var alpha = _group.alpha;
            var scale = _sheet.localScale;
            var duration = PlayerPrefs.GetInt("Coup.ReducedMotion", 0) != 0 ? 0 : show ? .24f : .16f;
            for (float time = 0; time < duration; time += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(time / duration);
                var eased = 1 - Mathf.Pow(1 - t, 3);
                _group.alpha = Mathf.Lerp(alpha, show ? 1 : 0, eased);
                _sheet.localScale = Vector3.Lerp(scale, _home * (show ? 1 : .98f), eased);
                yield return null;
            }
            _group.alpha = show ? 1 : 0;
            _sheet.localScale = _home;
            _group.interactable = show;
            _motion = null;
            if (!show) gameObject.SetActive(false);
        }
    }
}
