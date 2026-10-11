using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if USE_DOTWEEN
using DG.Tweening;
#endif

namespace Coup.Game.UI
{
    /// <summary>
    /// Every animation in the game goes through here.
    ///
    /// DOTween is the intended backend, but it ships from the Asset Store rather than
    /// a package registry, so the project has to compile before it is imported.
    /// <c>DOTweenDetector</c> adds the USE_DOTWEEN define the moment DOTween appears
    /// and the DOTween paths below take over; until then a small coroutine backend
    /// drives the same calls. Callers never change either way.
    /// </summary>
    public static class CoupTween
    {
        public const float Quick = 0.18f;
        public const float Normal = 0.3f;
        public const float Slow = 0.5f;

        private static TweenRunner _runner;

        private static TweenRunner Runner
        {
            get
            {
                if (_runner != null) return _runner;
                var go = new GameObject("~CoupTweenRunner") { hideFlags = HideFlags.HideAndDontSave };
                UnityEngine.Object.DontDestroyOnLoad(go);
                _runner = go.AddComponent<TweenRunner>();
                return _runner;
            }
        }

        /// <summary>Stops anything already animating this object, so tweens never fight.</summary>
        public static void Kill(Component target)
        {
            if (target == null) return;
#if USE_DOTWEEN
            DOTween.Kill(target, false);
#else
            if (_runner != null) _runner.StopFor(target);
#endif
        }

        public static void Fade(CanvasGroup group, float to, float duration = Normal, Action onComplete = null)
        {
            if (group == null) return;
            Kill(group);
#if USE_DOTWEEN
            group.DOFade(to, duration).SetTarget(group).SetUpdate(true)
                 .SetEase(Ease.OutQuad).OnComplete(() => onComplete?.Invoke());
#else
            var from = group.alpha;
            Runner.Run(group, duration, t => group.alpha = Mathf.LerpUnclamped(from, to, EaseOutQuad(t)), onComplete);
#endif
        }

        public static void MoveTo(RectTransform rect, Vector2 to, float duration = Normal, Action onComplete = null)
        {
            if (rect == null) return;
            Kill(rect);
#if USE_DOTWEEN
            rect.DOAnchorPos(to, duration).SetTarget(rect).SetUpdate(true)
                .SetEase(Ease.OutCubic).OnComplete(() => onComplete?.Invoke());
#else
            var from = rect.anchoredPosition;
            Runner.Run(rect, duration, t => rect.anchoredPosition = Vector2.LerpUnclamped(from, to, EaseOutCubic(t)), onComplete);
#endif
        }

        public static void ScaleTo(Transform target, Vector3 to, float duration = Normal, Action onComplete = null)
        {
            if (target == null) return;
            Kill(target);
#if USE_DOTWEEN
            target.DOScale(to, duration).SetTarget(target).SetUpdate(true)
                  .SetEase(Ease.OutBack).OnComplete(() => onComplete?.Invoke());
#else
            var from = target.localScale;
            Runner.Run(target, duration, t => target.localScale = Vector3.LerpUnclamped(from, to, EaseOutBack(t)), onComplete);
#endif
        }

        /// <summary>
        /// A short squash-and-pop. Pass the element's resting scale explicitly: reading
        /// it from the transform picks up whatever a half-finished tween left behind,
        /// and the error compounds until the element is visibly the wrong size.
        /// </summary>
        public static void Punch(Transform target, float strength = 0.22f, float duration = Normal,
                                 Vector3? restingScale = null)
        {
            if (target == null) return;
            var home = restingScale ?? Vector3.one;
            Kill(target);
            target.localScale = home;
#if USE_DOTWEEN
            target.DOPunchScale(Vector3.one * strength, duration, 6, 0.8f)
                  .SetTarget(target).SetUpdate(true)
                  .OnKill(() => target.localScale = home);
#else
            Runner.Run(target, duration, t =>
            {
                var p = Mathf.Sin(t * Mathf.PI * 3f) * (1f - t) * strength;
                target.localScale = home * (1f + p);
            }, () => target.localScale = home);
#endif
        }

        /// <summary>Used when a bluff is caught or influence is lost.</summary>
        public static void Shake(RectTransform rect, float strength = 18f, float duration = Slow)
        {
            if (rect == null) return;
            Kill(rect);
#if USE_DOTWEEN
            rect.DOShakeAnchorPos(duration, strength, 18, 90f, false, true)
                .SetTarget(rect).SetUpdate(true);
#else
            var home = rect.anchoredPosition;
            Runner.Run(rect, duration, t =>
            {
                var decay = 1f - t;
                rect.anchoredPosition = home + new Vector2(
                    Mathf.Sin(t * 50f) * strength * decay,
                    Mathf.Cos(t * 41f) * strength * 0.5f * decay);
            }, () => rect.anchoredPosition = home);
#endif
        }

