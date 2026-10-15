using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Coup.Core;

namespace Coup.Game.UI
{
    /// <summary>
    /// The portrait table. Assembles the HUD from the generated prefabs and keeps it
    /// in step with the rules engine, which it only ever reads — every mutation goes
    /// through MatchController.
    /// </summary>
    public class TableView : MonoBehaviour
    {
        // Layout bands for the 1080x1920 canvas, measured from the centre.
        private const float TopBarY = 870f;
        private const float CentreY = 200f;
        private const float StageScale = 0.75f;
        private const float StatusY = -40f;
        private const float HandY = -350f;
        private const float HudY = -630f;
        private const float BarY = -800f;

        [Header("Data")]
        public CoupArt art;
        public MatchController match;

        [Header("Prefabs")]
        public CardView cardPrefab;
        public PlayerPanelView playerPanelPrefab;
        public ActionButtonView actionButtonPrefab;

        private static readonly ActionKind[] ActionOrder =
        {
            ActionKind.Income, ActionKind.ForeignAid, ActionKind.Tax, ActionKind.Steal,
            ActionKind.Assassinate, ActionKind.Exchange, ActionKind.Coup
        };

        private RectTransform _root;
        private RectTransform _stage;
        private RectTransform _hudRect;
        private readonly List<PlayerPanelView> _panels = new();
        private readonly Dictionary<ActionKind, ActionButtonView> _actions = new();
        private readonly List<CardView> _hand = new();
        private readonly Dictionary<int, Role> _lastClaim = new();
        private readonly Dictionary<int, string> _lastDeed = new();
        private readonly List<string> _log = new();

        private RectTransform _handRoot;
        private RectTransform _actionBar;
        private RectTransform _responseBar;
        private TextMeshProUGUI _statusTitle;
        private TextMeshProUGUI _status;
        private TextMeshProUGUI _logText;
        private TextMeshProUGUI _turnText;
        private TextMeshProUGUI _hudName;
        private TextMeshProUGUI _hudCoins;
        private RectTransform _deckRect;
        private RectTransform _discardRect;
        private RectTransform _coinPileRect;
        private RectTransform _deckCaption;
        private RectTransform _discardCaption;
        private RectTransform _hudCoinIcon;
        private bool _openingPlayed;
        private TextMeshProUGUI _deckCount;
        private TextMeshProUGUI _discardCount;
        private Button _blockButton;
        private Button _challengeButton;
        private Button _passButton;
        private ModalPanel _modal;
        private UiVfx _vfx;
        private CardView _revealCard;

        private ActionKind? _awaitingTarget;

        private void Awake()
        {
            _root = (RectTransform)transform;
            if (match == null) match = FindAnyObjectByType<MatchController>();
            UIFactory.UseFonts(art != null ? art.displayFont : null, art != null ? art.bodyFont : null);
            // A reimported sprite asset keeps a stale name lookup, and the coin
            // tag then renders against a character with no asset.
            if (TMP_Settings.defaultSpriteAsset != null)
                TMP_Settings.defaultSpriteAsset.UpdateLookupTables();

            // The table lives in the scene. Build() only runs if that hierarchy
            // has not been authored yet.
            if (transform.Find("Stage") == null)
                Build();
            else
                BindScene();

            UIFactory.ApplyFonts(transform);

            if (match != null)
            {
                match.StateChanged -= Refresh;
                match.EventOccurred -= OnEvent;
                match.StateChanged += Refresh;
                match.EventOccurred += OnEvent;
            }
        }

        private void OnDestroy()
        {
            if (match == null) return;
            match.StateChanged -= Refresh;
            match.EventOccurred -= OnEvent;
        }

        /// <summary>
        /// Hooks the scene hierarchy up to the match. Positions and sizes stay
        /// where they were placed in the scene.
        /// </summary>
        private void BindScene()
        {
            _panels.Clear();
            _actions.Clear();
            _stage = (RectTransform)transform.Find("Stage");

            _turnText = FindText("Stage/TopBar/Turn");
            _logText = FindText("Stage/TopBar/Log");
            EnlargeHeaderLog();
            _deckRect = FindRect("Stage/DrawDeck");
            _discardRect = FindRect("Stage/Discard");
            _coinPileRect = FindRect("Stage/CoinPile");
            _deckCaption = FindRect("Stage/DeckCountPlate");
            _discardCaption = FindRect("Stage/DiscardCountPlate");
            _deckCount = FindText("Stage/DeckCountPlate/DeckCount");
            _discardCount = FindText("Stage/DiscardCountPlate/DiscardCount");
            _statusTitle = FindText("Stage/StatusPlate/StatusTitle");
            _status = FindText("Stage/StatusPlate/Status");
            _handRoot = FindRect("Stage/Hand");
            _hudRect = FindRect("Stage/Hud");
            _hudName = FindText("Stage/Hud/Name");
            _hudCoinIcon = FindRect("Stage/Hud/CoinIcon");
            _hudCoins = FindText("Stage/Hud/Coins");
            DressHudCoins();
            _actionBar = FindRect("Stage/ActionBar");
            _responseBar = FindRect("Stage/ResponseBar");

            Wire(FindButton("Stage/TopBar/Rules"), ShowRules);
            Wire(FindButton("Stage/TopBar/Settings"), ShowSettings);

            var seats = Enumerable.Range(0, match != null ? match.playerCount : 0)
                .Where(i => match == null || i != match.humanSeat).ToList();
            var panels = _stage.GetComponentsInChildren<PlayerPanelView>(true);
            for (var i = 0; i < panels.Length && i < seats.Count; i++)
            {
                var panel = panels[i];
                panel.ClearHooks();
                panel.Initialise(seats[i], art, cardPrefab);
                panel.SetSeatScale(panel.transform.localScale.x);
                panel.Clicked += OnOpponentClicked;
                _panels.Add(panel);
            }

            var buttons = _actionBar != null
                ? _actionBar.GetComponentsInChildren<ActionButtonView>(true)
                : System.Array.Empty<ActionButtonView>();
            for (var i = 0; i < buttons.Length && i < ActionOrder.Length; i++)
            {
                var kind = ActionOrder[i];
                buttons[i].ClearHooks();
                buttons[i].Initialise(kind, art);
                buttons[i].SetEffect(Effect(kind));
                buttons[i].Clicked += OnActionClicked;
                buttons[i].HeldForInfo += ShowActionInfo;
                _actions[kind] = buttons[i];
            }

            _blockButton = FindButton("Stage/ResponseBar/Block");
            _challengeButton = FindButton("Stage/ResponseBar/Challenge");
            _passButton = FindButton("Stage/ResponseBar/Pass");
            Wire(_blockButton, OnBlockPressed);
            Wire(_challengeButton, () => match.HumanChallenge());
            Wire(_passButton, () => match.HumanPass());

            _modal = GetComponent<ModalPanel>();
            if (_modal == null) _modal = gameObject.AddComponent<ModalPanel>();
            if (!_modal.TryBind(art, cardPrefab))
                _modal.Build(_root, art, cardPrefab);

            _vfx = GetComponent<UiVfx>();
            if (_vfx == null) _vfx = gameObject.AddComponent<UiVfx>();
            _vfx.Build(_root, art);
        }

