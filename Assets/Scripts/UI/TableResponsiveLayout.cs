using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Coup.Game.UI
{
    [DefaultExecutionOrder(1000)]
    public sealed class TableResponsiveLayout : MonoBehaviour
    {
        private RectTransform _root, _stage;
        private Rect _safe;
        private Vector2 _size;
        private int _players = -1;
        private TableView _table;
        private void Awake()
        {
            _root = (RectTransform)transform;
            _table = GetComponent<TableView>();
            _stage = transform.Find("Stage") as RectTransform;
            var canvas = GetComponentInParent<Canvas>();
            var scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
            if (scaler != null) scaler.matchWidthOrHeight = 0;
            Apply();
        }
        private void Start() => Apply();

        private void Update()
        {
            var players = _table != null && _table.match != null && _table.match.Game != null ? _table.match.Game.Players.Count : _table != null && _table.match != null ? _table.match.playerCount : 0;
            if (_root != null && (_safe != Screen.safeArea || _size != _root.rect.size || players != _players)) Apply();
        }
        private void Apply()
        {
            if (_stage == null || Screen.width <= 0 || Screen.height <= 0) return;
            _players = _table != null && _table.match != null && _table.match.Game != null ? _table.match.Game.Players.Count : _table != null && _table.match != null ? _table.match.playerCount : 0;
            _safe = Screen.safeArea;
            _size = _root.rect.size;
            _stage.anchorMin = new Vector2(_safe.xMin / Screen.width, _safe.yMin / Screen.height);
            _stage.anchorMax = new Vector2(_safe.xMax / Screen.width, _safe.yMax / Screen.height);
            _stage.offsetMin = _stage.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            var tall = Mathf.InverseLerp(1920, 2400, _stage.rect.height);
            Bottom("ActionBar", 235, 1);
            Bottom("ResponseBar", 235, 1);
            Bottom("Hud", Mathf.Lerp(580, 600, tall), .95f);
            var hud = _stage.Find("Hud") as RectTransform;
            if (hud != null) hud.anchoredPosition += new Vector2(34, 0);
            Bottom("Hand", Mathf.Lerp(845, 935, tall), Mathf.Lerp(.72f, .9f, tall));
            Bottom("StatusPlate", Mathf.Lerp(1075, 1220, tall), .94f);
            Actions();
            Responses();
            var top = _stage.Find("TopBar") as RectTransform;
            if (top != null)
            {
                top.anchorMin = new Vector2(0, 1);
                top.anchorMax = Vector2.one;
                top.sizeDelta = new Vector2(-48, 140);
                top.anchoredPosition = new Vector2(0, -85);
            }
            var centreY = Mathf.Lerp(1330, 1500, tall);
            var crowded = _players >= 4;
            var compactCentre = _stage.rect.height < 2200;
            if (compactCentre) centreY = Mathf.Min(centreY, _stage.rect.height - 660);
            var pileScale = compactCentre ? .45f : .75f;
            var deckX = _players == 2 ? 330 : compactCentre ? 60 : crowded ? 120 : Mathf.Lerp(130, 250, tall);
            Centre("DrawDeck", -deckX, centreY, pileScale);
            Centre("Discard", deckX, centreY, pileScale);
            Centre("DrawDeckSlot", -deckX, centreY, compactCentre ? .65f : 1);
            Centre("DiscardSlot", deckX, centreY, compactCentre ? .65f : 1);
            Centre("CoinPile", 0, centreY + (crowded ? 185 : 0), crowded ? .4f : Mathf.Lerp(.4f, .75f, tall));
            var coins = _stage.Find("CoinPile");
            if (coins != null) coins.gameObject.SetActive(!compactCentre);
            var captionOffset = compactCentre ? 100 : 145;
            var captionScale = compactCentre ? .6f : .85f;
            Centre("DeckCountPlate", -deckX, centreY - captionOffset, captionScale);
            Centre("DiscardCountPlate", deckX, centreY - captionOffset, captionScale);
            foreach (var name in new[] { "DeckCountPlate", "DiscardCountPlate" })
            {
                var caption = _stage.Find(name) as RectTransform;
                if (caption == null) continue;
                caption.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, compactCentre ? 180 : 230);
                foreach (var text in caption.GetComponentsInChildren<TMP_Text>(true))
                    text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, compactCentre ? 180 : 230);
            }
        }
        private void Bottom(string name, float y, float scale)
        {
            var rect = _stage.Find(name) as RectTransform;
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
            rect.anchoredPosition = new Vector2(0, y);
            rect.localScale = Vector3.one * scale;
        }
        private void Centre(string name, float x, float y, float scale)
        {
            var rect = _stage.Find(name) as RectTransform;
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
            rect.anchoredPosition = new Vector2(x, y);
            rect.localScale = Vector3.one * scale;
        }
        private void Actions()
        {
            var bar = _stage.Find("ActionBar") as RectTransform;
            if (bar == null) return;
            bar.sizeDelta = new Vector2(1032, 410);
            foreach (var name in new[] { "DockBackground", "DockBorder" })
            {
                var dock = bar.Find(name) as RectTransform;
                if (dock == null) continue;
                dock.anchorMin = Vector2.zero; dock.anchorMax = Vector2.one;
                dock.pivot = new Vector2(.5f, .5f);
                dock.offsetMin = new Vector2(0, -10); dock.offsetMax = new Vector2(0, 10);
                dock.localScale = Vector3.one;
            }
            var heading = bar.Find("ConsoleTitle")?.GetComponent<TMP_Text>();
            if (heading != null) heading.gameObject.SetActive(false);
            var buttons = bar.GetComponentsInChildren<ActionButtonView>(true);
            for (var i = 0; i < buttons.Length; i++)
            {
                var b = buttons[i];
                var rect = (RectTransform)b.transform;
                var row = i / 4;
                var cols = Mathf.Min(4, buttons.Length - row * 4);
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(238, 180);
                rect.anchoredPosition = new Vector2((i % 4 - (cols - 1) * .5f) * 252, row == 0 ? 98 : -98);
                rect.localScale = Vector3.one;
                if (b.icon != null) SetRect(b.icon.rectTransform, new Vector2(78, 78), new Vector2(0, 39));
                if (b.label != null)
                {
                    SetRect(b.label.rectTransform, new Vector2(226, 43), new Vector2(0, -24));
                    b.label.enableAutoSizing = true; b.label.fontSizeMin = 23; b.label.fontSizeMax = 31;
                }
                if (b.cost != null)
                {
                    SetRect(b.cost.rectTransform, new Vector2(226, 40), new Vector2(0, -64));
                    b.cost.enableAutoSizing = true; b.cost.fontSizeMin = 22; b.cost.fontSizeMax = 27;
                }
            }
        }
        private void Responses()
        {
            var bar = _stage.Find("ResponseBar");
            if (bar == null) return;
            foreach (var b in bar.GetComponentsInChildren<Button>(true))
            {
                var rect = (RectTransform)b.transform;
                rect.sizeDelta = new Vector2(320, 190);
                var fill = b.GetComponent<Image>();
                if (fill != null) fill.preserveAspect = false;
                var border = b.transform.Find("Border") as RectTransform;
                if (border != null)
                {
                    border.anchorMin = Vector2.zero; border.anchorMax = Vector2.one;
                    border.offsetMin = border.offsetMax = Vector2.zero;
                    border.localScale = Vector3.one;
                }
                foreach (var text in b.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.enableAutoSizing = true;
                    text.fontSizeMin = text.name == "Label" ? 32 : 23;
                    text.fontSizeMax = text.name == "Label" ? 39 : 28;
                }
            }
        }
        private static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }
    }
}
