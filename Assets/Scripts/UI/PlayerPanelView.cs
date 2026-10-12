using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Coup.Core;

namespace Coup.Game.UI
{
    /// <summary>
    /// An opponent's seat: who they are, what they hold, and — crucially for a
    /// bluffing game — the character they most recently claimed, so the table can be
    /// read without remembering every announcement.
    /// </summary>
    public class PlayerPanelView : MonoBehaviour, IPointerClickHandler
    {
        [Header("Layers")]
        public RectTransform root;
        public CanvasGroup group;
        public Image background;
        public Image border;
        public Image turnGlow;
        public Image targetGlow;
        public TextMeshProUGUI avatarInitial;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI coinText;
        public Image coinIcon;
        public RectTransform cardSlots;

        [Header("Claim badge")]
        public GameObject claimBadge;
        public TextMeshProUGUI claimText;

        public event Action<PlayerPanelView> Clicked;

        /// <summary>Drops click handlers so a second bind does not fire twice.</summary>
        public void ClearHooks() => Clicked = null;

        public int Seat { get; private set; }
        public bool Targetable { get; private set; }

        private CoupArt _art;
        private CardView _cardPrefab;
        private readonly List<CardView> _cards = new();
        private int _lastCoins = -1;
        private bool _isTurn;
        private Vector3 _seatScale = Vector3.one;

        private void Awake()
        {
            if (root == null) root = (RectTransform)transform;
            if (group == null) group = gameObject.GetComponent<CanvasGroup>();
        }

        public void Initialise(int seat, CoupArt art, CardView cardPrefab)
        {
            Seat = seat;
            _art = art;
            _cardPrefab = cardPrefab;
            if (coinIcon != null) coinIcon.enabled = false;
            if (coinText != null)
            {
                var coins = coinText.rectTransform;
                coins.anchorMin = coins.anchorMax = coins.pivot = new Vector2(0.5f, 1f);
                coins.anchoredPosition = new Vector2(0f, -168f);
                coins.sizeDelta = new Vector2(160f, 40f);
                coinText.alignment = TextAlignmentOptions.Center;
                coinText.richText = true;
                if (coinText.spriteAsset == null)
                    coinText.spriteAsset = TMP_Settings.defaultSpriteAsset;
            }
            if (claimText != null)
            {
                claimText.enableAutoSizing = true;
                claimText.fontSizeMin = 13f;
                claimText.fontSizeMax = 18f;
            }
        }

        /// <summary>
        /// Shrinks the whole seat so five opponents still sit around the table.
        /// Punches must return to this scale, or the seat snaps back to full size.
        /// </summary>
        public void SetSeatScale(float scale)
        {
            _seatScale = Vector3.one * Mathf.Max(0.4f, scale);
            if (root != null) root.localScale = _seatScale;
        }

        public void Refresh(PlayerState player, bool isActing, bool targetable, Role? lastClaim, string deed = null)
        {
            if (nameText != null) nameText.text = player.Name;
            if (avatarInitial != null)
                avatarInitial.text = string.IsNullOrEmpty(player.Name) ? "?" : player.Name.Substring(0, 1).ToUpperInvariant();

            RefreshCoins(player.Coins);
            RefreshCards(player);
            RefreshClaim(lastClaim, deed);

            SetTurn(isActing);
            SetTargetable(targetable);
            SetSeatFaded(!player.IsAlive);
        }

        /// <summary>
        /// A eliminated seat goes quiet, but the cards they lost stay solid.
        /// Fading the whole panel would fade those faces too, since a parent
        /// CanvasGroup multiplies into its children.
        /// </summary>
        private void SetSeatFaded(bool faded)
        {
            if (group != null) group.alpha = 1f;
            Fade(background, faded ? 0.4f : 1f);
            Fade(nameText, faded ? 0.65f : 1f);
            Fade(avatarInitial, faded ? 0.65f : 1f);
            Fade(coinText, faded ? 0.65f : 1f);
            Fade(coinIcon, faded ? 0.65f : 1f);
        }

        private static void Fade(Graphic graphic, float alpha)
        {
            if (graphic == null) return;
            var color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }

        private void RefreshCoins(int coins)
        {
            if (coinText == null) return;
            coinText.text = $"<sprite name=\"coin\" tint=0> {coins}";

            // Pop the counter only when it actually moves, so the eye follows the money.
            if (_lastCoins >= 0 && coins != _lastCoins)
                CoupTween.Punch(coinText.transform, 0.3f, CoupTween.Quick);
            _lastCoins = coins;
        }

        private void RefreshCards(PlayerState player)
        {
            var needed = player.InfluenceCount + player.Revealed.Count;
            EnsureCards(needed);

            for (var i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                var used = i < needed;
                card.gameObject.SetActive(used);
                if (!used) continue;

                if (card.IsAnimating) continue;

                if (i < player.InfluenceCount)
                {
                    card.ShowBack();
                    card.SetDimmed(false);
                }
                else
                {
                    // A lost card is public. Keep the face solid so the table can read it.
                    var role = player.Revealed[i - player.InfluenceCount];
                    if (card.Role != role) card.ShowRole(role, animate: true);
                    card.SetDimmed(false);
                }
            }

            if (cardSlots != null) cardSlots.SetAsLastSibling();
            LayoutCards(player.InfluenceCount, player.Revealed.Count);
        }

        public CardView GetCard(int index) =>
            index >= 0 && index < _cards.Count ? _cards[index] : null;

        private void EnsureCards(int count)
        {
            while (_cards.Count < count)
            {
                var card = Instantiate(_cardPrefab, cardSlots);
                card.Bind(_art);
                card.SetCompact(true);
                card.SetInteractable(false);
                _cards.Add(card);
            }
        }

        /// <summary>
        /// Sizes as well as positions the seat cards. Face-down and lost cards
        /// share one size, hung below the name so the seat stays readable.
        /// </summary>
        private void LayoutCards(int influence, int revealed)
        {
            var count = influence + revealed;
            if (count <= 0 || cardSlots == null) return;

            const float width = 210f;
            const float gap = 8f;
            const float y = -120f;

            var span = count * width + Mathf.Max(0, count - 1) * gap;
            var x = -span * 0.5f;
            for (var i = 0; i < count; i++)
            {
                if (!_cards[i].IsAnimating)
                {
                    _cards[i].SetDisplayWidth(width);
                    var rt = (RectTransform)_cards[i].transform;
                    rt.anchoredPosition = new Vector2(x + width * 0.5f, y);
                    if (i >= influence) _cards[i].transform.SetAsLastSibling();
                }

                x += width + gap;
            }
        }

        private void RefreshClaim(Role? claim, string deed)
        {
            if (claimBadge == null) return;

            var show = claim.HasValue || !string.IsNullOrEmpty(deed);
            if (claimBadge.activeSelf != show) claimBadge.SetActive(show);
            if (!show) return;

            var text = DeedText(claim, deed);
            if (claimText != null && claimText.text != text)
            {
                claimText.text = text;
                claimText.color = claim.HasValue
                    ? UIFactory.Readable(_art?.Accent(claim.Value) ?? Color.white)
                    : UIFactory.Gold;
                CoupTween.PopIn((RectTransform)claimBadge.transform, UIFactory.Group(claimBadge), CoupTween.Quick);
            }
        }

        private string DeedText(Role? claim, string deed)
        {
            var character = claim.HasValue ? (_art?.DisplayName(claim.Value) ?? claim.Value.ToString()) : null;
            if (!string.IsNullOrEmpty(deed) && character != null) return $"{deed} · {character}";
            if (!string.IsNullOrEmpty(deed)) return deed;
            return $"claims {character}";
        }

        public void SetTurn(bool on)
        {
            if (turnGlow == null || _isTurn == on) return;
            _isTurn = on;
            turnGlow.gameObject.SetActive(on);
            if (on) CoupTween.Punch(root, 0.05f, CoupTween.Normal, _seatScale);
        }

        public void SetTargetable(bool value)
        {
            Targetable = value;
            if (targetGlow == null) return;

            if (targetGlow.gameObject.activeSelf != value)
            {
                targetGlow.gameObject.SetActive(value);
                if (value) CoupTween.PopIn(root, null, CoupTween.Quick, _seatScale);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!Targetable) return;
            Clicked?.Invoke(this);
        }
    }
}