        private static void Wire(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private TextMeshProUGUI FindText(string path) =>
            transform.Find(path)?.GetComponent<TextMeshProUGUI>();

        private RectTransform FindRect(string path) =>
            transform.Find(path) as RectTransform;

        private Button FindButton(string path) =>
            transform.Find(path)?.GetComponent<Button>();

        private void Start() => Refresh();

        // ------------------------------------------------------------------ build

        private void Build()
        {
            UIFactory.FullScreen("Background", _root, art != null ? art.tableBackground : null);

            // A vignette keeps the busy table art from fighting the HUD text.
            var vignette = UIFactory.FullScreen("Vignette", _root, null);
            vignette.sprite = null;
            vignette.color = new Color(0.01f, 0.02f, 0.05f, 0.55f);

            // The room stays full-bleed. Each HUD piece is a quarter smaller
            // than it was drawn, and keeps the anchor it was laid out at.
            _stage = UIFactory.Rect("Stage", _root, new Vector2(1080, 1920), Vector2.zero);

            BuildTopBar();
            BuildOpponents();
            BuildCentre();
            BuildStatus();
            BuildHand();
            BuildHud();
            BuildActionBar();
            BuildResponseBar();

            _modal = gameObject.AddComponent<ModalPanel>();
            _modal.Build(_root, art, cardPrefab);

            // Built last so the effects layer composites above every other element.
            _vfx = gameObject.AddComponent<UiVfx>();
            _vfx.Build(_root, art);
        }

        private void BuildTopBar()
        {
            var bar = Panel("TopBar", _stage, new Vector2(1000, 150), new Vector2(0, TopBarY), 0.8f);

            _turnText = UIFactory.Text("Turn", bar, "", 30, new Vector2(400, 44), new Vector2(-270, 34));
            _turnText.alignment = TextAlignmentOptions.Left;
            _turnText.color = UIFactory.Gold;
            _turnText.fontStyle = FontStyles.Bold;

            _logText = UIFactory.Text("Log", bar, "", 38, new Vector2(640, 136), new Vector2(-160, 0));
            _logText.alignment = TextAlignmentOptions.Left;
            _logText.color = new Color(0.78f, 0.76f, 0.70f);
            EnlargeHeaderLog();

            var rules = TextButton("Rules", bar, "RULES", new Vector2(150, 76), new Vector2(300, 0));
            rules.onClick.AddListener(ShowRules);

            var settings = UIFactory.Button("Settings", bar, art?.settingsButton, new Vector2(76, 76), new Vector2(420, 0));
            settings.onClick.AddListener(ShowSettings);
        }

        private void BuildOpponents()
        {
            if (match == null || playerPanelPrefab == null) return;

            var seats = Enumerable.Range(0, match.playerCount).Where(i => i != match.humanSeat).ToList();
            var spots = SeatSpots(seats.Count);

            for (var i = 0; i < seats.Count; i++)
            {
                var panel = Instantiate(playerPanelPrefab, _stage);
                var spot = i < spots.Length ? spots[i] : spots[spots.Length - 1];
                var rt = (RectTransform)panel.transform;
                rt.anchoredPosition = spot.Position;
                panel.SetSeatScale(spot.Scale * StageScale);

                panel.Initialise(seats[i], art, cardPrefab);
                panel.Clicked += OnOpponentClicked;
                _panels.Add(panel);
            }
        }

        /// <summary>
        /// Seats around the coin pile rather than in one row, so four players have
        /// room and six still fit on the portrait table. You always sit at the bottom.
        /// </summary>
        private static SeatSpot[] SeatSpots(int opponents)
        {
            switch (opponents)
            {
                case 1: return new[] { new SeatSpot(0f, 600f, 1f) };
                case 2: return new[] { new SeatSpot(-300f, 560f, 0.92f), new SeatSpot(300f, 560f, 0.92f) };
                case 3:
                    return new[]
                    {
                        new SeatSpot(-400f, 500f, 0.82f),
                        new SeatSpot(0f, 630f, 0.88f),
                        new SeatSpot(400f, 500f, 0.82f)
                    };
                case 4:
                    return new[]
                    {
                        new SeatSpot(-420f, 380f, 0.68f),
                        new SeatSpot(-175f, 640f, 0.7f),
                        new SeatSpot(175f, 640f, 0.7f),
                        new SeatSpot(420f, 380f, 0.68f)
                    };
                default:
                    return new[]
                    {
                        new SeatSpot(-430f, 340f, 0.58f),
                        new SeatSpot(-230f, 580f, 0.6f),
                        new SeatSpot(0f, 690f, 0.6f),
                        new SeatSpot(230f, 580f, 0.6f),
                        new SeatSpot(430f, 340f, 0.58f)
                    };
            }
        }

        private readonly struct SeatSpot
        {
            public readonly Vector2 Position;
            public readonly float Scale;

            public SeatSpot(float x, float y, float scale)
            {
                Position = new Vector2(x, y);
                Scale = scale;
            }
        }

        private void BuildCentre()
        {
            var deck = UIFactory.Image("DrawDeck", _stage, art?.drawDeck, new Vector2(190, 230), new Vector2(-290, 175));
            _deckRect = deck.rectTransform;
            _deckCount = Caption("DeckCount", new Vector2(-290, 24));
            _deckCaption = (RectTransform)_deckCount.transform.parent;

            var pile = UIFactory.Image("CoinPile", _stage, art?.coinPile, new Vector2(360, 360), new Vector2(0, 170));
            _coinPileRect = pile.rectTransform;

            var discard = UIFactory.Image("Discard", _stage, art?.discardDeck, new Vector2(190, 230), new Vector2(290, 175));
            _discardRect = discard.rectTransform;
            _discardCount = Caption("DiscardCount", new Vector2(290, 24));
            _discardCaption = (RectTransform)_discardCount.transform.parent;

            LayoutCentre(match != null ? Mathf.Max(0, match.playerCount - 1) : 3);
            Shrink(_deckRect);
            Shrink(_discardRect);
            Shrink(_coinPileRect);
            Shrink(_deckCaption);
            Shrink(_discardCaption);
        }

        /// <summary>Pulls the deck and the pile in when five or six players ring the table.</summary>
        private void LayoutCentre(int opponents)
        {
            var crowded = opponents >= 4;
            var deckSize = crowded ? new Vector2(150f, 180f) : new Vector2(190f, 220f);
            var deckX = crowded ? 220f : 290f;
            var deckY = crowded ? 175f : 220f;
            var coin = crowded ? 300f : 360f;

            Place(_deckRect, deckSize, new Vector2(-deckX, deckY));
            Place(_discardRect, deckSize, new Vector2(deckX, deckY));
            Place(_coinPileRect, new Vector2(coin, coin), new Vector2(0f, deckY - 36f));

            // Keep the labels above the status banner.
            var captionY = Mathf.Max(78f, deckY - deckSize.y * 0.5f - 32f);
            Place(_deckCaption, new Vector2(180f, 46f), new Vector2(-deckX, captionY));
            Place(_discardCaption, new Vector2(180f, 46f), new Vector2(deckX, captionY));
        }

        private static void Place(RectTransform rect, Vector2 size, Vector2 pos)
        {
            if (rect == null) return;
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
        }

        /// <summary>Shrinks a piece in place. Its anchored position stays on the table.</summary>
        private static void Shrink(RectTransform rect)
        {
            if (rect != null) rect.localScale = Vector3.one * StageScale;
        }

        private TextMeshProUGUI Caption(string name, Vector2 pos)
        {
            var plate = Panel($"{name}Plate", _stage, new Vector2(200, 50), pos, 0.75f);
            var text = UIFactory.Text(name, plate, "", 24, new Vector2(190, 44), Vector2.zero);
            text.color = UIFactory.Gold;
            text.fontStyle = FontStyles.Bold;
            return text;
        }

        private void BuildStatus()
        {
            var plate = Panel("StatusPlate", _stage, new Vector2(1000, 140), new Vector2(0, StatusY), 0.92f);
            _statusTitle = UIFactory.Text("StatusTitle", plate, "", 30, new Vector2(940, 40), new Vector2(0, 32));
            _statusTitle.fontStyle = FontStyles.Bold;
            _statusTitle.color = UIFactory.Gold;
            _status = UIFactory.Text("Status", plate, "", 24, new Vector2(940, 72), new Vector2(0, -26));
            _status.color = new Color(0.93f, 0.90f, 0.82f);
        }

        private void BuildHand()
        {
            _handRoot = UIFactory.Rect("Hand", _stage, new Vector2(1000, 460), new Vector2(0, HandY));
            Shrink(_handRoot);
        }

        private void BuildHud()
        {
            var plate = Panel("Hud", _stage, new Vector2(1000, 130), new Vector2(0, HudY), 0.9f);
            _hudRect = plate;

            _hudName = UIFactory.Text("Name", plate, "You", 34, new Vector2(400, 54), new Vector2(-270, 0));
            _hudName.alignment = TextAlignmentOptions.Left;
            _hudName.fontStyle = FontStyles.Bold;

            var coinIcon = UIFactory.Image("CoinIcon", plate, art?.coin, new Vector2(54, 54), new Vector2(250, 0));
            _hudCoinIcon = coinIcon.rectTransform;
            _hudCoins = UIFactory.Text("Coins", plate, "0", 40, new Vector2(180, 56), new Vector2(340, 0));
            _hudCoins.alignment = TextAlignmentOptions.Left;
            _hudCoins.color = UIFactory.Gold;
            _hudCoins.fontStyle = FontStyles.Bold;
            DressHudCoins();
        }

        /// <summary>The count on the You bar carries the gold coin inside the text.</summary>
        private void DressHudCoins()
        {
            if (_hudCoinIcon != null)
            {
                var image = _hudCoinIcon.GetComponent<Image>();
                if (image != null) image.enabled = false;
            }

            if (_hudCoins == null) return;
            _hudCoins.richText = true;
            _hudCoins.alignment = TextAlignmentOptions.Left;
            if (_hudCoins.spriteAsset == null)
                _hudCoins.spriteAsset = TMP_Settings.defaultSpriteAsset;
        }

        private void BuildActionBar()
        {
            _actionBar = UIFactory.Rect("ActionBar", _stage, new Vector2(1060, 190), new Vector2(0, BarY));
            Shrink(_actionBar);
            if (actionButtonPrefab == null) return;

            var slot = 1050f / ActionOrder.Length;
            for (var i = 0; i < ActionOrder.Length; i++)
            {
                var kind = ActionOrder[i];
                var button = Instantiate(actionButtonPrefab, _actionBar);

                var rt = (RectTransform)button.transform;
                rt.sizeDelta = new Vector2(slot - 8f, 176f);
                rt.anchoredPosition = new Vector2((i - (ActionOrder.Length - 1) / 2f) * slot, 0f);

                button.Initialise(kind, art);
                button.SetEffect(Effect(kind));
                button.Clicked += OnActionClicked;
                button.HeldForInfo += ShowActionInfo;
                _actions[kind] = button;
            }
        }

        private void BuildResponseBar()
        {
            _responseBar = UIFactory.Rect("ResponseBar", _stage, new Vector2(1060, 160), new Vector2(0, BarY));
            Shrink(_responseBar);

            _blockButton = TextButton("Block", _responseBar, "STOP", new Vector2(320, 130), new Vector2(-350, 0), "Stop this action");
            _blockButton.GetComponent<Image>().color = new Color(0.14f, 0.28f, 0.45f);
            _blockButton.onClick.AddListener(OnBlockPressed);

            _challengeButton = TextButton("Challenge", _responseBar, "CALL BLUFF", new Vector2(320, 130), new Vector2(0, 0), "If wrong, you lose a card");
            _challengeButton.GetComponent<Image>().color = new Color(0.47f, 0.13f, 0.16f);
            _challengeButton.onClick.AddListener(() => match.HumanChallenge());

            _passButton = TextButton("Pass", _responseBar, "LET IT", new Vector2(320, 130), new Vector2(350, 0), "Don't call it");
            _passButton.GetComponent<Image>().color = new Color(0.16f, 0.19f, 0.28f);
            _passButton.onClick.AddListener(() => match.HumanPass());

            // The action bar occupies this spot until a response is needed.
            _responseBar.gameObject.SetActive(false);
        }

        /// <summary>A tinted rounded plate — the base for every panel in the HUD.</summary>
        private RectTransform Panel(string name, Transform parent, Vector2 size, Vector2 pos, float opacity)
        {
            var root = UIFactory.Rect(name, parent, size, pos);
            Shrink(root);

            var bg = UIFactory.Image("Bg", root, art?.roundedPanel, size, Vector2.zero, false);
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.047f, 0.071f, 0.129f, opacity);

            var border = UIFactory.Image("Border", root, art?.roundedBorder, size, Vector2.zero, false);
            border.type = Image.Type.Sliced;
            border.color = new Color(0.84f, 0.69f, 0.33f, 0.5f);

            return root;
        }

