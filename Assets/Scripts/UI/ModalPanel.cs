using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Coup.Core;

namespace Coup.Game.UI
{
    /// <summary>
    /// The single overlay used for every decision and explanation: action details,
    /// the role reference, choosing which character to claim when blocking, picking
    /// an influence to give up, the Ambassador's exchange, and the result screen.
    /// One component keeps all of them looking and behaving the same.
    /// </summary>
    public class ModalPanel : MonoBehaviour
    {
        private RectTransform _root;
        private CanvasGroup _group;
        private Image _dim;
        private RectTransform _sheet;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _body;
        private RectTransform _cardRow;
        private RectTransform _buttonRow;
        private Button _confirm;
        private TextMeshProUGUI _confirmLabel;
        private Button _dismiss;

        private CoupArt _art;
        private CardView _cardPrefab;

        private readonly List<CardView> _cards = new();
        private readonly List<GameObject> _buttons = new();
        private readonly List<GameObject> _captions = new();
        private readonly HashSet<int> _picked = new();
        private int _epoch;
        private bool _open;
        // Vertical extent of whatever sits between the body and the footer, in its row's space.
        private RectTransform _middle;
        private float _middleTop, _middleBottom;

        private const float SheetWidth = 980f, InnerWidth = 840f;

        private int _pickCount;
        private Action<IReadOnlyList<Role>> _onConfirm;
        private IReadOnlyList<Role> _pool;

        public bool IsOpen => _open;

        /// <summary>Reconnects a modal that was saved in the scene.</summary>
        public bool TryBind(CoupArt art, CardView cardPrefab)
        {
            var modal = transform.Find("Modal") as RectTransform;
            if (modal == null) return false;

            _art = art;
            _cardPrefab = cardPrefab;
            _root = modal;
            _group = modal.GetComponent<CanvasGroup>();
            _dim = modal.Find("Dim")?.GetComponent<Image>();
            _sheet = modal.Find("Sheet") as RectTransform;
            if (_sheet == null) return false;

            _title = _sheet.Find("Title")?.GetComponent<TextMeshProUGUI>();
            _body = _sheet.Find("Body")?.GetComponent<TextMeshProUGUI>();
            _cardRow = _sheet.Find("Cards") as RectTransform;
            _buttonRow = _sheet.Find("Buttons") as RectTransform;
            _confirm = _sheet.Find("Confirm")?.GetComponent<Button>();
            _confirmLabel = _confirm != null ? _confirm.transform.Find("Label")?.GetComponent<TextMeshProUGUI>() : null;
            _dismiss = _sheet.Find("Close")?.GetComponent<Button>();

            if (_confirm != null)
            {
                _confirm.onClick.RemoveAllListeners();
                _confirm.onClick.AddListener(OnConfirm);
            }

            if (_dismiss != null)
            {
                _dismiss.onClick.RemoveAllListeners();
                _dismiss.onClick.AddListener(Hide);
            }
            ApplyTheme();
            return true;
        }

        public void Build(RectTransform parent, CoupArt art, CardView cardPrefab)
        {
            _art = art;
            _cardPrefab = cardPrefab;

            _root = UIFactory.Rect("Modal", parent, new Vector2(1080, 1920), Vector2.zero);
            _group = UIFactory.Group(_root.gameObject);

            _dim = UIFactory.Image("Dim", _root, null, new Vector2(1200, 2100), Vector2.zero, false);
            _dim.color = new Color(0.01f, 0.02f, 0.04f, 0.88f);
            _dim.raycastTarget = true;

            _sheet = UIFactory.Rect("Sheet", _root, new Vector2(980, 1180), Vector2.zero);

            var bg = UIFactory.Image("Bg", _sheet, art != null ? art.roundedPanel : null, new Vector2(980, 1180), Vector2.zero, false);
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.055f, 0.078f, 0.141f, 0.99f);

            var border = UIFactory.Image("Border", _sheet, art != null ? art.roundedBorder : null, new Vector2(980, 1180), Vector2.zero, false);
            border.type = Image.Type.Sliced;
            border.color = new Color(0.84f, 0.69f, 0.33f, 0.85f);

            _title = UIFactory.Text("Title", _sheet, "", 48, new Vector2(880, 70), new Vector2(0, 500));
            _title.fontStyle = FontStyles.Bold;
            _title.color = UIFactory.Gold;

