using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Coup.Core;

namespace Coup.Game.UI
{
    /// <summary>
    /// One action in the bottom bar. Carries its cost and a one-line explanation so a
    /// new player can read the board instead of memorising the rulebook, and reports
    /// long-presses so the full detail panel can be opened.
    /// </summary>
    public class ActionButtonView : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Layers")]
        public RectTransform root;
        public CanvasGroup group;
        public Image background;
        public Image border;
        public Image icon;
        public TextMeshProUGUI label;
        public TextMeshProUGUI cost;
        public Image lockIcon;
        public Color accentColor = new Color(0.12f, 0.72f, 1f);

        public event Action<ActionKind> Clicked;
        public event Action<ActionKind> HeldForInfo;

        /// <summary>Drops click handlers so a second bind does not fire twice.</summary>
        public void ClearHooks()
        {
            Clicked = null;
            HeldForInfo = null;
        }

        public ActionKind Kind { get; private set; }
        public bool Available { get; private set; } = true;

        private bool _chosen, _supported;
        private Image _supportGlow, _supportCard;
        private Color _supportColor, _baseBackground;

        private const float HoldSeconds = 0.45f;
        private float _downAt = -1f;
        private bool _infoFired;

        private void Awake()
        {
            if (background != null) _baseBackground = background.color;
            if (root == null) root = (RectTransform)transform;
            if (group == null) group = gameObject.GetComponent<CanvasGroup>();
        }

        public void Initialise(ActionKind kind, CoupArt art)
        {
            Kind = kind;
            var entry = art != null ? art.For(kind) : null;

            if (icon != null)
            {
                icon.sprite = entry?.icon;
                icon.enabled = icon.sprite != null;
            }

            if (label != null)
            {
                label.text = (entry?.label ?? kind.ToString()).ToUpperInvariant();
                label.enableAutoSizing = true;
                label.fontSizeMin = 17f;
                label.fontSizeMax = 24f;
            }

            if (cost != null) cost.text = entry?.subtitle ?? "";
        }

        /// <summary>The line under the name: what this tap actually does.</summary>
        public void SetEffect(string effect)
        {
            if (cost == null) return;
            cost.text = effect ?? "";
            cost.enableAutoSizing = true;
            cost.fontSizeMin = 15f;
            cost.fontSizeMax = 19f;
        }

        /// <summary>Greys the button and shows a lock when the action is not legal now.</summary>
        public void SetAvailable(bool value)
        {
            if (Available == value) return;
            Available = value;

            if (group != null) group.alpha = value ? 1f : 0.55f;
            if (lockIcon != null) lockIcon.gameObject.SetActive(!value);
            ApplyFrame();
        }

        /// <summary>Marks the action the player is in the middle of, such as picking a target.</summary>
        public void SetChosen(bool chosen)
        {
            if (_chosen == chosen) return;
            _chosen = chosen;
            ApplyFrame();
        }

        public void SetSupportedByHand(bool supported, CoupArt art)
        {
            var role = Rules.ClaimedRole(Kind);
            _supported = supported;
            _supportColor = role.HasValue && art != null ? Color.Lerp(art.Accent(role.Value), Color.white, .35f) : UIFactory.Gold;
            if (_supportGlow == null)
            {
                _supportGlow = UIFactory.Image("HandSupportGlow", transform, border != null ? border.sprite : null,
                    Vector2.zero, Vector2.zero, false);
                var rt = _supportGlow.rectTransform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(-7,-7); rt.offsetMax = new Vector2(7,7);
                _supportGlow.type = border != null ? border.type : Image.Type.Sliced;
                _supportGlow.transform.SetAsFirstSibling();
                _supportCard = UIFactory.Image("MatchingCard", transform, null, new Vector2(44,64), new Vector2(82,47));
            }
            _supportGlow.gameObject.SetActive(supported && Available);
            _supportCard.gameObject.SetActive(supported);
            if (supported && role.HasValue && art != null)
            {
                _supportCard.sprite = art.CardFace(role.Value);
                _supportCard.color = Color.white;
            }
            // Main action icon retains its full size; the matching card sits beside it.
            if (icon != null) { icon.rectTransform.anchoredPosition = new Vector2(0,39); icon.rectTransform.localScale = Vector3.one; }
            if (background != null) background.color = supported && Available
                ? Color.Lerp(_baseBackground, new Color(_supportColor.r*.28f,_supportColor.g*.28f,_supportColor.b*.28f,1), .85f)
                : _baseBackground;
            ApplyFrame();
        }

        private void ApplyFrame()
        {
            if (border == null) return;
            border.color = _chosen
                ? new Color(1f, 0.93f, 0.55f, 1f)
                : Available && _supported ? _supportColor : Available ? accentColor : new Color(accentColor.r * 0.5f, accentColor.g * 0.5f, accentColor.b * 0.5f, 0.7f);
        }

        private void Update()
        {
            if (_supportGlow != null && _supported && Available)
            {
                var pulse = PlayerPrefs.GetInt("Coup.ReducedMotion",0) != 0 ? .65f : .55f+.25f*Mathf.Sin(Time.unscaledTime*3);
                _supportGlow.color = new Color(_supportColor.r,_supportColor.g,_supportColor.b,pulse);
            }
            if (_downAt < 0f || _infoFired) return;
            if (Time.unscaledTime - _downAt < HoldSeconds) return;

            _infoFired = true;
            HeldForInfo?.Invoke(Kind);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _downAt = Time.unscaledTime;
            _infoFired = false;
            if (Available) CoupTween.ScaleTo(root, Vector3.one * 0.94f, CoupTween.Quick);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _downAt = -1f;
            CoupTween.ScaleTo(root, Vector3.one, CoupTween.Quick);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_infoFired) return; // the press was a hold for information

            if (!Available)
            {
                // Say why instead of silently doing nothing.
                CoupTween.Shake(root, 7f, CoupTween.Quick);
                HeldForInfo?.Invoke(Kind);
                return;
            }

            Clicked?.Invoke(Kind);
        }
    }
}