        private Button TextButton(string name, Transform parent, string label, Vector2 size, Vector2 pos, string caption = null)
        {
            var btn = UIFactory.Button(name, parent, art?.roundedPanel, size, pos);
            var img = btn.GetComponent<Image>();
            img.type = Image.Type.Sliced;
            img.color = new Color(0.10f, 0.13f, 0.20f);

            var border = UIFactory.Image("Border", btn.transform, art?.roundedBorder, size, Vector2.zero, false);
            border.type = Image.Type.Sliced;
            border.color = new Color(0.84f, 0.69f, 0.33f, 0.7f);

            var hasCaption = !string.IsNullOrEmpty(caption);
            var text = UIFactory.Text("Label", btn.transform, label, hasCaption ? 26 : 30,
                hasCaption ? new Vector2(size.x - 24f, 48f) : size,
                hasCaption ? new Vector2(0f, 18f) : Vector2.zero);
            text.fontStyle = FontStyles.Bold;

            if (hasCaption)
            {
                var line = UIFactory.Text("Caption", btn.transform, caption, 18,
                    new Vector2(size.x - 24f, 36f), new Vector2(0f, -28f));
                line.color = new Color(0.92f, 0.88f, 0.78f, 0.92f);
            }

            return btn;
        }

        // ---------------------------------------------------------------- refresh

        private void Refresh()
        {
            var game = match?.Game;
            if (game == null) return;

            EnsureSeats(game);
            RefreshOpponents(game);
            RefreshCentre(game);
            RefreshHud(game);
            RefreshHand(game);
            RefreshBars(game);
            RefreshStatus(game);
            RefreshModal(game);
            PlayOpening();
        }

        private void EnsureSeats(CoupGame game)
        {
            if (match == null || _panels.Count == 0) return;
            var seats = Enumerable.Range(0, game.Players.Count).Where(i => i != match.humanSeat).ToList();
            for (var i = 0; i < _panels.Count; i++)
            {
                var panel = _panels[i];
                var show = i < seats.Count;
                if (panel.gameObject.activeSelf != show) panel.gameObject.SetActive(show);
                if (!show || panel.Seat == seats[i]) continue;
                panel.ClearHooks();
                panel.Initialise(seats[i], art, cardPrefab);
                panel.Clicked += OnOpponentClicked;
            }
        }

        private void RefreshOpponents(CoupGame game)
        {
            var acting = game.Phase == GamePhase.AwaitingAction
                ? game.CurrentPlayerIndex
                : game.ActingPlayerIndex;

            foreach (var panel in _panels)
            {
                var player = game.Players[panel.Seat];
                var targetable = _awaitingTarget.HasValue &&
                                 game.CanDeclare(match.humanSeat, _awaitingTarget.Value, panel.Seat);

                _lastClaim.TryGetValue(panel.Seat, out var claim);
                var claimValue = _lastClaim.ContainsKey(panel.Seat) ? claim : (Role?)null;
                _lastDeed.TryGetValue(panel.Seat, out var deed);

                panel.Refresh(player, panel.Seat == acting, targetable, claimValue, deed);
            }
        }

        private void RefreshCentre(CoupGame game)
        {
            _deckCount.text = $"DECK {game.DeckCount}";
            _discardCount.text = $"OUT {game.Players.Sum(p => p.Revealed.Count)}";
        }

        private void RefreshHud(CoupGame game)
        {
            var me = game.Players[match.humanSeat];
            _hudName.text = me.Name;

            var coins = $"<sprite name=\"coin\" tint=0> {me.Coins}";
            if (_hudCoins.text != coins)
            {
                _hudCoins.text = coins;
                CoupTween.Punch(_hudCoins.transform, 0.28f, CoupTween.Quick);
            }
        }

        private void RefreshHand(CoupGame game)
        {
            var me = game.Players[match.humanSeat];
            // The exchange picker is this hand plus the two drawn cards. Leaving the
            // hand on screen would show those same cards a second time.
            if (game.Phase == GamePhase.AwaitingExchangeSelection &&
                game.ActingPlayerIndex == match.humanSeat)
            {
                foreach (var held in _hand)
                    if (held != null) held.gameObject.SetActive(false);
                return;
            }

            var choosingLoss = game.Phase == GamePhase.AwaitingInfluenceLoss &&
                               game.PendingLoserIndex == match.humanSeat;

            var influence = me.Influence.ToList();
            var revealed = me.Revealed.ToList();
            var total = influence.Count + revealed.Count;

            while (_hand.Count < total)
            {
                var card = Instantiate(cardPrefab, _handRoot);
                card.Bind(art);
                card.Clicked += OnHandCardClicked;
                _hand.Add(card);
            }

            var slot = 330f;
            for (var i = 0; i < _hand.Count; i++)
            {
                var card = _hand[i];
                var used = i < total;
                card.gameObject.SetActive(used);
                if (!used) continue;

                if (card.IsAnimating) continue;

                // Position only — the card's own size and inner layout stay as authored.
                var rt = (RectTransform)card.transform;
                rt.anchoredPosition = new Vector2((i - (total - 1) / 2f) * slot, 0);

                // The opening deal reveals the hand itself. Showing the face here
                // would flip the card before it has left the deck.
                if (!_openingPlayed)
                {
                    card.ShowBack();
                    card.SetDimmed(false);
                    card.SetInteractable(false);
                    card.SetHighlight(false);
                    continue;
                }

                if (i < influence.Count)
                {
                    var role = influence[i];
                    if (card.Role != role || card.IsFaceDown) card.ShowRole(role, animate: true);
                    card.SetDimmed(false);
                    card.SetInteractable(choosingLoss);
                    card.SetHighlight(choosingLoss);
                }
                else
                {
                    var role = revealed[i - influence.Count];
                    if (card.Role != role) card.ShowRole(role, animate: true);
                    card.SetDimmed(true);
                    card.SetInteractable(false);
                }
            }
        }

