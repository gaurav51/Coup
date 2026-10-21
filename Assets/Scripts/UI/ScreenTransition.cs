using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Coup.Game.UI
{
    /// <summary>Persistent full-screen cover prevents duplicate navigation and input during loads.</summary>
    public sealed class ScreenTransition : MonoBehaviour
    {
        static ScreenTransition _instance;
        CanvasGroup _cover;
        bool _loading;
        public static bool IsTransitioning => _instance != null && _instance._loading;

        public static void Load(string scene)
        {
            if (_instance == null)
            {
                var root = new GameObject("ScreenTransition", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
                DontDestroyOnLoad(root);
                _instance = root.AddComponent<ScreenTransition>();
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32760;
                _instance._cover = root.GetComponent<CanvasGroup>();
                var fill = new GameObject("Cover", typeof(RectTransform), typeof(Image));
                fill.transform.SetParent(root.transform, false);
                var rect = (RectTransform)fill.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                fill.GetComponent<Image>().color = new Color(.012f, .022f, .035f, 1);
                _instance._cover.alpha = 0;
            }
            if (_instance._loading) return;
            _instance._loading = true;
            _instance.StartCoroutine(_instance.ChangeScene(scene));
        }

        IEnumerator ChangeScene(string scene)
        {
            _cover.blocksRaycasts = true;
            var duration = PlayerPrefs.GetInt("Coup.ReducedMotion", 0) != 0 ? 0 : .22f;
            yield return Fade(1, duration);
            AsyncOperation load = null;
            try { load = SceneManager.LoadSceneAsync(scene); }
            catch (System.Exception error) { Debug.LogException(error); }
            if (load != null) yield return load;
            yield return null; // Let the destination canvas complete its first layout pass.
            yield return Fade(0, duration);
            _cover.blocksRaycasts = false;
            _loading = false;
        }

        IEnumerator Fade(float target, float duration)
        {
            var from = _cover.alpha;
            for (float time = 0; time < duration; time += Time.unscaledDeltaTime)
            {
                _cover.alpha = Mathf.SmoothStep(from, target, time / duration);
                yield return null;
            }
            _cover.alpha = target;
        }
    }
}
