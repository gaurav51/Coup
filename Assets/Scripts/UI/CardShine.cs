using UnityEngine;
using UnityEngine.UI;

namespace Coup.Game.UI
{
    /// <summary>
    /// Drives the foil sweep on a card. It owns a dedicated overlay Image and its own
    /// material instance — the card's portrait and backdrop are never touched, so the
    /// artwork stays exactly as authored.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class CardShine : MonoBehaviour
    {
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int IntensityId = Shader.PropertyToID("_ShineIntensity");
        private static readonly int OffsetId = Shader.PropertyToID("_TimeOffset");
        private static readonly int IntervalId = Shader.PropertyToID("_ShineInterval");
        private static readonly int ShiftId = Shader.PropertyToID("_ShineShift");

        [Tooltip("Shine strength during the idle shimmer.")]
        public float idleIntensity = 0.5f;

        [Tooltip("Shine strength during a triggered sweep.")]
        public float sweepIntensity = 1.45f;

        [Tooltip("Seconds for one triggered sweep.")]
        public float sweepDuration = 0.72f;

        [Tooltip("Let the shader run its own slow shimmer when nothing is sweeping.")]
        public bool idleShimmer = true;

        [Tooltip("Shortest and longest gap between idle shimmers, picked per card.")]
        public float minInterval = 3.5f;
        public float maxInterval = 7.5f;

        private Image _image;
        private Material _material;
        private float _sweepStart = -1f;
        private Color _tint = new Color(1f, 0.96f, 0.82f, 1f);

        private void Awake()
        {
            _image = GetComponent<Image>();
            _image.raycastTarget = false;

            // Each card sweeps independently, so it needs its own material instance
            // rather than mutating the shared asset.
            if (_image.material != null && _image.material.shader != null)
            {
                _material = new Material(_image.material);
                _image.material = _material;
            }

            RandomisePhase();
            ApplyIdle();
        }

        /// <summary>
        /// Gives this card its own shimmer clock. Cards share a material asset but
        /// each gets an instance, so a random phase and period here stops the table
        /// from pulsing in unison.
        /// </summary>
        private void RandomisePhase()
        {
            if (_material == null) return;

            var interval = Random.Range(minInterval, Mathf.Max(minInterval, maxInterval));
            _material.SetFloat(IntervalId, interval);
            _material.SetFloat(OffsetId, Random.Range(0f, interval));
            _material.SetFloat(ShiftId, 0f);
        }

        /// <summary>Tints the sweep to the card's accent, unchanged.</summary>
        public void SetTint(Color color)
        {
            color.a = 1f;
            _tint = color;
            // Vertex colour, not the material. The card mask draws a stencil clone
            // of the material, so a colour set on the material stays at its default.
            if (_image != null) _image.color = _tint;
        }

        /// <summary>Slides the band as the card tilts, so the gloss follows the pointer.</summary>
        public void SetShift(float shift)
        {
            if (_material != null) _material.SetFloat(ShiftId, shift);
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }

        /// <summary>Runs a single bright sweep, used when a card is revealed or picked.</summary>
        public void PlaySweep()
        {
            if (_material == null) return;
            _sweepStart = Time.unscaledTime;
            if (_image != null) _image.color = _tint;
            _material.SetFloat(IntensityId, sweepIntensity);
            enabled = true;
        }

        private void ApplyIdle()
        {
            if (_material == null) return;

            if (_image != null) _image.color = _tint;

            if (idleShimmer)
            {
                // A negative progress hands the sweep back to the shader's timer.
                _material.SetFloat(ProgressId, -1f);
                _material.SetFloat(IntensityId, idleIntensity);
            }
            else
            {
                _material.SetFloat(IntensityId, 0f);
            }
        }

        private void Update()
        {
            if (_sweepStart < 0f || _material == null) return;

            var t = (Time.unscaledTime - _sweepStart) / Mathf.Max(sweepDuration, 0.01f);
            if (t >= 1f)
            {
                _sweepStart = -1f;
                ApplyIdle();
                return;
            }

            _material.SetFloat(ProgressId, t);
        }
    }
}