        private void RefreshBars(CoupGame game)
        {
            var myTurn = game.Phase == GamePhase.AwaitingAction &&
                         game.CurrentPlayerIndex == match.humanSeat;

            var amResponder = game.PendingResponders.Contains(match.humanSeat);
            var canChallenge = amResponder &&
                game.Phase is GamePhase.AwaitingChallenge or GamePhase.AwaitingBlockChallenge;
            // A steal's victim can block as soon as the claim is announced, not only
            // after every other player has passed on challenging it.
            var canBlock = amResponder && game.MayBlock(match.humanSeat) &&
                game.Phase is GamePhase.AwaitingChallenge or GamePhase.AwaitingBlock;

            _actionBar.gameObject.SetActive(myTurn || _awaitingTarget.HasValue);
            _responseBar.gameObject.SetActive(canChallenge || canBlock);

            var letting = game.Phase == GamePhase.AwaitingBlock;
            SetResponse(_passButton, "LET IT", letting ? "Let it happen" : "Don't call it");
            SetResponse(_blockButton, "STOP", BlockCaption(game));
            SetResponse(_challengeButton, "CALL BLUFF", "If wrong, you lose a card");
            ShowResponse(_blockButton, canBlock);
            ShowResponse(_challengeButton, canChallenge);
            ShowResponse(_passButton, canChallenge || canBlock);
            LayoutResponses();

            foreach (var pair in _actions)
            {
                var kind = pair.Key;
                var legal = myTurn && (Rules.RequiresTarget(kind)
                    ? game.Players.Any(p => game.CanDeclare(match.humanSeat, kind, p.Index))
                    : game.CanDeclare(match.humanSeat, kind));
                pair.Value.SetAvailable(legal);
                pair.Value.SetChosen(_awaitingTarget == kind);
            }
        }

        private void RefreshStatus(CoupGame game)
        {
            _turnText.text = game.Phase == GamePhase.GameOver ? "GAME OVER" : $"TURN {game.TurnNumber}";
            var prompt = DescribePhase(game);
            if (_statusTitle != null) _statusTitle.text = prompt.Title;
            _status.text = prompt.Body;
        }

        private Prompt DescribePhase(CoupGame game)
        {
            if (game.Phase == GamePhase.GameOver)
            {
                if (game.Winner == null) return new Prompt("Game over", "");
                return game.Winner.Index == match.humanSeat
                    ? new Prompt("You win", "Everyone else is out.")
                    : new Prompt($"{game.Winner.Name} wins", "");
            }

            if (_awaitingTarget.HasValue)
                return TargetingPrompt(_awaitingTarget.Value);

            var me = match.humanSeat;
            switch (game.Phase)
            {
                case GamePhase.AwaitingAction:
                    if (game.CurrentPlayerIndex != me)
                        return new Prompt($"{Name(game, game.CurrentPlayerIndex)}'s turn", "They are choosing what to do.");
                    return game.Players[me].MustCoup
                        ? new Prompt("You must Coup", "You have 10 coins. Pay 7 to make a player lose a card.")
                        : new Prompt("Your turn", "Tap an action. The line under its name is what it does.");

                case GamePhase.AwaitingChallenge:
                {
                    var headline = PendingHeadline(game);
                    if (!game.PendingResponders.Contains(me))
                    {
                        var waiting = game.ActingPlayerIndex == me
                            ? "Waiting to see if anyone calls your bluff."
                            : "Waiting for the other players.";
                        return new Prompt(headline, waiting + Accounted(game, game.PendingClaim));
                    }

                    if (game.MayBlock(me))
                        return new Prompt(headline,
                            $"Stop it with {Blockers(game)}, call the bluff, or let it happen.{HeldBlockers(game)}{Accounted(game, game.PendingClaim)}");
                    return new Prompt(headline,
                        $"Call the bluff if they don't have the {Claim(game.PendingClaim)}, or let it happen.{Accounted(game, game.PendingClaim)}");
                }

                case GamePhase.AwaitingBlock:
                {
                    var headline = PendingHeadline(game);
                    if (!game.PendingResponders.Contains(me))
                    {
                        var waiting = game.ActingPlayerIndex == me
                            ? "Waiting to see if they stop it."
                            : "Waiting for the other players.";
                        return new Prompt(headline, waiting);
                    }

                    return new Prompt(headline, $"Stop it with {Blockers(game)}, or let it happen.{HeldBlockers(game)}");
                }

                case GamePhase.AwaitingBlockChallenge:
                {
                    var who = Mine(game.PendingBlockerIndex) ? "You stop it" : $"{Name(game, game.PendingBlockerIndex)} stops it";
                    var headline = $"{who} with the {Claim(game.PendingBlockClaim)}";
                    if (!game.PendingResponders.Contains(me))
                    {
                        var waiting = game.PendingBlockerIndex == me
                            ? "Waiting to see if anyone calls your bluff."
                            : "Waiting for the other players.";
                        return new Prompt(headline, waiting + Accounted(game, game.PendingBlockClaim));
                    }

                    return new Prompt(headline, $"Call the bluff if they don't have it, or let the stop stand.{Accounted(game, game.PendingBlockClaim)}");
                }

                case GamePhase.AwaitingInfluenceLoss:
                    return game.PendingLoserIndex == me
                        ? new Prompt("You lose a card", "Tap one of your cards to reveal it and lose it.")
                        : new Prompt($"{Name(game, game.PendingLoserIndex)} loses a card", "They are choosing which card to reveal.");

                case GamePhase.AwaitingExchangeSelection:
                    return game.ActingPlayerIndex == me
                        ? new Prompt("Swap your cards", $"Keep {game.ExchangeKeepCount}. The rest go back to the deck.")
                        : new Prompt($"{Name(game, game.ActingPlayerIndex)} is swapping cards", "They claimed the Ambassador.");
            }

            return new Prompt("", "");
        }

        private Prompt TargetingPrompt(ActionKind kind) => kind switch
        {
            ActionKind.Steal => new Prompt("Steal 2 coins",
                "Tap a player. You claim the Captain. They can stop you. Tap Steal again to cancel."),
            ActionKind.Assassinate => new Prompt("Make them lose a card",
                "Tap a player. This costs 3 coins and claims the Assassin. Tap Assassinate again to cancel."),
            ActionKind.Coup => new Prompt("Force a lost card",
                "Tap a player. This costs 7 coins and cannot be stopped. Tap Coup again to cancel."),
            _ => new Prompt("Choose a player", "Tap who this hits. Tap the action again to cancel.")
        };

        private string PendingHeadline(CoupGame game)
        {
            var kind = game.PendingActionKind;
            if (!kind.HasValue) return "Waiting";
            return ActionHeadline(game, kind.Value, game.ActingPlayerIndex, game.PendingTargetIndex);
        }

        private string ActionHeadline(CoupGame game, ActionKind kind, int actor, int target)
        {
            var me = Mine(actor);
            var who = Name(game, actor);
            var them = target >= 0 ? (Mine(target) ? "you" : Name(game, target)) : "someone";
            return kind switch
            {
                ActionKind.Income => me ? "You take 1 coin" : $"{who} takes 1 coin",
                ActionKind.ForeignAid => me ? "You take 2 coins" : $"{who} takes 2 coins",
                ActionKind.Tax => me ? "You take 3 coins" : $"{who} takes 3 coins",
                ActionKind.Steal => me ? $"You steal 2 coins from {them}" : $"{who} steals 2 coins from {them}",
                ActionKind.Assassinate => me ? $"You make {them} lose a card" : $"{who} makes {them} lose a card",
                ActionKind.Exchange => me ? "You swap your cards" : $"{who} swaps cards",
                ActionKind.Coup => me ? $"You force {them} to lose a card" : $"{who} forces {them} to lose a card",
                _ => Label(kind)
            };
        }

