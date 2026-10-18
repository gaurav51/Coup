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

        private bool _chosen;

        private const float HoldSeconds = 0.45f;
        private float _downAt = -1f;
        private bool _infoFired;

        private void Awake()
        {
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
                label.text = entry?.label ?? kind.ToString();
                // "Assassinate" is wider than the button at the authored 20pt, so it
                // truncated mid-word. The longest names shrink instead; short ones are
                // unchanged because the maximum is the size the prefab already used.
                label.enableAutoSizing = true;
                label.fontSizeMin = 13f;
                label.fontSizeMax = 20f;
            }

            if (cost != null) cost.text = entry?.subtitle ?? "";
        }

        /// <summary>The line under the name: what this tap actually does.</summary>
        public void SetEffect(string effect)
        {
            if (cost == null) return;
            cost.text = effect ?? "";
            cost.enableAutoSizing = true;
            cost.fontSizeMin = 12f;
            cost.fontSizeMax = 16f;
        }

        /// <summary>Greys the button and shows a lock when the action is not legal now.</summary>
        public void SetAvailable(bool value)
        {
            if (Available == value) return;
            Available = value;

            if (group != null) group.alpha = value ? 1f : 0.35f;
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

        private void ApplyFrame()
        {
            if (border == null) return;
            border.color = _chosen
                ? new Color(1f, 0.93f, 0.55f, 1f)
                : Available ? UIFactory.Gold : new Color(0.4f, 0.4f, 0.45f, 0.7f);
        }

        private void Update()
        {
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