        public static void ColorTo(Graphic graphic, Color to, float duration = Normal)
        {
            if (graphic == null) return;
            Kill(graphic);
#if USE_DOTWEEN
            graphic.DOColor(to, duration).SetTarget(graphic).SetUpdate(true);
#else
            var from = graphic.color;
            Runner.Run(graphic, duration, t => graphic.color = Color.LerpUnclamped(from, to, EaseOutQuad(t)));
#endif
        }

        /// <summary>
        /// Flips a card by scaling its width through zero, swapping the face at the
        /// midpoint. `restingScale` is the scale to return to — taking it from the
        /// transform would capture a mid-flip value if this interrupts another flip.
        /// </summary>
        public static void Flip(RectTransform rect, Action atMidpoint, float duration = Normal,
                                Action onComplete = null, Vector3? restingScale = null)
        {
            if (rect == null) { atMidpoint?.Invoke(); onComplete?.Invoke(); return; }
            Kill(rect);

            var full = restingScale ?? Vector3.one;
            rect.localScale = full;
            var half = duration * 0.5f;
            var flat = new Vector3(0f, full.y, full.z);

#if USE_DOTWEEN
            DOTween.Sequence().SetTarget(rect).SetUpdate(true)
                .Append(rect.DOScale(flat, half).SetEase(Ease.InQuad))
                .AppendCallback(() => atMidpoint?.Invoke())
                .Append(rect.DOScale(full, half).SetEase(Ease.OutQuad))
                .OnComplete(() => onComplete?.Invoke());
#else
            Runner.Run(rect, half, t => rect.localScale = new Vector3(Mathf.Lerp(full.x, 0f, t), full.y, full.z), () =>
            {
                atMidpoint?.Invoke();
                Runner.Run(rect, half, t => rect.localScale = new Vector3(Mathf.Lerp(0f, full.x, t), full.y, full.z), onComplete);
            });
#endif
        }

        /// <summary>
        /// Moves, scales and settles a card in one tween. Position and scale have to
        /// share a runner: they both target the same transform, and separate tweens
        /// would kill each other.
        /// </summary>
        public static void ArcTo(RectTransform rect, Vector2 to, Vector3 scaleTo, float duration = Normal,
                                 float arcHeight = 120f, Action onComplete = null)
        {
            if (rect == null) { onComplete?.Invoke(); return; }
            Kill(rect);

            var from = rect.anchoredPosition;
            var fromScale = rect.localScale;
            var spin = Mathf.Clamp((to.x - from.x) * 0.015f, -12f, 12f);

            void Step(float t)
            {
                var e = EaseOutCubic(t);
                var pos = Vector2.LerpUnclamped(from, to, e);
                pos.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
                rect.anchoredPosition = pos;
                rect.localScale = Vector3.LerpUnclamped(fromScale, scaleTo, EaseOutBack(Mathf.Clamp01(t)));
                rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(spin, 0f, e));
            }

#if USE_DOTWEEN
            DOVirtual.Float(0f, 1f, duration, Step).SetTarget(rect).SetUpdate(true)
                .SetEase(Ease.Linear).OnComplete(() => onComplete?.Invoke());
#else
            Runner.Run(rect, duration, Step, onComplete);
#endif
        }

        /// <summary>Fades and slides a panel in from slightly below.</summary>
        public static void PopIn(RectTransform rect, CanvasGroup group, float duration = Normal,
                                 Vector3? restingScale = null)
        {
            if (rect == null) return;
            var home = restingScale ?? Vector3.one;
            if (group != null) { group.alpha = 0f; Fade(group, 1f, duration); }
            rect.localScale = home * 0.92f;
            ScaleTo(rect, home, duration);
        }

        public static void DelayedCall(float delay, Action action)
        {
#if USE_DOTWEEN
            DOVirtual.DelayedCall(delay, () => action?.Invoke(), true);
#else
            Runner.Run(null, delay, null, action);
#endif
        }

        // ------------------------------------------------------- fallback backend

        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
        private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private class TweenRunner : MonoBehaviour
        {
            private readonly System.Collections.Generic.Dictionary<Component, Coroutine> _active = new();

            public void Run(Component owner, float duration, Action<float> step, Action onComplete = null)
            {
                if (owner != null) StopFor(owner);
                var routine = StartCoroutine(Drive(owner, duration, step, onComplete));
                if (owner != null) _active[owner] = routine;
            }

            public void StopFor(Component owner)
            {
                if (owner == null) return;
                if (_active.TryGetValue(owner, out var routine))
                {
                    if (routine != null) StopCoroutine(routine);
                    _active.Remove(owner);
                }
            }

            private IEnumerator Drive(Component owner, float duration, Action<float> step, Action onComplete)
            {
                var elapsed = 0f;
                while (elapsed < duration)
                {
                    if (owner == null && step != null) yield break;
                    elapsed += Time.unscaledDeltaTime;
                    step?.Invoke(Mathf.Clamp01(elapsed / duration));
                    yield return null;
                }

                step?.Invoke(1f);
                if (owner != null) _active.Remove(owner);
                onComplete?.Invoke();
            }
        }
    }
}