            _body = UIFactory.Text("Body", _sheet, "", 28, new Vector2(860, 240), new Vector2(0, 340));
            _body.alignment = TextAlignmentOptions.Top;

            _cardRow = UIFactory.Rect("Cards", _sheet, new Vector2(930, 560), new Vector2(0, -90));
            _buttonRow = UIFactory.Rect("Buttons", _sheet, new Vector2(900, 320), new Vector2(0, -120));

            _confirm = UIFactory.Button("Confirm", _sheet, art != null ? art.roundedPanel : null, new Vector2(420, 110), new Vector2(0, -470));
            _confirm.GetComponent<Image>().type = Image.Type.Sliced;
            _confirm.GetComponent<Image>().color = new Color(0.16f, 0.36f, 0.24f, 1f);
            _confirmLabel = UIFactory.Text("Label", _confirm.transform, "CONFIRM", 34, new Vector2(420, 60), Vector2.zero);
            _confirmLabel.fontStyle = FontStyles.Bold;
            _confirm.onClick.AddListener(OnConfirm);

            _dismiss = UIFactory.Button("Close", _sheet, art != null ? art.roundedPanel : null, new Vector2(420, 110), new Vector2(0, -470));
            _dismiss.GetComponent<Image>().type = Image.Type.Sliced;
            _dismiss.GetComponent<Image>().color = new Color(0.16f, 0.19f, 0.28f, 1f);
            UIFactory.Text("Label", _dismiss.transform, "CLOSE", 34, new Vector2(420, 60), Vector2.zero).fontStyle = FontStyles.Bold;
            _dismiss.onClick.AddListener(Hide);

            ApplyTheme();
            _root.gameObject.SetActive(false);
        }

        private void ApplyTheme() {
            PanelTheme.Title(_title);
            PanelTheme.Surface(_sheet.Find("Bg")?.GetComponent<Image>(),_sheet.Find("Border")?.GetComponent<Image>());
            PanelTheme.Button(_confirm);PanelTheme.Button(_dismiss);
        }
        // ------------------------------------------------------------- show modes

        /// <summary>Plain explanation with a single Close button.</summary>
        public void ShowInfo(string title, string body)
        {
            Reset();
            _title.text = title.ToUpperInvariant();
            _body.text = body;
            _dismiss.gameObject.SetActive(true);
            foreach(var button in _sheet.GetComponentsInChildren<Button>(true))PanelTheme.Button(button);
            Open();
        }

        /// <summary>A row of labelled buttons — used for choosing which card to claim.</summary>
        public void ShowChoices(string title, string body, IReadOnlyList<(string label, Color tint, Action act)> choices,
                                bool closable = false)
        {
            Reset();
            _title.text = title.ToUpperInvariant();
            _body.text = body;
            _dismiss.gameObject.SetActive(closable);

            var slot = Mathf.Min(420f, InnerWidth / Mathf.Max(1, choices.Count));
            for (var i = 0; i < choices.Count; i++)
            {
                var choice = choices[i];
                var x = (i - (choices.Count - 1) / 2f) * slot;

                var btn = UIFactory.Button($"Choice{i}", _buttonRow, _art != null ? _art.roundedPanel : null,
                    new Vector2(slot - 20, 104), new Vector2(x, 40));
                var img = btn.GetComponent<Image>();
                img.type = Image.Type.Sliced;
                img.color = choice.tint;

                var label = UIFactory.Text("Label", btn.transform, choice.label, 30, new Vector2(slot - 30, 90), Vector2.zero);
                label.fontStyle = FontStyles.Bold;

                var act = choice.act;
                btn.onClick.AddListener(() => { Hide(); act?.Invoke(); });
                _buttons.Add(btn.gameObject);
            }
            _middle = _buttonRow; _middleTop = 92f; _middleBottom = -12f;

            foreach(var button in _sheet.GetComponentsInChildren<Button>(true))PanelTheme.Button(button);
            Open();
        }