        private static string Effect(ActionKind kind) => kind switch
        {
            ActionKind.Income => "Get 1 <sprite name=\"coin\" tint=0>",
            ActionKind.ForeignAid => "Get 2 <sprite name=\"coin\" tint=0>",
            ActionKind.Tax => "Get 3 <sprite name=\"coin\" tint=0>",
            ActionKind.Steal => "Take 2 <sprite name=\"coin\" tint=0>",
            ActionKind.Assassinate => "Pay 3 <sprite name=\"coin\" tint=0>",
            ActionKind.Exchange => "Draw new cards",
            ActionKind.Coup => "Pay 7 <sprite name=\"coin\" tint=0>",
            _ => ""
        };

        private static string ShortDeed(ActionKind? kind) => kind switch
        {
            ActionKind.Income => "Took 1",
            ActionKind.ForeignAid => "Took 2",
            ActionKind.Tax => "Took 3",
            ActionKind.Steal => "Stealing",
            ActionKind.Assassinate => "Killing",
            ActionKind.Exchange => "Swapping",
            ActionKind.Coup => "Coup",
            _ => null
        };

        private string BlockCaption(CoupGame game) => game.PendingActionKind switch
        {
            ActionKind.Steal => "Keep your coins",
            ActionKind.Assassinate => "Keep your card",
            ActionKind.ForeignAid => "Stop their coins",
            _ => "Stop this action"
        };

        private bool Mine(int seat) => seat == match.humanSeat;

        private static string Name(CoupGame game, int i) =>
            i >= 0 && i < game.Players.Count ? game.Players[i].Name : "Someone";

        private string Claim(Role? role) =>
            role.HasValue ? (art?.DisplayName(role.Value) ?? role.Value.ToString()) : "a character";

        /// <summary>
        /// Cards of this character the human can already count: their own hand,
        /// every revealed card, and the cards they are holding during an exchange.
        /// There are only <see cref="Rules.CopiesPerRole"/> in the deck.
        /// </summary>
        private int SeenCopies(CoupGame game, Role role)
        {
            var seen = 0;
            foreach (var player in game.Players)
            {
                if (player.Index == match.humanSeat)
                    foreach (var card in player.Influence)
                        if (card == role) seen++;
                foreach (var card in player.Revealed)
                    if (card == role) seen++;
            }

            if (game.Phase == GamePhase.AwaitingExchangeSelection &&
                game.ActingPlayerIndex == match.humanSeat)
                foreach (var card in game.ExchangeDrawn)
                    if (card == role) seen++;

            return seen;
        }

        private string Accounted(CoupGame game, Role? role)
        {
            if (!role.HasValue) return "";
            var seen = SeenCopies(game, role.Value);
            var name = Claim(role);
            if (seen >= Rules.CopiesPerRole)
                return $" All {Rules.CopiesPerRole} {name} cards are already accounted for, so this is a bluff.";
            if (seen == 0)
                return $" None of the {Rules.CopiesPerRole} {name} cards are accounted for yet.";
            return $" {seen} of {Rules.CopiesPerRole} {name} cards are already accounted for.";
        }

        private string Label(ActionKind? kind) =>
            kind.HasValue ? (art?.For(kind.Value)?.label ?? kind.Value.ToString()) : "an action";

        private string Blockers(CoupGame game)
        {
            var kind = game.PendingActionKind;
            if (!kind.HasValue) return "a character";
            var names = Rules.BlockingRoles(kind.Value)
                .Select(r => art?.DisplayName(r) ?? r.ToString());
            return string.Join(" or ", names);
        }

        /// <summary>Names the blocking characters this player actually holds, if any.</summary>
        private string HeldBlockers(CoupGame game)
        {
            var kind = game.PendingActionKind;
            if (!kind.HasValue) return "";

            var held = Rules.BlockingRoles(kind.Value)
                .Where(r => game.Players[match.humanSeat].Influence.Contains(r))
                .Select(r => art?.DisplayName(r) ?? r.ToString())
                .ToList();
            return held.Count == 0 ? "" : $" You hold {string.Join(" and ", held)}.";
        }

        private static void SetResponse(Button button, string title, string caption)
        {
            if (button == null) return;
            var label = button.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label != null) label.text = title;
            var line = button.transform.Find("Caption")?.GetComponent<TextMeshProUGUI>();
            if (line != null) line.text = caption;
        }

        private static void ShowResponse(Button button, bool show)
        {
            if (button != null) button.gameObject.SetActive(show);
        }

        /// <summary>Packs whichever response buttons are on, so Block is not stranded at the screen edge.</summary>
        private void LayoutResponses()
        {
            var shown = new List<Button>();
            if (_blockButton.gameObject.activeSelf) shown.Add(_blockButton);
            if (_challengeButton.gameObject.activeSelf) shown.Add(_challengeButton);
            if (_passButton.gameObject.activeSelf) shown.Add(_passButton);

            var slot = 340f;
            for (var i = 0; i < shown.Count; i++)
            {
                var rt = (RectTransform)shown[i].transform;
                rt.anchoredPosition = new Vector2((i - (shown.Count - 1) / 2f) * slot, 0f);
            }
        }

        // ----------------------------------------------------------- modal driving

        private void RefreshModal(CoupGame game)
        {
            var exchanging = game.Phase == GamePhase.AwaitingExchangeSelection &&
                             game.ActingPlayerIndex == match.humanSeat;

            if (!exchanging || _modal.IsOpen) return;

            _modal.ShowCards(
                "Swap your cards",
                $"Keep {game.ExchangeKeepCount}. The rest go back to the deck.",
                game.ExchangePool(),
                game.ExchangeKeepCount,
                keep => match.HumanExchangeKeep(keep.ToList()));
        }

        private void ShowRules()
        {
            // One terse line per action here; the full wording lives behind a long
            // press on the action itself, so this stays scannable.
            var body = string.Join("\n", ActionOrder.Select(k =>
                $"<b>{art?.For(k)?.label ?? k.ToString()}</b>   {ShortRule(k)}"));

            _modal.ShowCards("The Court", body, Rules.AllRoles, 0, null);
        }

        private static string ShortRule(ActionKind kind) => kind switch
        {
            ActionKind.Income => "take 1 · unstoppable",
            ActionKind.ForeignAid => "take 2 · Duke blocks",
            ActionKind.Tax => "take 3 · claim Duke",
            ActionKind.Steal => "take 2 · claim Captain · Captain or Ambassador blocks",
            ActionKind.Assassinate => "pay 3 · claim Assassin · Contessa blocks",
            ActionKind.Exchange => "swap cards · claim Ambassador",
            ActionKind.Coup => "pay 7 · unstoppable · forced at 10 coins",
            _ => ""
        };

        private void ShowSettings() =>
            _modal.ShowInfo("Match",
                $"Seats: {match.playerCount}\nYou are seat {match.humanSeat + 1}.\n\n" +
                "Lose both cards and you are out. Last player standing wins.\n\n" +
                "Any claim can be called a bluff. If they really have the card, you lose one. If they were lying, they lose one.");

        private void ShowActionInfo(ActionKind kind)
        {
            var game = match?.Game;
            var entry = art?.For(kind);

            var body = ActionSummary(kind);
            if (game != null)
            {
                var error = game.DeclareError(match.humanSeat, kind,
                    Rules.RequiresTarget(kind) ? FirstLegalTarget(game, kind) : -1);
                if (error != null) body += $"\n\n<color=#D98A8A>Not available: {error}</color>";
            }

            _modal.ShowInfo($"{entry?.label ?? kind.ToString()}  ·  {entry?.subtitle}", body);
        }

        private int FirstLegalTarget(CoupGame game, ActionKind kind)
        {
            foreach (var p in game.Players)
                if (p.Index != match.humanSeat && p.IsAlive) return p.Index;
            return -1;
        }

        /// <summary>Plain-language rules text, so nobody has to know Coup beforehand.</summary>
        private static string ActionSummary(ActionKind kind) => kind switch
        {
            ActionKind.Income =>
                "Take 1 coin. No character needed. Cannot be challenged or blocked.",
            ActionKind.ForeignAid =>
                "Take 2 coins. No character needed, so it cannot be called a bluff — but anyone claiming the Duke may stop it.",
            ActionKind.Tax =>
                "Claim the Duke to take 3 coins. Someone may say you are bluffing. Nobody can stop it.",
            ActionKind.Steal =>
                "Claim the Captain to take 2 coins from a player (or 1 if that is all they have). Can be challenged, and the victim may block with Captain or Ambassador.",
            ActionKind.Assassinate =>
                "Pay 3 coins and claim the Assassin to make a player lose a card. Can be called a bluff, and the victim may stop it with the Contessa. The 3 coins stay spent if it is stopped.",
            ActionKind.Exchange =>
                "Claim the Ambassador to draw 2 cards from the deck and keep the best of your hand. Can be called a bluff. Nobody can stop it.",
            ActionKind.Coup =>
                "Pay 7 coins to force a player to lose a card. It always works. Required once you hold 10 coins.",
            _ => ""
        };

