using Coup.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Coup.Game.UI
{
    /// <summary>The printable reference translated to live, scalable UI text.</summary>
    public sealed class RulesReferenceCard : MonoBehaviour
    {
        private RectTransform _card, _content;
        private ScrollRect _scroll;
        private TMP_Text _heading, _hint;
        private Button _actions, _rules;
        private CoupArt _art;
        private int _rows;
        private static readonly Color Ink = Hex("#17243A"), Paper = UIFactory.Parchment, Gold = UIFactory.Gold;
        private const string NoteHex = "AFB9C8";  // the printed card's italic asides

        public static void Open(Component owner, CoupArt art)
        {
            var canvas = owner.GetComponentInParent<Canvas>().rootCanvas;
            var existing = canvas.GetComponentInChildren<RulesReferenceCard>(true);
            if (existing == null)
            {
                var root = UIFactory.Rect("RulesReferenceCard", canvas.transform, Vector2.zero, Vector2.zero);
                root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
                root.offsetMin = root.offsetMax = Vector2.zero;
                existing = root.gameObject.AddComponent<RulesReferenceCard>();
                existing._art = art;
                existing.Build();
            }
            existing.gameObject.SetActive(true);
            existing.transform.SetAsLastSibling();
            existing.ShowSide(false);
            existing.Fit();
        }

        public void Close() => gameObject.SetActive(false);

        private void Update()
        {
            Fit();
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) Close();
        }

        private void Fit()
        {
            var parent = transform as RectTransform;
            if (_card == null || parent.rect.width <= 0 || Screen.width <= 0) return;
            var safe = Screen.safeArea;
            float width = parent.rect.width * safe.width / Screen.width;
            float height = parent.rect.height * safe.height / Screen.height;
            float scale = Mathf.Min(1f, (width - 48) / 980f, (height - 48) / 1450f);
            _card.localScale = Vector3.one * Mathf.Max(.1f, scale);
            _card.anchoredPosition = new Vector2(
                (safe.center.x / Screen.width - .5f) * parent.rect.width,
                (safe.center.y / Screen.height - .5f) * parent.rect.height);
        }

        private void Build()
        {
            var dim = gameObject.AddComponent<Image>(); dim.color = new Color(.01f,.02f,.04f,.88f);
            _card = UIFactory.Rect("Reference", transform, new Vector2(980,1450), Vector2.zero);
            var surface = _card.gameObject.AddComponent<Image>();
            surface.color = Ink;
            PanelTheme.Surface(surface);
            PanelAura.Attach(_card, _art, Gold, 18f);
            var header = UIFactory.Rect("Header", _card, new Vector2(860,180),new Vector2(0,615));
            _heading = Text("Heading",header,"COUP",64,new Vector2(870,110),new Vector2(0,22),true);
            _heading.color=Gold;
            var sub=Text("Subtitle",header,"VEILED COURT / QUICK REFERENCE",24,new Vector2(870,40),new Vector2(0,-49));sub.color=Paper;
            _actions = Button("Actions", "ACTIONS", new Vector2(-228,465), new Vector2(416,82),()=>ShowSide(false));
            _rules = Button("CourtRules", "COURT RULES",new Vector2(228,465),new Vector2(416,82),()=>ShowSide(true));
            var viewport=UIFactory.Rect("Viewport",_card,new Vector2(876,950),new Vector2(0,-73));
            Fill(viewport,new Color(1,1,1,.001f));viewport.gameObject.AddComponent<RectMask2D>();
            _content=UIFactory.Rect("Content",viewport,Vector2.zero,Vector2.zero);
            _content.anchorMin=new Vector2(0,1);_content.anchorMax=Vector2.one;_content.pivot=new Vector2(.5f,1);
            var layout=_content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing=22;layout.padding=new RectOffset(8,8,10,16);
            layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;
            _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            _scroll=viewport.gameObject.AddComponent<ScrollRect>();_scroll.viewport=viewport;_scroll.content=_content;
            _scroll.horizontal=false;_scroll.movementType=ScrollRect.MovementType.Clamped;_scroll.scrollSensitivity=55;
            _hint=Text("ScrollHint",_card,"",22,new Vector2(740,30),new Vector2(0,-588));_hint.color=Gold;
            Button("Close","CLOSE",new Vector2(0,-650),new Vector2(370,68),Close);
        }

        public void ShowSide(bool courtRules)
        {
            foreach(Transform child in _content) {child.gameObject.SetActive(false);Destroy(child.gameObject);}
            _heading.text=courtRules?"COURT RULES":"PLAY";
            _content.GetComponent<VerticalLayoutGroup>().spacing = courtRules ? 22 : 0;
            Select(_actions,!courtRules);Select(_rules,courtRules);
            if (!courtRules)
            {
                var instruction = Text("TurnRule", _content,
                    $"Take one action  <i><color=#{NoteHex}>(at {Rules.ForcedCoupThreshold}+ coins you must launch a Coup)</color></i>",
                    29, new Vector2(860,66), Vector2.zero);
                instruction.gameObject.AddComponent<LayoutElement>().preferredHeight = 66;
                _rows = 0;
                HeaderRow();
                GridRow(null, "Income", $"Take {Rules.IncomeAmount} coin", null);
                GridRow(null, "Foreign Aid", $"Take {Rules.ForeignAidAmount} coins", null);
                GridRow(null, "Coup", $"Pay {Rules.CoupCost} coins", null, "Choose player to lose influence");
                GridRow(Role.Duke, "Tax", $"Take {Rules.TaxAmount} coins", "Blocks Foreign Aid");
                GridRow(Role.Assassin, "Assassinate", $"Pay {Rules.AssassinateCost} coins", null, "Choose player to lose influence");
                GridRow(Role.Ambassador, "Exchange", "Exchange cards\nwith Court deck", "Blocks stealing");
                GridRow(Role.Captain, "Steal", $"Take {Rules.StealAmount} coins\nfrom another player", "Blocks stealing");
                GridRow(Role.Contessa, null, null, "Blocks assassination");
            }
            else
            {
                Row("SET UP & WIN",$"Deck: 3 of each role. Start with {Rules.StartingInfluence} hidden cards and {Rules.StartingCoins} coins. With 2 players, the first starts with 1 coin. Last player with influence wins.");
                Row("YOUR TURN","Take 1 action; play moves clockwise. Keep coins visible. You may claim any role, even if you do not hold it.");
                Row("CHALLENGE A CLAIM","Anyone may challenge a role claim or block before it resolves. Income, foreign aid and Coup cannot be challenged.");
                Row("PROVE IT OR LOSE INFLUENCE","Show the claimed role: the challenger loses 1 influence. Shuffle your shown card into the deck and draw a replacement; your claim stands. Otherwise, you lose 1 influence and your claim fails. A challenged action that fails refunds its cost.");
                Row("BLOCK AN ACTION","Anyone may claim Duke to block foreign aid. Only the target may block stealing (Captain or Ambassador) or assassination (Contessa). A successful block stops the action; assassination coins stay spent. Blocks can be bluffs and can be challenged.");
                Row("EXCHANGE & STEAL","Exchange: draw 2 cards, choose which face-down cards to keep, then return 2 to the deck. Keep the same influence count. Steal: take up to 2 coins; if the target has only 1, take 1.");
                Row("LOSING INFLUENCE","Choose a hidden card to reveal permanently; it loses its powers. Lose both and you are out. A failed challenge against an Assassin can cost 1 influence, then the assassination another.");
                Row("ONLINE PLAY","The turn timer keeps running while you read. A missed decision passes, takes Income (or a forced Coup), or chooses a legal card.");
            }
            Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            _scroll.StopMovement();_scroll.verticalNormalizedPosition=1;
            _hint.text = courtRules ? "SCROLL TO READ MORE" : "COUNTERACTION = WHAT THAT CHARACTER CAN BLOCK";
        }

        // Fixed columns make every action comparable at a glance, like the physical reference.
        private static readonly float[] Columns = { 204f, 156f, 310f, 190f };

        private void HeaderRow()
        {
            var row = NewRow("ColumnHeaders", 64);
            var titles = new[] { "Character", "Action", "Effect", "Counteraction" };
            float x = 0;
            for (int i = 0; i < Columns.Length; i++)
            {
                var label = CellText(Cell(row, x, Columns[i], new Color(.30f,.23f,.10f,.65f)), titles[i], 27);
                label.fontStyle = FontStyles.Bold; label.color = Gold;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.enableAutoSizing = true; label.fontSizeMax = 27; label.fontSizeMin = 22;
                x += Columns[i];
            }
        }

        // A null cell is "nothing here": a rule under Character, a cross everywhere else, as printed.
        // Rows grow to fit their tallest cell so the italic notes never have to shrink.
        private void GridRow(Role? role, string action, string effect, string counter, string note = null)
        {
            var row = NewRow(action ?? role.ToString(), 92);
            var band = new Color(.12f,.18f,.25f, _rows++ % 2 == 0 ? .85f : .50f);
            var accent = role.HasValue ? _art?.For(role.Value)?.accent ?? Ink : Ink;
            var values = new[] { null, action, note == null ? effect :
                $"{effect}\n<size=80%><i><color=#{NoteHex}>{note}</color></i></size>", counter };
            float x = 0, tallest = 0;
            for (int i = 0; i < Columns.Length; i++)
            {
                var cell = Cell(row, x, Columns[i], i == 0 && role.HasValue ? accent : band);
                x += Columns[i];
                if (i == 0) { CharacterCell(cell, role, accent); continue; }
                if (values[i] == null) { Mark(cell, "Mark_None", new Vector2(30, 30), Dim(.5f)); continue; }
                var label = CellText(cell, values[i], 28);
                if (i == 1)
                {
                    // Action names are single words; shrink "Assassinate" rather than break it.
                    label.textWrappingMode = TextWrappingModes.NoWrap;
                    label.enableAutoSizing = true; label.fontSizeMax = 28; label.fontSizeMin = 23;
                }
                tallest = Mathf.Max(tallest, label.GetPreferredValues(values[i], Columns[i] - 18, 0).y);
            }
            row.GetComponent<LayoutElement>().preferredHeight = Mathf.Max(92, tallest + 24);
        }

        private void CharacterCell(RectTransform cell, Role? role, Color accent)
        {
            if (!role.HasValue)
            {
                var rule = UIFactory.Rect("NoCharacter", cell, new Vector2(54, 3), Vector2.zero);
                Fill(rule, Dim(.55f)); rule.GetComponent<Image>().raycastTarget = false;
                return;
            }
            // Orange is too light for cream text, so the Ambassador takes dark ink.
            var ink = 0.2126f * accent.r + 0.7152f * accent.g + 0.0722f * accent.b > .45f ? Ink : Paper;
            Mark(cell, "Sigil_" + role.Value, new Vector2(46, 46), ink).rectTransform.anchoredPosition =
                new Vector2(-Columns[0] / 2 + 34, 0);
            var name = CellText(cell, role.Value.ToString(), 28, 62);
            name.fontStyle = FontStyles.Bold; name.color = ink;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.enableAutoSizing = true; name.fontSizeMax = 28; name.fontSizeMin = 21;
        }

        private RectTransform NewRow(string name, float height)
        {
            var row = UIFactory.Rect(name, _content, new Vector2(860, height), Vector2.zero);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return row;
        }

        // Cells hang off the row's left edge and stretch with its height, so a row can grow after it is filled.
        private static RectTransform Cell(RectTransform row, float x, float width, Color fill)
        {
            var cell = UIFactory.Rect("Cell", row, Vector2.zero, Vector2.zero);
            cell.anchorMin = Vector2.zero; cell.anchorMax = new Vector2(0, 1);
            cell.offsetMin = new Vector2(x + 1, 1); cell.offsetMax = new Vector2(x + width - 1, -1);
            Fill(cell, fill); cell.GetComponent<Image>().raycastTarget = false;
            return cell;
        }

        private TMP_Text CellText(RectTransform cell, string value, float size, float left = 8)
        {
            var label = Text("CellText", cell, value, size, Vector2.zero, Vector2.zero);
            var rt = label.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, 6); rt.offsetMax = new Vector2(-8, -6);
            return label;
        }

        private static Image Mark(RectTransform cell, string sprite, Vector2 size, Color tint)
        {
            var mark = UIFactory.Image(sprite, cell, Resources.Load<Sprite>("RulesCard/" + sprite), size, Vector2.zero);
            if (mark.sprite != null) mark.color = tint;
            return mark;
        }

        private static Color Dim(float alpha) => new Color(Paper.r, Paper.g, Paper.b, alpha);

        private void Row(string title,string body,Color? accent=null)
        {
            var row=UIFactory.Rect(title,_content,Vector2.zero,Vector2.zero);
            var text=Text("Body",row,$"<b><color=#{ColorUtility.ToHtmlStringRGB(accent.HasValue ? UIFactory.Readable(accent.Value) : Gold)}>{title}</color></b>\n{body}",32,new Vector2(860,100),Vector2.zero);
            text.alignment=TextAlignmentOptions.TopLeft;
            float h=text.GetPreferredValues(text.text,860,0).y+6;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight=h;
            text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;
            text.rectTransform.offsetMin=text.rectTransform.offsetMax=Vector2.zero;
        }

        private TMP_Text Text(string name,Transform parent,string value,float size,Vector2 area,Vector2 pos,bool display=false)
        {
            var t=UIFactory.Text(name,parent,value,size,area,pos);
            var font=display?PanelTheme.Current?.buttonFont:(_art!=null?_art.bodyFont:UIFactory.BodyFont);
            if(font!=null)t.font=font;
            t.color=Paper;t.overflowMode=TextOverflowModes.Overflow;return t;
        }
        private Button Button(string name,string title,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction action)
        {
            var b=UIFactory.Button(name,_card,null,size,pos);
            b.image.color=Ink;
            Text("Label",b.transform,title,28,size-new Vector2(24,4),Vector2.zero,true).color=Paper;
            PanelTheme.Button(b, name == "Close" ? ButtonTone.Quiet : ButtonTone.Primary);
            b.onClick.AddListener(action);return b;
        }
        private static void Select(Button b,bool selected)
        {
            PanelTheme.Button(b, selected ? ButtonTone.Primary : ButtonTone.Quiet);
        }
        private static void Fill(RectTransform rt,Color c) {rt.gameObject.AddComponent<Image>().color=c;}
        private static Color Hex(string value) {ColorUtility.TryParseHtmlString(value,out var c);return c;}
    }
}
