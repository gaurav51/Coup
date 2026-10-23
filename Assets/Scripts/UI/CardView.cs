using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Coup.Core;

namespace Coup.Game.UI
{
    /// <summary>
    /// One influence card. Composites the faction backdrop, the cut-out portrait and
    /// a tinted frame, then draws the name and ability with TextMeshPro so the card
    /// stays crisp and translatable rather than relying on the baked-in captions.
    /// </summary>
    public class CardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler,
        IPointerExitHandler, IPointerMoveHandler
    {
        [Header("Layers")]
        public RectTransform root;
        public CanvasGroup group;
        public Image backdrop;
        public Image portrait;
        public Image scrim;
        public Image frame;
        public Image glow;
        public GameObject back;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI abilityText;

        [Tooltip("Optional foil sweep overlay. Left unset, the card simply does not shine.")]
        public CardShine shine;

        [Header("State")]
        public bool interactable;

        public event Action<CardView> Clicked;

        public Role? Role { get; private set; }
        public bool IsFaceDown { get; private set; }

        private CoupArt _art;
        private bool _dimmed;
        private bool _highlighted;
        private bool _actionSupported;
        private Image _actionSupportRim;
        private bool _compact;
        private bool _flipping;
        private bool _dealing;

        private bool _hover;
        private Vector2 _pointer;
        private Vector2 _parallax;
        private float _phase;
        private Vector2 _portraitRest;
        private Vector2 _backdropRest;
        private bool _parallaxReady;

        /// <summary>True while a deal or a flip owns this card's transform.</summary>
        public bool IsAnimating => _dealing || _flipping;

        /// <summary>
        /// The scale this card rests at. Callers set it instead of rewriting the
        /// prefab's rect, so the authored layout is never overwritten and tweens
        /// always have a correct value to return to.
        /// </summary>
        public Vector3 RestingScale { get; private set; } = Vector3.one;

        public void SetRestingScale(float uniform)
        {
            RestingScale = Vector3.one * uniform;
            if (!_flipping && !_dealing && root != null) root.localScale = RestingScale;
        }

        /// <summary>The size the prefab was authored at, used to derive display scale.</summary>
        public Vector2 AuthoredSize { get; private set; } = new Vector2(300, 440);

        private void Awake()
        {
            if (root == null) root = (RectTransform)transform;
            if (group == null) group = gameObject.GetComponent<CanvasGroup>();

            var size = root.rect.size;
            if (size.x > 1f && size.y > 1f) AuthoredSize = size;
            _phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        }

        private void Start() => CacheParallax();

        /// <summary>Scales the card to a target on-screen width, keeping its proportions.</summary>
        public void SetDisplayWidth(float width)
        {
            if (AuthoredSize.x <= 0f) return;
            SetRestingScale(width / AuthoredSize.x);
        }

        public void Bind(CoupArt art) => _art = art;

        /// <summary>
        /// Marks this as a seat thumbnail. It no longer forces anything off: whether
        /// a card shows a caption is decided in the prefab, and overriding that here
        /// would undo deliberate art direction.
        /// </summary>
        public void SetCompact(bool compact) => _compact = compact;

        /// <summary>Shows a face-up card. Pass animate to flip rather than cut.</summary>
        public void ShowRole(Role role, bool animate = false)
        {
            if (animate && !_flipping && (IsFaceDown || Role != role))
            {
                _flipping = true;
                CoupTween.Flip(root, () => ApplyRole(role), CoupTween.Normal,
                    () => { _flipping = false; root.localScale = RestingScale; },
                    RestingScale);
                return;
            }

            // Interrupting a flip would otherwise strand the card at a partial scale.
            if (_flipping)
            {
                CoupTween.Kill(root);
                root.localScale = RestingScale;
                _flipping = false;
            }

            ApplyRole(role);
        }

        private void ApplyRole(Role role)
        {
            Role = role;
            IsFaceDown = false;

            var entry = _art != null ? _art.For(role) : null;

            // Assign the artwork and nothing else. Its colour, enabled state and
            // aspect handling are the prefab's business; adjusting them here would
            // override deliberate art direction, and a missing sprite should be
            // reported rather than quietly hidden.
            if (backdrop != null) backdrop.sprite = entry?.backdrop;

            if (portrait != null)
            {
                var sprite = entry?.portrait != null ? entry.portrait : entry?.cardFace;
                if (sprite == null) Debug.LogWarning($"[Coup] No portrait art for {role}.");
                portrait.sprite = sprite;
            }

            // Captions and the frame are shown or hidden in the prefab, not here.
            if (nameText != null)
            {
                nameText.text = entry?.displayName ?? role.ToString().ToUpperInvariant();
                UIFactory.SetFont(nameText, false);
            }
            if (abilityText != null)
            {
                abilityText.text = entry?.ability ?? "";
                UIFactory.SetFont(abilityText, true);
            }

            if (back != null) back.SetActive(false);

            // The overlay takes the role accent exactly. The artwork underneath
            // is never recolored.
            if (shine != null)
            {
                shine.SetTint(entry != null ? entry.accent : UIFactory.Gold);
                shine.PlaySweep();
            }
        }

        /// <summary>Shows the card back — an opponent's hidden influence.</summary>
        public void ShowBack()
        {
            Role = null;
            IsFaceDown = true;

            if (back != null)
            {
                back.SetActive(true);
                var img = back.GetComponent<Image>();
                if (img != null && _art != null && _art.cardBack != null) img.sprite = _art.cardBack;
            }

            // A hidden card has no faction accent, so the shimmer stays gold.
            if (shine != null) shine.SetTint(UIFactory.Gold);
        }

        /// <summary>
        /// Tosses the card from the deck to its seat. It stays face down in flight;
        /// a local card flips open once it lands.
        /// </summary>
        public void BeginDeal(Vector2 from, Vector2 to, float delay, bool reveal, Role? role)
        {
            _dealing = true;
            CoupTween.Kill(root);
            if (group != null) CoupTween.Kill(group);
            ShowBack();
            ResetParallax();

            var rt = (RectTransform)transform;
            rt.anchoredPosition = from;
            rt.localRotation = Quaternion.identity;
            rt.localScale = RestingScale * 0.42f;
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
            }

            CoupTween.DelayedCall(delay, () =>
            {
                if (this == null) return;
                if (group != null) CoupTween.Fade(group, 1f, 0.16f);
                CoupTween.ArcTo(rt, to, RestingScale, 0.46f, 110f, () =>
                {
                    _dealing = false;
                    if (rt != null) rt.localRotation = Quaternion.identity;
                    if (group != null) group.blocksRaycasts = true;
                    if (reveal && role.HasValue) ShowRole(role.Value, true);
                    else if (root != null) root.localScale = RestingScale;
                });
            });
        }