        /// <summary>
        /// Card picker. With pickCount 0 the cards are reference only (the rules
        /// screen); otherwise the player selects exactly that many and confirms.
        /// </summary>
        public void ShowCards(string title, string body, IReadOnlyList<Role> pool,
                              int pickCount, Action<IReadOnlyList<Role>> onConfirm)
        {
            Reset();
            _title.text = title.ToUpperInvariant();
            _body.text = body;
            _pool = pool;
            _pickCount = pickCount;
            _onConfirm = onConfirm;

            _body.fontSize = 26;
            // A list of rules reads down the left edge; a sentence stays centred.
            if (body.Contains("\n")) _body.alignment = TextAlignmentOptions.TopLeft;

            BuildCardRow();

            if (pickCount > 0) _confirm.gameObject.SetActive(true);
            else _dismiss.gameObject.SetActive(true);

            RefreshConfirm();
            foreach(var button in _sheet.GetComponentsInChildren<Button>(true))PanelTheme.Button(button);
            Open();
        }

        /// <summary>
        /// Lays out the cards with a caption beneath each one. The card prefab
        /// deliberately carries no name or ability text, so without these the
        /// exchange picker would ask the player to choose blind.
        /// </summary>
        private void BuildCardRow()
        {
            var count = _pool.Count;
            if (count == 0) return;

            var slot = Mathf.Min(250f, (InnerWidth + 20f) / count);
            var height = Mathf.Min(380f, slot * 1.42f);   // used only to place the caption
            var cardY = 58f;

            for (var i = 0; i < count; i++)
            {
                var role = _pool[i];
                var x = (i - (count - 1) / 2f) * slot;

                var card = Instantiate(_cardPrefab, _cardRow);
                card.Bind(_art);
                card.ShowRole(role);

                card.SetDisplayWidth(slot - 14f);
                var rt = (RectTransform)card.transform;
                rt.anchoredPosition = new Vector2(x, cardY);

                if (_pickCount > 0)
                {
                    var index = i;
                    card.SetInteractable(true);
                    card.Clicked += _ => TogglePick(index);
                }

                _cards.Add(card);

                var captionY = cardY - height / 2f - 20f;
                var name = UIFactory.Text($"Name{i}", _cardRow,
                    _art != null ? _art.DisplayName(role) : role.ToString(),
                    22, new Vector2(slot - 6, 30), new Vector2(x, captionY));
                name.fontStyle = FontStyles.Bold;
                name.color = UIFactory.Readable(_art != null ? _art.Accent(role) : UIFactory.Gold);
                name.enableAutoSizing = true;
                name.fontSizeMin = 12;
                name.fontSizeMax = 22;
                _captions.Add(name.gameObject);

                var ability = UIFactory.Text($"Ability{i}", _cardRow,
                    _art != null ? _art.Ability(role) : "",
                    17, new Vector2(slot - 10, 70), new Vector2(x, captionY - 44));
                ability.color = new Color(0.80f, 0.78f, 0.72f);
                UIFactory.SetFont(ability, true);
                ability.enableAutoSizing = true;
                ability.fontSizeMin = 10;
                ability.fontSizeMax = 17;
                _captions.Add(ability.gameObject);
            }
            _middle = _cardRow; _middleTop = cardY + height / 2f; _middleBottom = cardY - height / 2f - 99f;
        }

        private void TogglePick(int index)
        {
            // A pool can hold two copies of one character, so the choice is the
            // card itself. Reading the scale would count a second tap as another copy.
            var card = _cards[index];
            var resting = card.RestingScale;

            if (_picked.Contains(index))
            {
                _picked.Remove(index);
                card.SetHighlight(false);
                CoupTween.ScaleTo(card.root, resting, CoupTween.Quick);
            }
            else
            {
                if (_picked.Count >= _pickCount) return;
                _picked.Add(index);
                card.SetHighlight(true);
                CoupTween.ScaleTo(card.root, resting * 1.06f, CoupTween.Quick);
            }

            RefreshConfirm();
        }

        private void RefreshConfirm()
        {
            if (!_confirm.gameObject.activeSelf) return;
            var ready = _picked.Count == _pickCount;
            UIFactory.SetEnabled(_confirm, ready);
            _confirmLabel.text = ready ? "CONFIRM" : $"PICK {_pickCount - _picked.Count} MORE";
        }

        private void OnConfirm()
        {
            if (_picked.Count != _pickCount) return;
            var chosen = new List<Role>();
            for (var i = 0; i < _pool.Count; i++)
                if (_picked.Contains(i)) chosen.Add(_pool[i]);
            var callback = _onConfirm;
            Hide();
            callback?.Invoke(chosen);
        }

        // ------------------------------------------------------------- lifecycle