        // ------------------------------------------------------------------ input

        private void OnActionClicked(ActionKind kind)
        {
            var game = match?.Game;
            if (game == null || game.Phase != GamePhase.AwaitingAction) return;
            if (game.CurrentPlayerIndex != match.humanSeat) return;

            if (Rules.RequiresTarget(kind))
            {
                _awaitingTarget = _awaitingTarget == kind ? (ActionKind?)null : kind;
                Refresh();
                return;
            }

            _awaitingTarget = null;
            match.HumanDeclare(kind);
        }

        private void OnOpponentClicked(PlayerPanelView panel)
        {
            if (!_awaitingTarget.HasValue) return;
            var kind = _awaitingTarget.Value;
            _awaitingTarget = null;
            match.HumanDeclare(kind, panel.Seat);
        }

        private void OnHandCardClicked(CardView card)
        {
            var game = match?.Game;
            if (game == null || game.Phase != GamePhase.AwaitingInfluenceLoss) return;
            if (game.PendingLoserIndex != match.humanSeat || !card.Role.HasValue) return;

            card.PlayLost();
            match.HumanLoseInfluence(card.Role.Value);
        }

        /// <summary>
        /// Blocking is a claim like any other, and against a Steal there are two
        /// different characters to claim — so the player picks, rather than the UI
        /// deciding for them and silently removing a bluff.
        /// </summary>
        private void OnBlockPressed()
        {
            var game = match?.Game;
            var kind = game?.PendingActionKind;
            if (!kind.HasValue) return;

            var options = Rules.BlockingRoles(kind.Value);
            if (options.Length == 0) return;

            if (options.Length == 1)
            {
                match.HumanBlock(options[0]);
                return;
            }

            var held = game.Players[match.humanSeat].Influence;
            var choices = options.Select(role =>
            {
                var have = held.Contains(role);
                var label = $"{art?.DisplayName(role) ?? role.ToString()}{(have ? "" : "  (bluff)")}";
                var tint = have ? new Color(0.16f, 0.34f, 0.24f) : new Color(0.34f, 0.18f, 0.20f);
                return (label, tint, (System.Action)(() => match.HumanBlock(role)));
            }).ToList();

            _modal.ShowChoices("Stop it with which card?",
                "They can say you don't have it. Picking a card you don't hold is a bluff — if they catch you, you lose a card.",
                choices);
        }

        /// <summary>
        /// The header bar is scaled down to fit the table, which leaves the log
        /// unreadably small. Size the lines for the screen, not the shrunken bar.
        /// </summary>
        private void EnlargeHeaderLog()
        {
            if (_logText == null) return;
            _logText.enableAutoSizing = false;
            _logText.fontSize = 38f;
            _logText.alignment = TextAlignmentOptions.MidlineLeft;
            var rt = _logText.rectTransform;
            rt.sizeDelta = new Vector2(640f, 136f);
            rt.anchoredPosition = new Vector2(-160f, 0f);
        }

        // -------------------------------------------------------------------- log

        private void OnEvent(GameEvent e)
        {
            TrackClaims(e);
            PlayBeat(e);

            var line = Describe(e);
            if (string.IsNullOrEmpty(line)) return;

            _log.Add(line);
            while (_log.Count > 3) _log.RemoveAt(0);
            if (_logText != null) _logText.text = string.Join("\n", _log);
        }

        /// <summary>Remembers each seat's latest claim so the panels can display it.</summary>
        private void TrackClaims(GameEvent e)
        {
            switch (e.Kind)
            {
                case GameEventKind.ActionDeclared:
                    if (e.Role.HasValue) _lastClaim[e.PlayerIndex] = e.Role.Value;
                    else _lastClaim.Remove(e.PlayerIndex);
                    if (ShortDeed(e.Action) is { } deed) _lastDeed[e.PlayerIndex] = deed;
                    break;

                case GameEventKind.BlockDeclared:
                    if (e.Role.HasValue) _lastClaim[e.PlayerIndex] = e.Role.Value;
                    _lastDeed[e.PlayerIndex] = "Stopping";
                    break;

                case GameEventKind.ChallengeLost:
                    // The claim was exposed as a lie, so stop advertising it.
                    _lastClaim.Remove(e.PlayerIndex);
                    _lastDeed.Remove(e.PlayerIndex);
                    break;
            }
        }

        /// <summary>A bright version of the character's colour, so a dark accent still reads as a hit.</summary>
        private Color Signal(Role role)
        {
            var c = art != null ? art.Accent(role) : Color.white;
            Color.RGBToHSV(c, out var h, out var s, out var v);
            var glow = Color.HSVToRGB(h, Mathf.Max(s, 0.62f), Mathf.Max(v, 0.92f));
            glow.a = 1f;
            return glow;
        }

        private void HitSeat(int seat)
        {
            if (_vfx == null) return;
            if (seat == match.humanSeat && _hudName != null)
                CoupTween.Shake((RectTransform)_hudName.transform.parent, 8f, CoupTween.Quick);
            else
            {
                var panel = _panels.FirstOrDefault(p => p.Seat == seat);
                if (panel != null) CoupTween.Shake(panel.root, 10f, CoupTween.Quick);
            }
        }

        private void DukeFeel(int seat)
        {
            if (_vfx == null) return;
            var at = SeatPosition(seat);
            var colour = Signal(Role.Duke);
            _vfx.Impact(at, colour);
            _vfx.Rise(at, colour);
            _vfx.Flash(new Color(colour.r, colour.g, colour.b, 0.14f), 0.28f);
            HitSeat(seat);
        }

        private void CaptainFeel(int actor, int target)
        {
            if (_vfx == null) return;
            var colour = Signal(Role.Captain);
            var from = SeatPosition(actor);
            _vfx.Impact(from, colour);
            if (target >= 0) _vfx.Streak(from, SeatPosition(target), colour, 10);
            HitSeat(actor);
        }

        private void AmbassadorFeel(int seat)
        {
            if (_vfx == null) return;
            var at = SeatPosition(seat);
            var colour = Signal(Role.Ambassador);
            _vfx.Impact(at, colour);
            _vfx.Streak(at + new Vector2(-80f, 0f), at + new Vector2(80f, 28f), colour, 6);
            _vfx.Streak(at + new Vector2(80f, 0f), at + new Vector2(-80f, 28f), colour, 6);
            HitSeat(seat);
        }

        /// <summary>One title for the action. Character art is reserved for a block.</summary>
        private void ShowActionBanner(GameEvent e)
        {
            if (_vfx == null || art == null || !e.Action.HasValue) return;
            var sprite = art.Callout(e.Action.Value);
            if (sprite == null) return;
            var cover = PanelCover(e.PlayerIndex);
            _vfx.ShowBanner(sprite, cover.Center, e.PlayerIndex, cover.Width);
        }

        /// <summary>The lost-influence title covers the panel of the player who revealed a card.</summary>
        private void ShowInfluenceLostBanner(GameEvent e)
        {
            if (_vfx == null || art == null || art.influenceLostCallout == null) return;
            var cover = PanelCover(e.PlayerIndex);
            _vfx.ShowBanner(art.influenceLostCallout, cover.Center, e.PlayerIndex, cover.Width);
        }

        /// <summary>A block claims one character, so that character's title is the only banner.</summary>
        private void ShowRoleBanner(GameEvent e)
        {
            if (_vfx == null || art == null || !e.Role.HasValue) return;
            var sprite = art.Callout(e.Role.Value);
            if (sprite == null) return;
            var cover = PanelCover(e.PlayerIndex);
            _vfx.ShowBanner(sprite, cover.Center, e.PlayerIndex, cover.Width);
        }

        private void PlayActionFeel(GameEvent e)
        {
            if (!e.Action.HasValue || _vfx == null) return;
            switch (e.Action.Value)
            {
                case ActionKind.Tax:
                    DukeFeel(e.PlayerIndex);
                    break;
                case ActionKind.Steal:
                    CaptainFeel(e.PlayerIndex, e.TargetIndex);
                    break;
                case ActionKind.Exchange:
                    AmbassadorFeel(e.PlayerIndex);
                    break;
                case ActionKind.Assassinate:
                    _vfx.BeginBloodLine(SeatPosition(e.PlayerIndex), SeatPosition(e.TargetIndex));
                    break;
            }
        }