        /// <summary>
        /// Spent influence stays on the table, faded back. Done with the card's
        /// CanvasGroup rather than by tinting the artwork, so the images themselves
        /// are never recoloured.
        /// </summary>
        public void SetDimmed(bool dimmed)
        {
            if (_dimmed == dimmed) return;
            _dimmed = dimmed;

            if (group != null) CoupTween.Fade(group, dimmed ? 0.45f : 1f, CoupTween.Quick);
        }

        public void SetHighlight(bool on)
        {
            if (glow == null || _highlighted == on) return;
            _highlighted = on;

            glow.gameObject.SetActive(on);
            if (on)
            {
                glow.color = new Color(1f, 0.85f, 0.4f, 0f);
                CoupTween.ColorTo(glow, new Color(1f, 0.85f, 0.4f, 0.9f), CoupTween.Quick);
                CoupTween.Punch(root, 0.06f, CoupTween.Quick, RestingScale);
            }
        }

        public void SetActionSupport(bool on)
        {
            _actionSupported=on;
            if (_actionSupportRim==null)
            {
                _actionSupportRim=UIFactory.Image("ActionSupportRim",transform,frame!=null?frame.sprite:null,Vector2.zero,Vector2.zero,false);
                var rt=_actionSupportRim.rectTransform;rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;
                rt.offsetMin=new Vector2(-5,-5);rt.offsetMax=new Vector2(5,5);
                _actionSupportRim.type=frame!=null?frame.type:Image.Type.Sliced;
            }
            _actionSupportRim.gameObject.SetActive(on);
            if(on&&Role.HasValue)
            {
                var color=Color.Lerp(_art!=null?_art.Accent(Role.Value):UIFactory.Gold,Color.white,.35f);
                _actionSupportRim.color=color;
            }
        }