        private void Reset()
        {
            foreach (var c in _cards) if (c != null) Destroy(c.gameObject);
            foreach (var b in _buttons) if (b != null) Destroy(b);
            foreach (var c in _captions) if (c != null) Destroy(c);
            _cards.Clear();
            _buttons.Clear();
            _captions.Clear();
            _picked.Clear();

            _pickCount = 0;
            _onConfirm = null;
            _pool = Array.Empty<Role>();

            _body.fontSize = 28;
            _body.alignment = TextAlignmentOptions.Top;
            _middle = null;

            _confirm.gameObject.SetActive(false);
            _dismiss.gameObject.SetActive(false);
        }

        private void Open()
        {
            _epoch++;
            _open = true;
            _root.gameObject.SetActive(true);
            // The modal was authored at 1080x1920; on taller phones the dim must still cover the table.
            _root.anchorMin = Vector2.zero; _root.anchorMax = Vector2.one; _root.offsetMin = _root.offsetMax = Vector2.zero;
            if (_dim != null)
            {
                var dim = _dim.rectTransform;
                dim.anchorMin = Vector2.zero; dim.anchorMax = Vector2.one;
                dim.offsetMin = new Vector2(-60, -60); dim.offsetMax = new Vector2(60, 60);
            }
            // The table's font pass runs after the modal is bound, so style the title on each open.
            PanelTheme.Title(_title);
            Arrange();
            PanelAura.Attach(_sheet, _art, UIFactory.Gold, 22f);
            PanelTransition.For(_root.gameObject, _sheet).Show();
        }

        /// <summary>
        /// Sizes the sheet to what this mode shows, top to bottom: title, body, the card or
        /// button row, then the footer button. A fixed 1180 tall sheet left two buttons
        /// floating in empty space and pushed the rules text against the frame.
        /// </summary>
        private void Arrange()
        {
            const float pad = 64f, gap = 30f, footer = 110f;
            var maxHeight = Mathf.Max(900f, _root.rect.height - 160f);
            var hasBody = !string.IsNullOrEmpty(_body.text);
            var footerShown = _confirm.gameObject.activeSelf || _dismiss.gameObject.activeSelf;
            var middle = _middle != null ? _middleTop - _middleBottom : 0f;
            var fixedHeight = pad * 2 + 70f + (middle > 0 ? gap + middle : 0) + (footerShown ? gap + footer : 0);

            _body.enableAutoSizing = false;
            var bodyHeight = hasBody ? _body.GetPreferredValues(_body.text, InnerWidth, 0).y + 6f : 0f;
            var room = maxHeight - fixedHeight - (hasBody ? 18f : 0f);
            if (bodyHeight > room)
            {
                // Too long for the screen: shrink the type rather than overflow the frame.
                _body.enableAutoSizing = true; _body.fontSizeMax = _body.fontSize; _body.fontSizeMin = 18;
                bodyHeight = room;
            }
            var height = fixedHeight + (hasBody ? 18f + bodyHeight : 0f);

            _sheet.sizeDelta = new Vector2(SheetWidth, height);
            foreach (var name in new[] { "Bg", "Border" })
                if (_sheet.Find(name) is RectTransform layer) layer.sizeDelta = _sheet.sizeDelta;

            var y = height / 2f - pad;
            _title.rectTransform.sizeDelta = new Vector2(InnerWidth, 70f);
            _title.rectTransform.anchoredPosition = new Vector2(0, y - 35f);
            y -= 70f;
            _body.gameObject.SetActive(hasBody);
            if (hasBody)
            {
                y -= 18f;
                _body.rectTransform.sizeDelta = new Vector2(InnerWidth, bodyHeight);
                _body.rectTransform.anchoredPosition = new Vector2(0, y - bodyHeight / 2f);
                y -= bodyHeight;
            }
            if (_middle != null)
            {
                y -= gap;
                _middle.anchoredPosition = new Vector2(0, y - _middleTop);
                y -= middle;
            }
            var footerY = -height / 2f + pad + footer / 2f;
            foreach (var button in new[] { _confirm, _dismiss })
                ((RectTransform)button.transform).anchoredPosition = new Vector2(0, footerY);
        }

        public void Hide()
        {
            if (_root == null || !_open) return;
            _open = false;
            PanelTransition.For(_root.gameObject, _sheet).Hide();
        }
    }
}
