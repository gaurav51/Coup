using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Coup.Game.UI
{
    /// <summary>
    /// The effects layer. Everything here is plain UI Images on their own canvas
    /// layer, which is what a Screen Space - Overlay canvas can actually composite —
    /// a ParticleSystem would sort behind or in front of the whole HUD rather than
    /// between elements. Objects are pooled, so a long match does not churn garbage.
    /// </summary>
    public class UiVfx : MonoBehaviour
    {
        private RectTransform _root;
        private CoupArt _art;
        private Material _glow;

        [System.NonSerialized] private Sprite[] _triangles;
        [System.NonSerialized] private Texture2D[] _triangleTex;

        private readonly List<Image> _pool = new();
        private readonly List<Active> _live = new();

        private struct Active
        {
            public Image Image;
            public RectTransform Rect;
            public Vector2 From;
            public Vector2 To;
            public Vector2 Velocity;
            public float Gravity;
            public float Start;
            public float Duration;
            public float StartScale;
            public float EndScale;
            public Color StartColour;
            public float Arc;        // hop height on the eased path
            public bool Ballistic;   // arcs under gravity rather than easing to a point
            public float Angle;      // starting rotation, degrees
            public float Spin;       // extra degrees per second
            public float Stretch;    // length along the flight, 1 is a round mote
            public bool Align;       // point the streak along its velocity
            public float Heat;       // how white the spark burns before it cools
            public float Hold;       // fraction of the life spent at full brightness
        }

        [Header("Action particle art")]
        public Sprite daggerParticle;
        public Sprite assassinationReticle;
        public Sprite shockwaveParticle;
        public Sprite smokeParticle;
        private AssassinationCue _assassination;
        private bool _coupActive;
        public bool HasAssassination => _assassination != null;
        public int LiveParticleCount => _live.Count + (_assassination != null ? 1 : 0);
        public int PooledParticleCount => _pool.Count;

        private sealed class AssassinationCue
        {
            public Vector2 From, To;
            public Image Reticle;
            public float Started, ResolveAt = -1f;
            public bool Hit;
        }

        private readonly List<Banner> _banners = new();

        private sealed class Banner
        {
            public int Seat;
            public RectTransform Rect;
            public CanvasGroup Group;
            public Vector2 At;
            public Vector2 From;
            public Vector2 To;
            public bool Fly;
            public float Start;
        }

        /// <summary>The overlay cards and sparks are parented to, in this layer's space.</summary>
        public RectTransform Layer => _root;

        public void Build(RectTransform parent, CoupArt art, float scale = 1f)
        {
            _art = art;
            var existing = parent.Find("Vfx") as RectTransform;
            if (existing != null)
                _root = existing;
            else
                _root = UIFactory.Rect("Vfx", parent, new Vector2(1080, 1920), Vector2.zero);

            _root.localScale = Vector3.one * scale;
            _root.SetAsLastSibling();          // draws above the HUD
            var group = _root.GetComponent<CanvasGroup>();
            if (group == null) group = UIFactory.Group(_root.gameObject);
            group.blocksRaycasts = false;
            group.interactable = false;

            if (_glow == null)
            {
                var shader = Shader.Find("Coup/UI Additive");
                if (shader != null) _glow = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }

        }

        private void OnDestroy()
        {
            if (_glow != null) Destroy(_glow);
            if (_triangles != null)
                for (var i = 0; i < _triangles.Length; i++)
                    if (_triangles[i] != null) Destroy(_triangles[i]);
            if (_triangleTex != null)
                for (var i = 0; i < _triangleTex.Length; i++)
                    if (_triangleTex[i] != null) Destroy(_triangleTex[i]);
        }

        // ------------------------------------------------------------- effects

        /// <summary>Coins arcing from a source to a purse, or back into the pile.</summary>
        public void CoinFly(Vector2 from, Vector2 to, int count = 6, float delay = 0f)
        {
            count = Mathf.Clamp(count, 1, 12);
            for (var i = 0; i < count; i++)
            {
                var img = Take(_art != null ? _art.coin : null, 46f);
                var jitter = new Vector2(Random.Range(-36f, 36f), Random.Range(-24f, 24f));

                _live.Add(new Active
                {
                    Image = img,
                    Rect = (RectTransform)img.transform,
                    From = from + jitter,
                    To = to,
                    Start = Time.unscaledTime + delay + i * 0.05f,
                    Duration = 0.62f,
                    StartScale = 0.85f,
                    EndScale = 0.65f,
                    StartColour = Color.white,
                    Arc = 150f
                });
            }
        }

        /// <summary>A scatter of glowing streaks, for an impact or a card leaving play.</summary>
        public void Burst(Vector2 at, Color colour, int count = 14, float power = 420f)
        {
            count = Mathf.Clamp(count, 1, 40);
            var spark = _art != null ? _art.vfxSpark : null;

            for (var i = 0; i < count; i++)
            {
                var img = Take(spark, Random.Range(16f, 34f), glow: true);
                var angle = Random.Range(0f, Mathf.PI * 2f);
                var speed = power * Random.Range(0.4f, 1f);
                var tint = Color.Lerp(colour, Color.white, Random.Range(0.2f, 0.7f));
                tint.a = 1f;

                _live.Add(new Active
                {
                    Image = img,
                    Rect = (RectTransform)img.transform,
                    From = at,
                    Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
                    Gravity = -620f,
                    Start = Time.unscaledTime,
                    Duration = Random.Range(0.62f, 1.05f),
                    StartScale = 1f,
                    EndScale = 0.12f,
                    StartColour = tint,
                    Ballistic = true,
                    Align = true,
                    Stretch = Random.Range(2.6f, 4.8f),
                    Heat = Random.Range(0.4f, 0.9f),
                    Hold = 0.06f
                });
            }

            // A few round embers hang after the streaks have gone.
            var motes = Mathf.Clamp(count / 3, 3, 8);
            for (var i = 0; i < motes; i++)
            {
                var img = Take(spark, Random.Range(10f, 20f), glow: true);
                var angle = Random.Range(0f, Mathf.PI * 2f);
                var tint = colour;
                tint.a = 0.85f;
                _live.Add(new Active
                {
                    Image = img,
                    Rect = (RectTransform)img.transform,
                    From = at,
                    Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * power * Random.Range(0.15f, 0.4f),
                    Gravity = -180f,
                    Start = Time.unscaledTime,
                    Duration = Random.Range(0.55f, 0.9f),
                    StartScale = 1f,
                    EndScale = 0.2f,
                    StartColour = tint,
                    Ballistic = true,
                    Heat = 0.25f,
                    Hold = 0.15f
                });
            }
        }

        /// <summary>An expanding ring — reads as a shockwave on a challenge.</summary>
        public void Ring(Vector2 at, Color colour, float size = 560f, float delay = 0f)
        {
            var img = Take(shockwaveParticle != null ? shockwaveParticle : (_art != null ? _art.vfxRing : null), size * 0.42f, glow: true);
            var tint = Color.Lerp(colour, Color.white, 0.16f);
            tint.a = 0.9f;
            _live.Add(new Active
            {
                Image = img,
                Rect = (RectTransform)img.transform,
                From = at,
                To = at,
                Start = Time.unscaledTime + delay,
                Duration = 0.62f,
                StartScale = 0.65f,
                EndScale = size / Mathf.Max(size * 0.42f, 1f),
                StartColour = tint,
                Heat = 0.2f,
                Hold = 0.12f
            });
        }

        /// <summary>A role hitting the table: a flash, a shockwave and a spray of streaks.</summary>
        public void Impact(Vector2 at, Color colour)
        {
            var hot = Color.Lerp(colour, Color.white, 0.75f);
            hot.a = 1f;
            CoreFlash(at, hot);
            Bloom(at, colour, 240f);
            Ring(at, colour, 420f);
            Ring(at, colour, 640f, 0.07f);
            Burst(at, colour, 14, 500f);
        }

        /// <summary>A soft disc of the character's colour, the part that reads as the hit.</summary>
        public void Bloom(Vector2 at, Color colour, float size = 340f)
        {
            var img = Take(_art != null ? _art.vfxSpark : null, size, glow: true);
            var tint = colour;
            tint.a = 0.55f;
            _live.Add(new Active
            {
                Image = img,
                Rect = (RectTransform)img.transform,
                From = at,
                To = at,
                Start = Time.unscaledTime,
                Duration = 0.72f,
                StartScale = 0.2f,
                EndScale = 1.35f,
                StartColour = tint,
                Heat = 0.35f,
                Hold = 0.08f
            });
        }

        /// <summary>A white-hot pop at the centre of a hit, gone before the streaks land.</summary>
        private void CoreFlash(Vector2 at, Color colour)
        {
            var img = Take(_art != null ? _art.vfxSpark : null, 90f, glow: true);
            _live.Add(new Active
            {
                Image = img,
                Rect = (RectTransform)img.transform,
                From = at,
                To = at,
                Start = Time.unscaledTime,
                    Duration = 0.32f,
                    StartScale = 0.35f,
                    EndScale = 2.1f,
                StartColour = colour,
                Heat = 1f
            });
        }

        /// <summary>Sparks that climb, for the Duke's purple flourish.</summary>
        public void Rise(Vector2 at, Color colour, int count = 16)
        {
            count = Mathf.Clamp(count, 1, 28);
            var spark = _art != null ? _art.vfxSpark : null;
            Bloom(at + new Vector2(0f, 40f), colour, 200f);

            for (var i = 0; i < count; i++)
            {
                var img = Take(spark, Random.Range(12f, 28f), glow: true);
                var tint = Color.Lerp(colour, Color.white, Random.Range(0.15f, 0.55f));
                tint.a = 0.95f;
                _live.Add(new Active
                {
                    Image = img,
                    Rect = (RectTransform)img.transform,
                    From = at + new Vector2(Random.Range(-36f, 36f), Random.Range(-8f, 12f)),
                    Velocity = new Vector2(Random.Range(-50f, 50f), Random.Range(180f, 520f)),
                    Gravity = -160f,
                    Start = Time.unscaledTime + i * 0.018f,
                    Duration = Random.Range(0.7f, 1.15f),
                    StartScale = 1f,
                    EndScale = 0.15f,
                    StartColour = tint,
                    Ballistic = true,
                    Align = true,
                    Stretch = Random.Range(1.8f, 3.4f),
                    Heat = Random.Range(0.2f, 0.6f),
                    Hold = 0.12f
                });
            }
        }

        /// <summary>
        /// Two seats leaning in on each other: a glowing beam, sparks from both
        /// ends meeting in the middle, and a shock on each seat.
        /// </summary>
        public void Clash(Vector2 from, Vector2 to)
        {
            var mid = (from + to) * 0.5f;
            var red = new Color(1f, 0.32f, 0.22f, 1f);
            var gold = new Color(1f, 0.82f, 0.38f, 1f);

            Streak(from, mid, red, 4);
            Streak(to, mid, gold, 4);
            Ring(from, red, 150f);
            Ring(to, gold, 150f);
            CoreFlash(mid, new Color(1f, 0.85f, 0.55f, 1f));
            Ring(mid, gold, 240f);
            Burst(mid, new Color(1f, 0.62f, 0.28f, 1f), 8, 260f);
        }

        /// <summary>A short volley of sparks from one seat to another.</summary>
        public void Streak(Vector2 from, Vector2 to, Color colour, int count = 8)
        {
            count = Mathf.Clamp(count, 1, 16);
            var spark = _art != null ? _art.vfxSpark : null;
            var aim = to - from;
            var angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            if (aim.sqrMagnitude > 64f)
                Beam(from, to, new Color(colour.r, colour.g, colour.b, 0.4f), 16f, 0.34f, 0.15f, glow: true);

            for (var i = 0; i < count; i++)
            {
                var img = Take(spark, Random.Range(14f, 26f), glow: true);
                var jitter = new Vector2(Random.Range(-14f, 14f), Random.Range(-14f, 14f));
                var tint = Color.Lerp(colour, Color.white, Random.Range(0.25f, 0.7f));
                tint.a = 1f;
                _live.Add(new Active
                {
                    Image = img,
                    Rect = (RectTransform)img.transform,
                    From = from + jitter,
                    To = to + jitter * 0.25f,
                    Start = Time.unscaledTime + i * 0.028f,
                    Duration = 0.42f,
                    StartScale = 1f,
                    EndScale = 0.35f,
                    StartColour = tint,
                    Arc = Random.Range(16f, 48f),
                    Angle = angle,
                    Stretch = Random.Range(3.2f, 5.4f),
                    Heat = Random.Range(0.35f, 0.8f),
                    Hold = 0.2f
                });
            }
        }

        /// <summary>A glowing bar between two points. The wide pass is the halo, the thin pass the core.</summary>
        private void Beam(Vector2 from, Vector2 to, Color colour, float thickness, float duration, float hold, bool glow)
        {
            var aim = to - from;
            if (aim.sqrMagnitude < 16f) return;

            var img = Take(_art != null ? _art.vfxSpark : null, thickness, glow);
            img.preserveAspect = false;
            var rect = (RectTransform)img.transform;
            rect.sizeDelta = new Vector2(aim.magnitude, thickness);
            _live.Add(new Active
            {
                Image = img,
                Rect = rect,
                From = (from + to) * 0.5f,
                To = (from + to) * 0.5f,
                Start = Time.unscaledTime,
                Duration = duration,
                StartScale = 1f,
                EndScale = 1f,
                StartColour = colour,
                Angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg,
                Heat = glow ? 0.4f : 0f,
                Hold = hold
            });
        }


        /// <summary>A readable pending cue; no hit plays until the rules resolve it.</summary>
        public void BeginAssassination(Vector2 from, Vector2 to)
        {
            EndAssassination(false);
            if (_root == null || Vector2.Distance(from, to) < 8f) return;
            var sprite = assassinationReticle != null ? assassinationReticle : (_art != null ? _art.roundedBorder : null);
            var cue = Take(sprite, 140f);
            cue.type = Image.Type.Sliced;
            cue.preserveAspect = false;
            cue.rectTransform.sizeDelta = new Vector2(120f, 170f);
            _assassination = new AssassinationCue
            {
                From = from, To = to, Reticle = cue, Started = Time.unscaledTime
            };
            TickAssassination(Time.unscaledTime);
        }

        public void ReleaseAssassination(bool hit, float delay)
        {
            if (_assassination == null) return;
            _assassination.Hit = hit;
            _assassination.ResolveAt = Time.unscaledTime + Mathf.Max(0f, delay);
        }

        public void DeflectAssassination()
        {
            if (_assassination == null) return;
            var cue = _assassination;
            EndAssassination(false);
            StartCoroutine(KnifeStrike(cue.From, cue.To, true));
        }

        public void EndAssassination(bool hit)
        {
            if (_assassination == null) return;
            var cue = _assassination;
            _assassination = null;
            RetireImage(cue.Reticle, 0.12f);
            if (hit) StartCoroutine(KnifeStrike(cue.From, cue.To, false));
        }

        private IEnumerator KnifeStrike(Vector2 from, Vector2 to, bool blocked)
        {
            var direction = (to - from).normalized;
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            if (daggerParticle != null)
            {
                var knife = Take(daggerParticle, 150f);
                _live.Add(new Active
                {
                    Image = knife, Rect = knife.rectTransform, From = from, To = to,
                    Start = Time.unscaledTime, Duration = 0.22f, StartScale = 0.95f, EndScale = 0.95f,
                    StartColour = Color.white, Angle = angle, Hold = 0.92f
                });
            }
            yield return new WaitForSecondsRealtime(0.22f);
            if (blocked)
            {
                var normal = new Vector2(-direction.y, direction.x);
                Glyph(daggerParticle, to, to + normal * 90f - direction * 35f,
                    120f, Color.white, 0.25f, 0f, 1f, 0.6f, angle + 65f, false, 25f);
                Burst(to, new Color(0.65f, 0.8f, 0.9f), 4, 110f);
            }
            else
            {
                Glyph(_art != null ? _art.vfxSpark : null, to, to, 48f,
                    new Color(0.9f, 0.82f, 0.7f, 0.8f), 0.12f, 0f, 0.55f, 1f, 0f);
                Burst(to, new Color(0.75f, 0.5f, 0.3f), 6, 150f);
                Smoke(to, new Color(0.17f, 0.16f, 0.18f, 0.23f), 90f);
            }
        }

        private void TickAssassination(float now)
        {
            if (_assassination == null) return;
            if (_assassination.ResolveAt >= 0f && now >= _assassination.ResolveAt)
            {
                EndAssassination(_assassination.Hit);
                return;
            }
            var cue = _assassination;
            var t = now - cue.Started;
            cue.Reticle.rectTransform.anchoredPosition = cue.To;
            cue.Reticle.rectTransform.localScale = Vector3.one * (1f + 0.012f * Mathf.Sin(t * 3f));
            cue.Reticle.rectTransform.localEulerAngles = Vector3.zero;
            cue.Reticle.color = new Color(0.75f, 0.22f, 0.24f, 0.38f + 0.08f * Mathf.Sin(t * 3f));
        }

        /// <summary>One short royal impact: gather, strike, shockwave, falling metal.</summary>
        public void CoupEffect(Vector2 from, Vector2 to)
        {
            if (_root == null || _coupActive) return;
            StartCoroutine(CoupSequence(from, to));
        }

        private IEnumerator CoupSequence(Vector2 from, Vector2 to)
        {
            _coupActive = true;
            try
            {
                var gold = new Color(1f, 0.72f, 0.24f);
                var red = new Color(1f, 0.19f, 0.13f);
                Flash(new Color(0.01f, 0.01f, 0.025f, 0.14f), 0.45f);
                Glyph(shockwaveParticle, to, to, 260f, new Color(1f, 0.5f, 0.15f, 0.45f),
                    0.28f, 0f, 1.35f, 0.5f, 0f);
                for (var i = 0; i < 10; i++)
                {
                    var angle = i * Mathf.PI * 2f / 10f;
                    var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 145f;
                    Glyph(_art != null ? _art.vfxSpark : null, to + offset, to, 15f, gold,
                        0.28f, 0f, 1f, 0.3f, 0f);
                }
                yield return new WaitForSecondsRealtime(0.28f);
                Beam(to + Vector2.up * 220f, to, new Color(1f, 0.85f, 0.5f, 0.9f),
                    12f, 0.23f, 0.1f, true);
                Glyph(_art != null ? _art.vfxSpark : null, to, to, 82f,
                    new Color(1f, 0.85f, 0.5f, 0.8f), 0.18f, 0f, 0.5f, 1f, 0f);
                Ring(to, gold, 420f);
                Ring(to, red, 560f, 0.08f);
                Smoke(to, new Color(0.4f, 0.17f, 0.1f, 0.45f), 240f);
                Burst(to, gold, 20, 390f);
                for (var i = 0; i < 12; i++)
                {
                    var img = Take(RandomTriangle(), Random.Range(10f, 21f));
                    var angle = Random.Range(0f, Mathf.PI * 2f);
                    _live.Add(new Active
                    {
                        Image = img, Rect = img.rectTransform, From = to,
                        Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(100f, 320f),
                        Gravity = -650f, Start = Time.unscaledTime, Duration = Random.Range(0.55f, 0.85f),
                        StartScale = 1f, EndScale = 0.2f, StartColour = gold, Ballistic = true,
                        Angle = Random.Range(0f, 360f), Spin = Random.Range(-200f, 200f), Hold = 0.1f
                    });
                }
                yield return new WaitForSecondsRealtime(0.85f);
            }
            finally { _coupActive = false; }
        }

        /// <summary>Two cards trade places without implying a hidden role.</summary>
        public void ExchangeEffect(Vector2 at)
        {
            var amber = new Color(1f, 0.67f, 0.22f);
            Ring(at, amber, 200f);
            var back = _art != null ? _art.cardBack : null;
            Glyph(back, at + new Vector2(-72f, -15f), at + new Vector2(72f, 30f),
                90f, Color.white, 0.65f, 0f, 0.6f, 0.85f, -14f, false, 80f);
            Glyph(back, at + new Vector2(72f, 15f), at + new Vector2(-72f, -30f),
                90f, Color.white, 0.65f, 0.08f, 0.6f, 0.85f, 14f, false, 80f);
        }

        private void Smoke(Vector2 at, Color tint, float size)
        {
            if (smokeParticle == null) return;
            Glyph(smokeParticle, at, at + new Vector2(20f, 65f), size, tint,
                0.8f, 0f, 0.65f, 1.25f, Random.Range(-15f, 15f), false);
        }

        private void Glyph(Sprite sprite, Vector2 from, Vector2 to, float size, Color colour,
            float duration, float delay, float startScale, float endScale, float angle,
            bool additive = true, float arc = 0f)
        {
            if (sprite == null) sprite = _art != null ? _art.vfxSpark : null;
            var img = Take(sprite, size, additive);
            _live.Add(new Active
            {
                Image = img, Rect = img.rectTransform, From = from, To = to,
                Start = Time.unscaledTime + delay, Duration = duration, StartScale = startScale,
                EndScale = endScale, StartColour = colour, Angle = angle, Arc = arc, Hold = 0.12f
            });
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            _coupActive = false;
            if (_assassination != null) Release(_assassination.Reticle);
            _assassination = null;
            foreach (var effect in _live) Release(effect.Image);
            _live.Clear();
            for (var i = _banners.Count - 1; i >= 0; i--) RetireBanner(i);
        }

        /// <summary>A brief full-screen wash, for eliminations and the win screen.</summary>
        public void Flash(Color colour, float duration = 0.35f)
        {
            var img = Take(null, 0f);
            var rect = (RectTransform)img.transform;
            rect.sizeDelta = new Vector2(1800, 2800);
            rect.anchoredPosition = Vector2.zero;
            img.sprite = null;

            _live.Add(new Active
            {
                Image = img,
                Rect = rect,
                From = Vector2.zero,
                To = Vector2.zero,
                Start = Time.unscaledTime,
                Duration = duration,
                StartScale = 1f,
                EndScale = 1f,
                StartColour = colour
            });
        }

        // ------------------------------------------------------------- driving

        /// <summary>Drops a title still hanging over this seat so the next action replaces it.</summary>
        public void DismissBanners(int seat)
        {
            for (var i = _banners.Count - 1; i >= 0; i--)
                if (_banners[i].Seat == seat) RetireBanner(i);
        }

        /// <summary>
        /// Slams one title onto a player panel. <paramref name="at"/> is the centre of that panel.
        /// </summary>
        public void ShowBanner(Sprite sprite, Vector2 at, int seat, float width = 280f)
        {
            if (sprite == null || _root == null || sprite.rect.width < 1f) return;

            DismissBanners(seat);
            var height = width * (sprite.rect.height / sprite.rect.width);
            var bounds = _root.rect;
            var halfW = width * 0.5f;
            var halfH = height * 0.5f;
            at.x = Mathf.Clamp(at.x, bounds.xMin + halfW + 8f, bounds.xMax - halfW - 8f);
            at.y = Mathf.Clamp(at.y, bounds.yMin + halfH + 8f, bounds.yMax - halfH - 8f);

            var go = new GameObject("ActionBanner", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_root, false);
            rect.SetAsLastSibling();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = at;
            rect.localScale = Vector3.one * 0.4f;
            rect.localRotation = Quaternion.Euler(0f, 0f, -7f);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            _root.SetAsLastSibling();
            _banners.Add(new Banner
            {
                Seat = seat,
                Rect = rect,
                Group = group,
                At = at,
                From = at,
                To = at,
                Start = Time.unscaledTime
            });
        }

        /// <summary>
        /// Carries a title from one player panel to another. It pops on the challenger,
        /// arcs across, and settles on the player they called.
        /// </summary>
        public void FlyBanner(Sprite sprite, Vector2 from, Vector2 to, int seat, float width = 280f)
        {
            if (sprite == null || _root == null) return;
            ShowBanner(sprite, from, seat, width);

            var banner = _banners.Count > 0 ? _banners[_banners.Count - 1] : null;
            if (banner == null || banner.Rect == null) return;

            var half = banner.Rect.sizeDelta * 0.5f;
            banner.Fly = true;
            banner.From = ClampBanner(from, half);
            banner.To = ClampBanner(to, half);
            banner.At = banner.From;
            banner.Rect.anchoredPosition = banner.From;
        }

        private Vector2 ClampBanner(Vector2 at, Vector2 half)
        {
            if (_root == null) return at;
            var bounds = _root.rect;
            at.x = Mathf.Clamp(at.x, bounds.xMin + half.x + 8f, bounds.xMax - half.x - 8f);
            at.y = Mathf.Clamp(at.y, bounds.yMin + half.y + 8f, bounds.yMax - half.y - 8f);
            return at;
        }

        private void Update()
        {
            var now = Time.unscaledTime;
            TickAssassination(now);
            TickBanners(now);
            if (_live.Count == 0) return;

            for (var i = _live.Count - 1; i >= 0; i--)
            {
                var a = _live[i];
                if (now < a.Start) continue;

                var t = (now - a.Start) / Mathf.Max(a.Duration, 0.01f);
                if (t >= 1f)
                {
                    Release(a.Image);
                    _live.RemoveAt(i);
                    continue;
                }

                var elapsed = now - a.Start;
                if (a.Ballistic)
                {
                    a.Rect.anchoredPosition = a.From + a.Velocity * elapsed +
                                              0.5f * new Vector2(0f, a.Gravity) * elapsed * elapsed;
                }
                else
                {
                    var eased = 1f - Mathf.Pow(1f - t, 3f);
                    var pos = Vector2.Lerp(a.From, a.To, eased);
                    if (a.Arc > 0f) pos.y += Mathf.Sin(t * Mathf.PI) * a.Arc;
                    a.Rect.anchoredPosition = pos;
                }

                var scale = Mathf.Lerp(a.StartScale, a.EndScale, t);
                if (a.Align && a.Ballistic)
                {
                    var vel = a.Velocity + new Vector2(0f, a.Gravity) * elapsed;
                    if (vel.sqrMagnitude > 9f)
                        a.Angle = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg;
                }

                var stretch = a.Stretch < 1f ? 1f : a.Stretch;
                a.Rect.localScale = new Vector3(scale * stretch, scale, 1f);
                a.Rect.localEulerAngles = new Vector3(0f, 0f, a.Angle + a.Spin * elapsed);

                var fadeT = Mathf.InverseLerp(Mathf.Clamp01(a.Hold), 1f, t);
                var fade = 1f - fadeT * fadeT;
                var c = Color.Lerp(a.StartColour, Color.white, a.Heat * (1f - t));
                c.a = a.StartColour.a * fade;
                a.Image.color = c;

                _live[i] = a;
            }
        }

        /// <summary>Pops the title up, holds it, then lets it drift away inside the event pause.</summary>
        private void TickBanners(float now)
        {
            const float pop = 0.22f;
            const float settle = 0.36f;
            const float hold = 0.85f;
            const float end = 1.15f;

            for (var i = _banners.Count - 1; i >= 0; i--)
            {
                var banner = _banners[i];
                if (banner.Rect == null)
                {
                    _banners.RemoveAt(i);
                    continue;
                }

                var t = now - banner.Start;
                if (banner.Fly)
                {
                    if (t >= end) { RetireBanner(i); continue; }
                    TickFlight(banner, t);
                    continue;
                }

                if (t >= end)
                {
                    RetireBanner(i);
                    continue;
                }

                float scale;
                float alpha;
                float rise;
                float angle;
                if (t < pop)
                {
                    var u = EaseOutBack(Mathf.Clamp01(t / pop));
                    scale = Mathf.LerpUnclamped(0.4f, 1.08f, u);
                    alpha = Mathf.Clamp01(t / 0.1f);
                    rise = Mathf.Lerp(-28f, 6f, 1f - Mathf.Pow(1f - Mathf.Clamp01(t / pop), 3f));
                    angle = Mathf.Lerp(-7f, 2f, u);
                }
                else if (t < settle)
                {
                    var u = (t - pop) / (settle - pop);
                    scale = Mathf.Lerp(1.08f, 1f, u);
                    alpha = 1f;
                    rise = Mathf.Lerp(6f, 0f, u);
                    angle = Mathf.Lerp(2f, 0f, u);
                }
                else if (t < hold)
                {
                    scale = 1f;
                    alpha = 1f;
                    rise = 0f;
                    angle = 0f;
                }
                else
                {
                    var u = (t - hold) / (end - hold);
                    scale = Mathf.Lerp(1f, 1.05f, u);
                    alpha = 1f - u;
                    rise = Mathf.Lerp(0f, 18f, u);
                    angle = 0f;
                }

                banner.Rect.localScale = new Vector3(scale, scale, 1f);
                banner.Rect.anchoredPosition = banner.At + new Vector2(0f, rise);
                banner.Rect.localEulerAngles = new Vector3(0f, 0f, angle);
                banner.Group.alpha = alpha;
            }
        }

        /// <summary>Leaves the challenger's panel, arcs, and lands on the player they called.</summary>
        private void TickFlight(Banner banner, float t)
        {
            const float appear = 0.16f;
            const float travelEnd = 0.82f;
            const float life = 1.15f;

            float scale;
            float alpha;
            float angle;
            Vector2 pos;

            if (t < appear)
            {
                var u = EaseOutBack(Mathf.Clamp01(t / appear));
                scale = Mathf.LerpUnclamped(0.35f, 1f, u);
                alpha = Mathf.Clamp01(t / 0.08f);
                pos = banner.From;
                angle = Mathf.Lerp(-8f, 0f, u);
            }
            else if (t < travelEnd)
            {
                var u = (t - appear) / (travelEnd - appear);
                var eased = 1f - Mathf.Pow(1f - u, 3f);
                pos = Vector2.LerpUnclamped(banner.From, banner.To, eased);
                pos.y += Mathf.Sin(u * Mathf.PI) * 72f;
                var tilt = Mathf.Clamp((banner.To.x - banner.From.x) * 0.015f, -14f, 14f);
                scale = Mathf.Lerp(1f, 1.06f, Mathf.Sin(u * Mathf.PI));
                alpha = 1f;
                angle = Mathf.Lerp(tilt, 0f, eased);
            }
            else
            {
                var u = Mathf.Clamp01((t - travelEnd) / (life - travelEnd));
                scale = 1f;
                alpha = 1f - u * u;
                pos = banner.To;
                angle = 0f;
            }

            banner.Rect.localScale = new Vector3(scale, scale, 1f);
            banner.Rect.anchoredPosition = pos;
            banner.Rect.localEulerAngles = new Vector3(0f, 0f, angle);
            banner.Group.alpha = alpha;
        }

        private void RetireBanner(int index)
        {
            var banner = _banners[index];
            _banners.RemoveAt(index);
            if (banner.Rect != null) Destroy(banner.Rect.gameObject);
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        // ---------------------------------------------------------------- pool

        private Image Take(Sprite sprite, float size, bool glow = false)
        {
            Image img = null;
            foreach (var candidate in _pool)
            {
                if (candidate.gameObject.activeSelf) continue;
                img = candidate;
                break;
            }

            if (img == null)
            {
                var go = new GameObject("Vfx", typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(_root, false);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

                img = go.AddComponent<Image>();
                img.raycastTarget = false;
                _pool.Add(img);
            }

            img.gameObject.SetActive(true);
            img.enabled = true;
            img.color = Color.clear;
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.material = glow && _glow != null ? _glow : null;
            img.preserveAspect = true;
            img.transform.localScale = Vector3.one;
            img.transform.localRotation = Quaternion.identity;

            if (size > 0f) ((RectTransform)img.transform).sizeDelta = new Vector2(size, size);
            return img;
        }

        private void Release(Image img)
        {
            if (img == null) return;
            img.gameObject.SetActive(false);
        }

        private void RetireImage(Image img, float duration)
        {
            if (img == null) return;
            var rt = (RectTransform)img.transform;
            _live.Add(new Active
            {
                Image = img,
                Rect = rt,
                From = rt.anchoredPosition,
                To = rt.anchoredPosition,
                Start = Time.unscaledTime,
                Duration = duration,
                StartScale = 1f,
                EndScale = 1f,
                StartColour = img.color,
                Angle = rt.localEulerAngles.z
            });
        }

        private Sprite RandomTriangle()
        {
            if (_triangles == null || _triangles.Length == 0) BuildTriangles();
            return _triangles[Random.Range(0, _triangles.Length)];
        }

        private void BuildTriangles()
        {
            var shapes = new[]
            {
                new[] { new Vector2(0.50f, 0.08f), new Vector2(0.12f, 0.92f), new Vector2(0.88f, 0.78f) },
                new[] { new Vector2(0.18f, 0.12f), new Vector2(0.92f, 0.28f), new Vector2(0.42f, 0.94f) },
                new[] { new Vector2(0.08f, 0.20f), new Vector2(0.90f, 0.08f), new Vector2(0.55f, 0.96f) },
                new[] { new Vector2(0.30f, 0.06f), new Vector2(0.06f, 0.84f), new Vector2(0.94f, 0.62f) },
                new[] { new Vector2(0.62f, 0.04f), new Vector2(0.10f, 0.48f), new Vector2(0.86f, 0.94f) }
            };

            _triangles = new Sprite[shapes.Length];
            _triangleTex = new Texture2D[shapes.Length];
            for (var i = 0; i < shapes.Length; i++)
                _triangles[i] = HardTriangle(shapes[i][0], shapes[i][1], shapes[i][2], out _triangleTex[i]);
        }

        /// <summary>A hard-edged triangle. Coordinates are 0–1 inside the texture.</summary>
        private static Sprite HardTriangle(Vector2 a, Vector2 b, Vector2 c, out Texture2D tex)
        {
            const int size = 64;
            tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                name = "ImperialShard"
            };

            var pa = a * (size - 1);
            var pb = b * (size - 1);
            var pc = c * (size - 1);
            var px = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var p = new Vector2(x, y);
                var area = Edge(pb, pc, pa);
                var w0 = Edge(pb, pc, p) / area;
                var w1 = Edge(pc, pa, p) / area;
                var w2 = Edge(pa, pb, p) / area;
                var inside = w0 >= 0f && w1 >= 0f && w2 >= 0f;
                if (area < 0f) inside = w0 <= 0f && w1 <= 0f && w2 <= 0f;
                var dist = inside ? Mathf.Min(Mathf.Abs(w0), Mathf.Min(Mathf.Abs(w1), Mathf.Abs(w2))) * Mathf.Abs(area) : -1f;
                var alpha = inside ? Mathf.Clamp01(dist) : 0f;
                var v = (byte)(inside ? 255 : 0);
                px[y * size + x] = new Color32(v, v, v, (byte)Mathf.RoundToInt(alpha * 255f));
            }

            tex.SetPixels32(px);
            tex.Apply(false, false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, Vector4.zero);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static float Edge(Vector2 a, Vector2 b, Vector2 p) =>
            (p.x - a.x) * (b.y - a.y) - (p.y - a.y) * (b.x - a.x);

        /// <summary>Converts a UI element's position into the effects layer's space.</summary>
        public Vector2 PositionOf(RectTransform target)
        {
            if (target == null || _root == null) return Vector2.zero;
            var world = target.TransformPoint(Vector3.zero);
            return _root.InverseTransformPoint(world);
        }
    }
}