        private void PlayBlockFeel(GameEvent e)
        {
            if (!e.Role.HasValue || _vfx == null) return;
            switch (e.Role.Value)
            {
                case Role.Duke:
                    DukeFeel(e.PlayerIndex);
                    break;
                case Role.Captain:
                    CaptainFeel(e.PlayerIndex, e.TargetIndex);
                    break;
                case Role.Ambassador:
                    AmbassadorFeel(e.PlayerIndex);
                    if (e.TargetIndex >= 0)
                        _vfx.Streak(SeatPosition(e.PlayerIndex), SeatPosition(e.TargetIndex), Signal(Role.Ambassador), 8);
                    break;
                case Role.Contessa:
                    _vfx.ShatterBloodLine();
                    HitSeat(e.PlayerIndex);
                    break;
            }
        }

        /// <summary>
        /// The assassination landed. If the Contessa already broke the line and the
        /// block was a bluff, the shot draws itself again and then strikes.
        /// </summary>
        private void ResolveBloodLine(GameEvent e, bool hit)
        {
            if (_vfx == null) return;
            var redraw = !_vfx.HasBloodLine;
            if (redraw)
                _vfx.BeginBloodLine(SeatPosition(e.PlayerIndex), SeatPosition(e.TargetIndex));
            _vfx.ReleaseBloodLine(hit, redraw ? 0.6f : 0.2f);
        }

        /// <summary>
        /// The challenger and the claimant face each other: a beam and sparks
        /// run from both seats and meet between them.
        /// </summary>
        private void PlayClash(GameEvent e)
        {
            if (_vfx == null) return;
            if (_status != null)
                CoupTween.Shake((RectTransform)_status.transform.parent, 12f, CoupTween.Normal);

            var challenger = SeatPosition(e.PlayerIndex);
            var claimant = SeatPosition(e.TargetIndex);
            FlyChallenge(e);
            _vfx.Clash(challenger, claimant);
            _vfx.Flash(new Color(0.55f, 0.08f, 0.1f, 0.18f), 0.28f);
            HitSeat(e.PlayerIndex);
            HitSeat(e.TargetIndex);

            var red = new Color(0.95f, 0.28f, 0.22f, 1f);
            var gold = new Color(1f, 0.78f, 0.32f, 1f);
            CoupTween.DelayedCall(0.48f, () =>
            {
                if (_vfx == null) return;
                _vfx.Streak(challenger, claimant, red, 8);
                _vfx.Streak(claimant, challenger, gold, 8);
            });
        }

        /// <summary>The Challenge title leaves the challenger and lands on the player they called.</summary>
        private void FlyChallenge(GameEvent e)
        {
            if (_vfx == null || art == null || art.challengeCallout == null) return;
            var from = PanelCover(e.PlayerIndex);
            var to = e.TargetIndex >= 0 ? PanelCover(e.TargetIndex) : from;
            _vfx.FlyBanner(art.challengeCallout, from.Center, to.Center, e.PlayerIndex, from.Width);
        }

        /// <summary>
        /// Lifts the challenged player's card so the table can read it. A proven
        /// claim then flies back into the deck and a replacement comes out.
        /// </summary>
        private void ShowTheirCard(int seat, Role role, bool swap)
        {
            if (cardPrefab == null || _vfx == null || _vfx.Layer == null) return;
            RetireReveal();

            var card = Instantiate(cardPrefab, _vfx.Layer);
            card.Bind(art);
            card.SetInteractable(false);
            if (card.group != null) card.group.blocksRaycasts = false;
            card.SetDisplayWidth(270f);
            card.ShowBack();

            var rt = card.root != null ? card.root : (RectTransform)card.transform;
            var home = CardAnchor(seat);
            rt.anchoredPosition = home;
            rt.localRotation = Quaternion.identity;
            _revealCard = card;

            CoupTween.ArcTo(rt, Showcase(home), card.RestingScale, 0.34f, 80f, () =>
            {
                if (card == null) return;
                card.ShowRole(role, true);
            });

            if (!swap)
            {
                CoupTween.DelayedCall(1.25f, () =>
                {
                    if (_revealCard == card) RetireReveal();
                });
                return;
            }

            CoupTween.DelayedCall(1.35f, () => FlySwap(card, home));
        }

        private void FlySwap(CardView shown, Vector2 home)
        {
            if (shown == null || _vfx == null) return;
            var rt = shown.root != null ? shown.root : (RectTransform)shown.transform;
            var deck = _deckRect != null ? _vfx.PositionOf(_deckRect) : Vector2.zero;
            CoupTween.ArcTo(rt, deck, shown.RestingScale * 0.42f, 0.42f, 100f, () =>
            {
                if (_revealCard == shown) RetireReveal();
                DealReplacement(deck, home);
            });
        }

        private void DealReplacement(Vector2 deck, Vector2 home)
        {
            if (cardPrefab == null || _vfx == null || _vfx.Layer == null) return;
            var card = Instantiate(cardPrefab, _vfx.Layer);
            card.Bind(art);
            card.SetInteractable(false);
            if (card.group != null) card.group.blocksRaycasts = false;
            card.SetDisplayWidth(210f);
            card.ShowBack();

            var rt = card.root != null ? card.root : (RectTransform)card.transform;
            rt.anchoredPosition = deck;
            rt.localScale = card.RestingScale * 0.4f;
            CoupTween.ArcTo(rt, home, card.RestingScale * 0.7f, 0.42f, 100f, () =>
            {
                if (card == null) return;
                if (card.group != null)
                    CoupTween.Fade(card.group, 0f, 0.16f, () => { if (card != null) Destroy(card.gameObject); });
                else
                    Destroy(card.gameObject);
            });
        }

        private void RetireReveal()
        {
            if (_revealCard == null) return;
            var card = _revealCard;
            _revealCard = null;
            if (card.root != null) CoupTween.Kill(card.root);
            Destroy(card.gameObject);
        }

        /// <summary>Where a seat's cards sit, so a reveal starts in that player's hand.</summary>
        private Vector2 CardAnchor(int seat)
        {
            if (_vfx == null) return SeatPosition(seat);
            if (seat == match.humanSeat && _handRoot != null)
                return _vfx.PositionOf(_handRoot);

            var panel = _panels.FirstOrDefault(p => p.Seat == seat);
            var card = panel != null ? panel.GetCard(0) : null;
            if (card != null) return _vfx.PositionOf((RectTransform)card.transform);
            return SeatPosition(seat);
        }

        /// <summary>Pulls a shown card off its seat, toward the table, so the face can be read.</summary>
        private static Vector2 Showcase(Vector2 home)
        {
            var inward = -home;
            if (inward.sqrMagnitude < 1f) inward = Vector2.up;
            return home + inward.normalized * 120f;
        }

        /// <summary>Gives each rules event a matching piece of stagecraft.</summary>
        private void PlayBeat(GameEvent e)
        {
            switch (e.Kind)
            {
                case GameEventKind.ChallengeDeclared:
                    PlayClash(e);
                    break;

                case GameEventKind.ChallengeWon:
                    if (e.Role.HasValue) ShowTheirCard(e.PlayerIndex, e.Role.Value, swap: true);
                    _vfx.Burst(SeatPosition(e.PlayerIndex), new Color(1f, 0.85f, 0.4f, 1f), 18);
                    break;

                case GameEventKind.ActionDeclared:
                    PlayActionFeel(e);
                    ShowActionBanner(e);
                    break;

                case GameEventKind.BlockDeclared:
                    PlayBlockFeel(e);
                    ShowRoleBanner(e);
                    break;

                case GameEventKind.ActionResolved:
                    if (e.Action == ActionKind.Assassinate)
                        ResolveBloodLine(e, hit: true);
                    break;

                case GameEventKind.ActionFailed:
                    if (_vfx != null) _vfx.ReleaseBloodLine(false, 0.05f);
                    break;

                case GameEventKind.ExchangeCompleted:
                    AmbassadorFeel(e.PlayerIndex);
                    break;

                case GameEventKind.InfluenceLost:
                {
                    ShowInfluenceLostBanner(e);
                    var panel = _panels.FirstOrDefault(p => p.Seat == e.PlayerIndex);
                    if (panel != null) CoupTween.Shake(panel.root, 14f, CoupTween.Slow);

                    var colour = e.Role.HasValue
                        ? UIFactory.Readable(art != null ? art.Accent(e.Role.Value) : Color.white)
                        : Color.white;
                    _vfx.Burst(SeatPosition(e.PlayerIndex), colour, 22, 520f);

                    // The lost card turns face up on its seat. A second copy here
                    // would look like an extra card of that character.
                    break;
                }

                case GameEventKind.PlayerEliminated:
                    _vfx.Flash(new Color(0.05f, 0.02f, 0.04f, 0.5f), 0.5f);
                    _vfx.Burst(SeatPosition(e.PlayerIndex), new Color(0.8f, 0.2f, 0.2f, 1f), 28, 620f);
                    break;

                case GameEventKind.CoinsChanged:
                {
                    if (e.Amount > 0) PlayCoinGain(e.PlayerIndex, e.Amount);
                    else if (e.Amount < 0) PlayBootCollect(e.PlayerIndex, -e.Amount);
                    if (e.PlayerIndex == match.humanSeat && _hudCoins != null)
                        CoupTween.Punch(_hudCoins.transform, 0.3f, CoupTween.Quick);
                    break;
                }

                case GameEventKind.GameOver:
                    _vfx.Flash(e.PlayerIndex == match.humanSeat
                        ? new Color(1f, 0.88f, 0.5f, 0.45f)
                        : new Color(0.1f, 0.05f, 0.08f, 0.55f), 0.7f);
                    break;
            }
        }