        public void SetInteractable(bool value)
        {
            interactable = value;
            if (group != null) group.blocksRaycasts = true;
        }

        /// <summary>Plays the "this card just left play" beat.</summary>
        public void PlayLost(Action onComplete = null)
        {
            CoupTween.Shake(root, 14f, CoupTween.Slow);
            CoupTween.DelayedCall(CoupTween.Slow, () =>
            {
                SetDimmed(true);
                onComplete?.Invoke();
            });
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!interactable) return;
            CoupTween.Punch(root, 0.1f, CoupTween.Quick, RestingScale);
            Clicked?.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData eventData) => _hover = true;

        public void OnPointerExit(PointerEventData eventData)
        {
            _hover = false;
            _pointer = Vector2.zero;
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (root == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    root, eventData.position, eventData.enterEventCamera, out var local))
                return;

            var size = root.rect.size;
            _pointer = new Vector2(
                Mathf.Clamp(local.x / Mathf.Max(size.x * 0.5f, 1f), -1f, 1f),
                Mathf.Clamp(local.y / Mathf.Max(size.y * 0.5f, 1f), -1f, 1f));
            _hover = true;
        }

        private void Update()
        {
            // The deal tween owns rotation. A flip only owns scale, so the layers
            // still need to sit still underneath it.
            if (_dealing) return;
            if (_flipping)
            {
                ApplyParallax(Vector2.zero);
                return;
            }

            var idle = Vector2.zero;
            if (!_compact && !_hover)
            {
                var time = Time.unscaledTime;
                idle = new Vector2(
                    Mathf.Sin(time * 0.65f + _phase),
                    Mathf.Cos(time * 0.5f + _phase)) * 0.22f;
            }

            var target = _hover ? _pointer : idle;
            var k = 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime);
            _parallax = Vector2.Lerp(_parallax, target, k);
            ApplyParallax(_parallax);
        }

        private void CacheParallax()
        {
            if (_parallaxReady) return;
            if (portrait != null) _portraitRest = portrait.rectTransform.anchoredPosition;
            if (backdrop != null) _backdropRest = backdrop.rectTransform.anchoredPosition;
            _parallaxReady = true;
        }

        private void ResetParallax()
        {
            _parallax = Vector2.zero;
            _hover = false;
            ApplyParallax(Vector2.zero);
        }

        /// <summary>
        /// Tilts the card and slides the portrait against the backdrop. The frame
        /// stays put, so the art reads as sitting behind the border.
        /// </summary>
        private void ApplyParallax(Vector2 amount)
        {
            CacheParallax();

            var depth = _compact ? 0.35f : 1f;
            if (root != null && !_dealing)
            {
                root.localRotation = Quaternion.Euler(-amount.y * 6f * depth, amount.x * 8f * depth, 0f);
            }

            var slide = new Vector2(amount.x, amount.y);
            if (portrait != null)
                portrait.rectTransform.anchoredPosition = _portraitRest + slide * (18f * depth);
            if (backdrop != null)
                backdrop.rectTransform.anchoredPosition = _backdropRest - slide * (10f * depth);
            if (shine != null) shine.SetShift(amount.x * 0.16f * depth);
        }
    }
}
