using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly List<Role> _picked = new();

        private int _pickCount;
        private Action<IReadOnlyList<Role>> _onConfirm;
        private IReadOnlyList<Role> _pool;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;

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

            _root.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------- show modes

        /// <summary>Plain explanation with a single Close button.</summary>
        public void ShowInfo(string title, string body)
        {
            Reset();
            _title.text = title;
            _body.text = body;
            _body.rectTransform.sizeDelta = new Vector2(860, 700);
            _body.rectTransform.anchoredPosition = new Vector2(0, 120);
            _dismiss.gameObject.SetActive(true);
            Open();
        }

        /// <summary>A row of labelled buttons — used for choosing which card to claim.</summary>
        public void ShowChoices(string title, string body, IReadOnlyList<(string label, Color tint, Action act)> choices)
        {
            Reset();
            _title.text = title;
            _body.text = body;

            var slot = Mathf.Min(420f, 900f / Mathf.Max(1, choices.Count));
            for (var i = 0; i < choices.Count; i++)
            {
                var choice = choices[i];
                var x = (i - (choices.Count - 1) / 2f) * slot;

                var btn = UIFactory.Button($"Choice{i}", _buttonRow, _art != null ? _art.roundedPanel : null,
                    new Vector2(slot - 20, 130), new Vector2(x, 40));
                var img = btn.GetComponent<Image>();
                img.type = Image.Type.Sliced;
                img.color = choice.tint;

                var label = UIFactory.Text("Label", btn.transform, choice.label, 30, new Vector2(slot - 30, 110), Vector2.zero);
                label.fontStyle = FontStyles.Bold;

                var act = choice.act;
                btn.onClick.AddListener(() => { Hide(); act?.Invoke(); });
                _buttons.Add(btn.gameObject);
            }

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
            _title.text = title;
            _body.text = body;
            _pool = pool;
            _pickCount = pickCount;
            _onConfirm = onConfirm;

            _body.fontSize = 24;
            _body.rectTransform.sizeDelta = new Vector2(880, 320);
            _body.rectTransform.anchoredPosition = new Vector2(0, 300);
            _body.alignment = TextAlignmentOptions.TopLeft;

            BuildCardRow();

            if (pickCount > 0) _confirm.gameObject.SetActive(true);
            else _dismiss.gameObject.SetActive(true);

            RefreshConfirm();
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

            var slot = Mathf.Min(250f, 930f / count);
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
                ability.enableAutoSizing = true;
                ability.fontSizeMin = 10;
                ability.fontSizeMax = 17;
                _captions.Add(ability.gameObject);
            }
        }

        private void TogglePick(int index)
        {
            // Track by index: a pool can hold two copies of the same role.
            var role = _pool[index];
            var card = _cards[index];

            var resting = card.RestingScale;
            var selected = card.transform.localScale.y > resting.y * 1.01f;

            if (selected)
            {
                _picked.Remove(role);
                CoupTween.ScaleTo((RectTransform)card.transform, resting, CoupTween.Quick);
                card.SetHighlight(false);
            }
            else
            {
                if (_picked.Count >= _pickCount) return;
                _picked.Add(role);
                CoupTween.ScaleTo((RectTransform)card.transform, resting * 1.06f, CoupTween.Quick);
                card.SetHighlight(true);
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
            var chosen = _picked.ToList();
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
            _body.rectTransform.sizeDelta = new Vector2(860, 240);
            _body.rectTransform.anchoredPosition = new Vector2(0, 340);

            _confirm.gameObject.SetActive(false);
            _dismiss.gameObject.SetActive(false);
        }

        private void Open()
        {
            _root.gameObject.SetActive(true);
            CoupTween.PopIn(_sheet, _group, CoupTween.Normal);
        }

        public void Hide()
        {
            if (_root == null || !_root.gameObject.activeSelf) return;
            CoupTween.Fade(_group, 0f, CoupTween.Quick, () => _root.gameObject.SetActive(false));
        }
    }
}