        /// <summary>
        /// The opening of a match: cards leave the deck one at a time, then each
        /// seat collects its starting coins from the pile.
        /// </summary>
        private void PlayOpening()
        {
            if (_openingPlayed || match?.Game == null || _vfx == null || _deckRect == null) return;
            if (_hand.Count == 0) return;

            var game = match.Game;
            _openingPlayed = true;

            var deckWorld = _deckRect.TransformPoint(Vector3.zero);
            var dealt = 0;

            for (var round = 0; round < Rules.StartingInfluence; round++)
            {
                for (var seat = 0; seat < game.Players.Count; seat++)
                {
                    var card = CardFor(seat, round);
                    if (card == null) continue;

                    var rt = (RectTransform)card.transform;
                    var parent = rt.parent as RectTransform;
                    var dest = rt.anchoredPosition;
                    var from = parent != null ? (Vector2)parent.InverseTransformPoint(deckWorld) : dest;

                    var reveal = false;
                    Role? role = null;
                    if (seat == match.humanSeat && round < game.Players[seat].Influence.Count)
                    {
                        reveal = true;
                        role = game.Players[seat].Influence[round];
                    }

                    card.BeginDeal(from, dest, dealt * 0.09f, reveal, role);
                    dealt++;
                }
            }

            if (_coinPileRect == null) return;
            var pile = _vfx.PositionOf(_coinPileRect);
            for (var seat = 0; seat < game.Players.Count; seat++)
            {
                var coins = game.Players[seat].Coins;
                if (coins <= 0) continue;
                _vfx.CoinFly(pile, CoinPosition(seat), coins, 0.2f + seat * 0.14f);
            }
        }

        private CardView CardFor(int seat, int index)
        {
            if (seat == match.humanSeat)
                return index >= 0 && index < _hand.Count ? _hand[index] : null;

            var panel = _panels.FirstOrDefault(p => p.Seat == seat);
            return panel != null ? panel.GetCard(index) : null;
        }

        private void PlayCoinGain(int seat, int amount)
        {
            if (_vfx == null) return;
            var treasury = _coinPileRect != null ? _vfx.PositionOf(_coinPileRect) : new Vector2(0f, CentreY);
            _vfx.CoinFly(treasury, CoinPosition(seat), Mathf.Clamp(amount, 1, 8));
        }

        /// <summary>Coins paid or returned travel back into the central pile.</summary>
        private void PlayBootCollect(int seat, int amount)
        {
            if (_vfx == null) return;
            var treasury = _coinPileRect != null ? _vfx.PositionOf(_coinPileRect) : new Vector2(0f, CentreY);
            _vfx.CoinFly(CoinPosition(seat), treasury, Mathf.Clamp(amount, 1, 8));
        }

        private Vector2 CoinPosition(int seat)
        {
            if (_vfx == null) return SeatPosition(seat);
            if (seat == match.humanSeat && _hudCoinIcon != null)
                return _vfx.PositionOf(_hudCoinIcon);

            var panel = _panels.FirstOrDefault(p => p.Seat == seat);
            if (panel != null && panel.coinIcon != null)
                return _vfx.PositionOf((RectTransform)panel.coinIcon.transform);
            return SeatPosition(seat);
        }

        /// <summary>Where a seat sits on screen, for aiming an effect at it.</summary>
        private Vector2 SeatPosition(int seat)
        {
            if (seat < 0) return Vector2.zero;
            if (seat == match.humanSeat)
                return _hudRect != null && _vfx != null
                    ? _vfx.PositionOf(_hudRect)
                    : new Vector2(0f, HudY);

            var panel = _panels.FirstOrDefault(p => p.Seat == seat);
            return panel != null ? _vfx.PositionOf(panel.root) : Vector2.zero;
        }

        /// <summary>Centre and width of a seat's panel, so the title sits on that panel.</summary>
        private (Vector2 Center, float Width) PanelCover(int seat)
        {
            var panel = seat == match.humanSeat
                ? _hudRect
                : _panels.FirstOrDefault(p => p.Seat == seat)?.root;

            if (panel == null || _vfx == null || _vfx.Layer == null)
                return (SeatPosition(seat), 260f);

            var center = (Vector2)_vfx.Layer.InverseTransformPoint(panel.TransformPoint(panel.rect.center));
            var edge = (Vector2)_vfx.Layer.InverseTransformPoint(
                panel.TransformPoint(new Vector3(panel.rect.xMax, panel.rect.center.y, 0f)));
            var width = Mathf.Clamp(Mathf.Abs(edge.x - center.x) * 2f, 200f, 420f);
            return (center, width);
        }

        private string Describe(GameEvent e)
        {
            var game = match?.Game;
            if (game == null) return null;

            var role = e.Role.HasValue ? Claim(e.Role) : "a card";
            return e.Kind switch
            {
                GameEventKind.ActionDeclared when e.Action.HasValue =>
                    ActionHeadline(game, e.Action.Value, e.PlayerIndex, e.TargetIndex) + ".",
                GameEventKind.ChallengeDeclared =>
                    Mine(e.PlayerIndex)
                        ? $"You say {Lacks(game, e.TargetIndex, role)}."
                        : $"{Name(game, e.PlayerIndex)} says {Lacks(game, e.TargetIndex, role)}.",
                GameEventKind.ChallengeWon =>
                    $"{Had(game, e.PlayerIndex, role)}. {LosesCard(game, e.TargetIndex)}.",
                GameEventKind.ChallengeLost =>
                    Mine(e.PlayerIndex) ? $"You were bluffing the {role}." : $"{Name(game, e.PlayerIndex)} was bluffing the {role}.",
                GameEventKind.BlockDeclared =>
                    Mine(e.PlayerIndex) ? $"You stop it with the {role}." : $"{Name(game, e.PlayerIndex)} stops it with the {role}.",
                GameEventKind.ActionResolved when e.Action.HasValue &&
                    (Rules.IsChallengeable(e.Action.Value) || Rules.IsBlockable(e.Action.Value)) =>
                    "It goes through.",
                GameEventKind.ActionFailed => "It is stopped.",
                GameEventKind.InfluenceLost =>
                    Mine(e.PlayerIndex) ? $"You reveal the {role} and lose it." : $"{Name(game, e.PlayerIndex)} reveals the {role} and loses it.",
                GameEventKind.PlayerEliminated =>
                    Mine(e.PlayerIndex) ? "You are out." : $"{Name(game, e.PlayerIndex)} is out.",
                GameEventKind.GameOver =>
                    Mine(e.PlayerIndex) ? "You win." : $"{Name(game, e.PlayerIndex)} wins.",
                _ => null
            };
        }

        private string Lacks(CoupGame game, int seat, string role) =>
            Mine(seat) ? $"you don't have the {role}" : $"{Name(game, seat)} doesn't have the {role}";

        private string Had(CoupGame game, int seat, string role) =>
            Mine(seat) ? $"You had the {role}" : $"{Name(game, seat)} had the {role}";

        private string LosesCard(CoupGame game, int seat) =>
            Mine(seat) ? "You lose a card" : $"{Name(game, seat)} loses a card";

        private readonly struct Prompt
        {
            public readonly string Title;
            public readonly string Body;

            public Prompt(string title, string body)
            {
                Title = title ?? "";
                Body = body ?? "";
            }
        }
    }
}
