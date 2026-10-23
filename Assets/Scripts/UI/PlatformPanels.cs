using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coup.Core;
using Coup.Net;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace Coup.Game.UI
{
    // Runtime panels reuse the lobby's art and fonts; authored prefab sources stay untouched.
    public sealed class PlatformPanels : MonoBehaviour
    {
        private Transform _content;
        private ScrollRect _scroll;
        private RectTransform _card, _viewport, _close;
        private float _maxCardHeight;
        public bool Loading => _status != null && _status.text == "Loading…";
        private GameObject _root;
        private TMP_Text _title, _status;
        private string _page;
        private CancellationTokenSource _view = new CancellationTokenSource();
        private bool _busy, _launching;
        private CoupArt _art;
        private PlatformClient _api;
        private Storefront _store;
        private string _storeStatus;
        public string CurrentPage => _page;
        public bool IsOpen => _page != null;
        private void Awake() {
            _api = PlatformClient.Instance; _api.Changed += UpdateHeader;
            _art = GetComponent<LobbyScreen>().art;
            if (_art != null) UIFactory.UseFonts(_art.displayFont,_art.bodyFont);
            Wire("Actions/ThisDevice",LaunchLocal);
            Wire("Actions/Rules",()=>Show("Rules"));
            Wire("Actions/Roles",()=>Show("Collection"));
            Wire("Actions/Court",()=>Show("Collection"));
            Wire("Actions/Play", () => Show("Play"));
            Wire("Actions/Casual", () => LaunchQueue("casual")); Wire("Actions/Ranked", () => LaunchQueue("ranked"));
            Wire("Actions/RankedTier", () => Show("Ranked")); Wire("Actions/SeasonPass", () => Show("Season"));
            Wire("Actions/Event", () => Show("Event")); Wire("Actions/Shop", () => Show("Shop"));
            Wire("Actions/Collection", () => Show("Collection")); Wire("Actions/Missions", () => Show("Missions"));
            Wire("Hud/Profile", () => Show("Profile")); Wire("Hud/Currency", () => Show("Wallet")); Wire("Hud/Gems", () => Show("Shop"));
            Wire("Hud/Settings", () => Show("Settings")); Wire("Actions/Settings", () => Show("Settings"));
            Wire("Actions/Friends", () => Show("Friends")); Wire("Actions/Table", () => Show("Private table"));
            Wire("FriendsSheet/Card/Host", () => LaunchPrivate(null));
            Wire("FriendsSheet/Card/Join", () => Show("Private table"));
            Build(); UpdateHeader();
        }
        private void OnDestroy() { _view.Cancel(); _api.Changed -= UpdateHeader; if (_store != null) _store.Changed -= OnStoreChanged; }
        private void Update() {
            if (IsOpen && Keyboard.current?.escapeKey.wasPressedThisFrame == true) Close();
        }
        private void Wire(string path, Action action) {
            var button=transform.Find(path)?.GetComponent<Button>(); if(button==null)return;
            button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => action());
        }
        private void UpdateHeader() {
            var p=_api.Profile;
            Label("Hud/Profile/Name",p?.Str("name") ?? "Player"); Label("Hud/Profile/Level",p==null ? "OFFLINE" : "Lv. " + p.Int("level",1));
            Label("Hud/Currency/Value",p==null ? "—" : p.Int("coins",0).ToString("N0"));
            Label("Hud/Gems/Value",p==null ? "—" : p.Int("gems",0).ToString("N0"));
            Label("Actions/RankedTier/Subtitle",p==null ? "Connect to view" : p.Int("rating",1000)+" rating");
            Label("Actions/SeasonPass/Progress","View season");
            Label("Actions/Ranked/Subtitle","Competitive matches");
            var progress=transform.Find("Hud/Profile/Progress")?.GetComponent<Image>();
            if(progress!=null) { progress.type=Image.Type.Filled;progress.fillMethod=Image.FillMethod.Horizontal;progress.fillOrigin=0;progress.fillAmount=p==null?0:(p.Int("xp",0)%500)/500f; }
        }
        private void Label(string path,string text) { var label=transform.Find(path)?.GetComponent<TMP_Text>(); if(label!=null) { label.richText=false; label.text=text; } }
        private void Build() {
            var root=UIFactory.Rect("PlatformPanels",transform,Vector2.zero,Vector2.zero); Stretch(root);
            var dim=root.gameObject.AddComponent<Image>();dim.color=new Color(.015f,.025f,.045f,.90f);_root=root.gameObject;
            var canvas=GetComponentInParent<Canvas>().transform as RectTransform;
            _maxCardHeight=Mathf.Min(1480,canvas.rect.height*.88f);
            var height=_maxCardHeight;
            var card=UIFactory.Rect("Card",root,new Vector2(940,height),Vector2.zero);_card=card;
            Plaque(card,new Color(.025f,.055f,.09f,.99f),UIFactory.Gold,0f);
            _title=UIFactory.Text("Title",card,"",42,new Vector2(800,90),new Vector2(0,height/2-75));_title.color=UIFactory.Gold;
            _status=UIFactory.Text("Status",card,"",27,new Vector2(820,120),new Vector2(0,height/2-175));_status.richText=false;
            var viewport=UIFactory.Rect("Viewport",card,new Vector2(840,height-430),new Vector2(0,-25));_viewport=viewport;
            var image=viewport.gameObject.AddComponent<Image>();image.color=new Color(1,1,1,.001f);viewport.gameObject.AddComponent<RectMask2D>();
            var content=UIFactory.Rect("Content",viewport,new Vector2(840,0),Vector2.zero);content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=18;layout.padding=new RectOffset(8,8,8,12);layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;
            var fit=content.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=50;_scroll=scroll;
            _content=content;
            // Back is a way out, not a fifth choice, so it stays narrower and quieter
            // than the rows above it rather than matching their full width.
            var close=UIFactory.Button("Close",card,null,new Vector2(420,78),new Vector2(0,-height/2+72));_close=(RectTransform)close.transform;
            var closeImage=close.GetComponent<Image>();closeImage.sprite=_art!=null?_art.roundedPanel:null;closeImage.type=Image.Type.Sliced;closeImage.preserveAspect=false;closeImage.color=new Color(.08f,.11f,.16f,.95f);
            var closeRim=UIFactory.Image("Rim",close.transform,_art!=null?_art.roundedBorder:null,new Vector2(420,78),Vector2.zero,false);closeRim.type=Image.Type.Sliced;closeRim.color=new Color(UIFactory.Gold.r,UIFactory.Gold.g,UIFactory.Gold.b,.45f);
            Stretch((RectTransform)closeRim.transform);
            UIFactory.Text("Label",close.transform,"BACK",28,new Vector2(380,68),Vector2.zero).color=new Color(.78f,.74f,.66f);
            PanelTheme.Button(close);
            close.onClick.AddListener(Close);
            _root.SetActive(false);
        }

        /// <summary>
        /// The lobby behind these panels is marble and antique gold. A flat fill with
        /// an Outline component reads as programmer art next to it, so panels borrow
        /// the 9-slice pair the card and player frames already use: roundedPanel for
        /// the fill, roundedBorder stretched over it for the rim.
        /// </summary>
        private void Plaque(RectTransform target,Color fill,Color rim,float inset) {
            var bg=target.gameObject.AddComponent<Image>();
            bg.sprite=_art!=null?_art.roundedPanel:null;bg.type=Image.Type.Sliced;bg.preserveAspect=false;bg.color=fill;
            var border=UIFactory.Image("Rim",target,_art!=null?_art.roundedBorder:null,target.sizeDelta,Vector2.zero,false);
            border.type=Image.Type.Sliced;border.color=rim;
            var rt=(RectTransform)border.transform;
            rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;
            rt.offsetMin=new Vector2(inset,inset);rt.offsetMax=new Vector2(-inset,-inset);
            if(target.name=="Card")PanelTheme.Surface(bg,border);
        }

        /// <summary>
        /// Sizes the card to what it is actually showing. The height used to be a flat
        /// min(1480, canvas*0.88), so the Play menu's four rows — about 400px — sat in
        /// a 1480px card and left a third of the screen empty beneath them. The 430 is
        /// the chrome the viewport does not cover: title, status and the Back row.
        /// </summary>
        private void FitCard() {
            if(_card==null)return;
            Canvas.ForceUpdateCanvases();
            // Status only needs room when it has something to say. A plain menu like
            // Play leaves it empty, and reserving its band regardless opened a hole
            // between the title and the first row.
            var hasStatus=_status!=null&&!string.IsNullOrEmpty(_status.text);
            var top=hasStatus?240f:150f;
            const float bottom=172f;
            var h=Mathf.Clamp(((RectTransform)_content).rect.height+top+bottom,560f,_maxCardHeight);
            var view=h-top-bottom;
            _card.sizeDelta=new Vector2(940,h);
            if(_viewport!=null){_viewport.sizeDelta=new Vector2(840,view);_viewport.anchoredPosition=new Vector2(0,h/2-top-view/2);}
            if(_title!=null)((RectTransform)_title.transform).anchoredPosition=new Vector2(0,h/2-72);
            if(_status!=null)((RectTransform)_status.transform).anchoredPosition=new Vector2(0,h/2-168);
            if(_close!=null)_close.anchoredPosition=new Vector2(0,-h/2+72);
        }
        private static void Stretch(RectTransform t) { t.anchorMin=Vector2.zero;t.anchorMax=Vector2.one;t.offsetMin=t.offsetMax=Vector2.zero; }
        public void Close() { _view.Cancel(); PanelTransition.For(_root,_card).Hide();_busy=false;_page=null; }
        public async void Show(string page) {
            _view.Cancel();_view.Dispose();_view=new CancellationTokenSource();var cancellation=_view.Token;
            _page=page;_busy=false;
            foreach(var old in new[]{"InformationSheet","FriendsSheet"}) {var t=transform.Find(old);if(t!=null)t.gameObject.SetActive(false);}
            PanelTransition.For(_root,_card).Show();_root.transform.SetAsLastSibling();Clear();_title.text=page.ToUpperInvariant();_status.text="Loading…";
            try { await Load(page,cancellation); cancellation.ThrowIfCancellationRequested(); if(_status.text=="Loading…")_status.text=""; FitCard(); Canvas.ForceUpdateCanvases(); _scroll.StopMovement(); _scroll.verticalNormalizedPosition=1; }
            catch(OperationCanceledException) { }
            catch(Exception error) { if(!cancellation.IsCancellationRequested && this!=null) { Clear();_status.text=error is ApiException || error is InvalidOperationException ? error.Message : "This screen could not load. Please try again.";Button("Try again",()=> {Show(page);return Task.CompletedTask;});Button("Play on this device",()=> {LaunchLocal();return Task.CompletedTask;}); if(error is ApiException api && api.Status==401) Button("Account recovery",()=>{Show("Account");return Task.CompletedTask;}); FitCard(); } }
        }
        private void Clear() { foreach(Transform t in _content) { t.gameObject.SetActive(false);Destroy(t.gameObject); } }
        private RectTransform Row(string name,float height) {var rt=UIFactory.Rect(name,_content,new Vector2(800,height),Vector2.zero);rt.gameObject.AddComponent<LayoutElement>().preferredHeight=height;return rt;}
        // Text rows used a flat 120 whatever they held, so a single line left about
        // 80px of slack underneath and the panels read as loosely spaced once the
        // card stopped padding that out. Measure the wrapped text and fit the row.
        private void Text(string value,float height=120) {
            var row=Row("Text",height);
            var label=UIFactory.Text("Body",row,value,31,new Vector2(790,height),Vector2.zero,TextAlignmentOptions.TopLeft);
            label.richText=false;
            var needed=Mathf.Clamp(label.GetPreferredValues(value,790f,0f).y+16f,40f,2400f);
            var element=row.GetComponent<LayoutElement>(); if(element!=null)element.preferredHeight=needed;
            row.sizeDelta=new Vector2(800,needed);
            ((RectTransform)label.transform).sizeDelta=new Vector2(790,needed);
        }
        private void Button(string label,Func<Task> action,bool enabled=true) {
            var row=Row(label,92);
            var gold=UIFactory.Gold;
            Plaque(row,enabled?new Color(.10f,.15f,.21f,.97f):new Color(.07f,.09f,.12f,.92f),
                       enabled?new Color(gold.r,gold.g,gold.b,.70f):new Color(.45f,.45f,.48f,.30f),2f);
            var button=row.gameObject.AddComponent<Button>();button.interactable=enabled;PanelTheme.Button(button);
            var text=UIFactory.Text("Label",row,label,30,new Vector2(740,80),Vector2.zero);text.richText=false;
            if(!enabled)text.color=new Color(.55f,.55f,.58f);
            button.onClick.AddListener(()=>Run(action));
        }
        private TMP_InputField Input(string hint,int limit=64) {
            // The field was a bare square black box among rounded, gold-rimmed rows,
            // which made it read as a hole in the panel. Same plaque, sunk darker and
            // with a dimmer rim so it still reads as something you type into.
            var row=Row("Input",100);
            var gold=UIFactory.Gold;
            Plaque(row,new Color(.012f,.028f,.05f,.98f),new Color(gold.r,gold.g,gold.b,.34f),2f);
            var input=row.gameObject.AddComponent<TMP_InputField>();var text=UIFactory.Text("Value",row,"",32,new Vector2(760,85),Vector2.zero,TextAlignmentOptions.MidlineLeft);text.richText=false;
            var placeholder=UIFactory.Text("Placeholder",row,hint,30,new Vector2(760,85),Vector2.zero,TextAlignmentOptions.MidlineLeft);placeholder.color=Color.gray;placeholder.richText=false;
            input.textViewport=row;input.textComponent=text;input.placeholder=placeholder;input.characterLimit=limit;input.lineType=TMP_InputField.LineType.SingleLine;return input;
        }
        private async void Run(Func<Task> action) {
            if(_busy)return;_busy=true;var cancellation=_view.Token;
            try {await action();}
            catch(OperationCanceledException) { }
            catch(Exception error) {if(this!=null && !cancellation.IsCancellationRequested)_status.text=error is ApiException || error is InvalidOperationException ? error.Message : "The request could not complete. Please try again.";}
            finally {if(!cancellation.IsCancellationRequested)_busy=false;}
        }
        private async Task<JsonValue> Get(string path,CancellationToken token) {var value=await _api.Request("GET",path,null,token);token.ThrowIfCancellationRequested();return value;}
        private async Task Load(string page,CancellationToken token) {
            switch(page) {
                case "Play":
                    Button("Learn to play · guided tutorial",()=>{MatchLaunch.Tutorial();OpenGame();return Task.CompletedTask;});
                    var active=(await Get("/v1/session/active",token)).Get("active");
                    if(active!=null&&active.Type!=JsonValue.Kind.Null) {Text("Your table "+active.Str("code")+" is ready to resume.");Button("Resume match",()=>{MatchLaunch.Resume();OpenGame();return Task.CompletedTask;});}
                    Button("Casual · find an opponent",()=>{LaunchQueue("casual");return Task.CompletedTask;});
                    Button("Ranked · competitive match",()=>{LaunchQueue("ranked");return Task.CompletedTask;});
                    Button("Private table with friends",()=>{Show("Private table");return Task.CompletedTask;});
                    Button("Practice on this device",()=>{LaunchLocal();return Task.CompletedTask;});break;
                case "Profile":
                    await _api.RefreshProfile(token);token.ThrowIfCancellationRequested();var p=_api.Profile;
                    Text(p.Str("name")+"\nLevel "+p.Int("level",1)+"  ·  "+p.Int("xp",0)+" XP\n"+p.Int("games",0)+" public games  ·  "+p.Int("wins",0)+" wins",170);
                    Text("Player ID\n"+p.Str("id"),100);Button("Copy player ID",()=>{GUIUtility.systemCopyBuffer=p.Str("id");_status.text="Player ID copied.";return Task.CompletedTask;});
                    var name=Input("Player name",16);name.text=p.Str("name");Button("Save name",async()=>{await _api.Request("PATCH","/v1/profile",JsonValue.FromObject(("name",JsonValue.FromString(name.text))),token);_status.text="Name saved.";});
                    Nav("Account","Account");Nav("Match history","History");Nav("Friends","Friends");break;
                case "Account":
                    Text("Guest progress is saved on the server. Link Google to recover your account on another device.",150);
                    var capabilities=await Get("/v1/status",token);
                    Button("Continue with Google",()=>Link("google",token),capabilities.Flag("google"));
                    if(!capabilities.Flag("google"))Text("Provider sign-in will become available when the game's registered app credentials are configured.",140);
                    Button("Sign out",()=>{Show("Sign out");return Task.CompletedTask;});Nav("Sign in to an existing account","Sign in");Nav("Privacy and support","Privacy and support");Nav("Delete account","Delete account");break;
                case "Sign in":
                    Text("Signing in replaces the account on this device. Link your current guest to Google first if you want to keep its progress.",170);
                    var signInCapabilities=await Get("/v1/status",token);
                    Button("Sign in with Google",()=>Link("google",token,false),signInCapabilities.Flag("google"));break;
                case "Sign out":
                    Text("Before signing out, link your account to Google. An unlinked guest account cannot be recovered after its credentials are removed.",180);
                    Button("Confirm sign out",async()=>{await _api.SignOut();Close();GetComponent<LoginScreen>().Show();});break;
                case "Privacy and support":
                    Text("Publisher: Fiesta games\nSupport and privacy: gaurav5130sharma@gmail.com");
                    Text("For account access, deletion or a player-safety concern, contact support with your player ID. Never send passwords, sign-in tokens or full payment details.");
                    Button("Email support",()=>{Application.OpenURL("mailto:gaurav5130sharma@gmail.com");return Task.CompletedTask;});
                    Button("Deletion instructions in browser",()=>{Application.OpenURL(_api.BaseUrl.TrimEnd('/')+"/support/account-deletion");return Task.CompletedTask;});
                    Text("Your data export contains personal account information. Copy it only on a trusted device, and clear the clipboard after saving it securely.");
                    Button("Copy my data export",async()=>{var export=await Get("/v1/account/export",token);GUIUtility.systemCopyBuffer=export.Get("data").Encode();_status.text="Personal data copied. Clear your clipboard after saving it.";});
                    Nav("Delete account","Delete account");break;
                case "Delete account":
                    Text("Permanently delete your profile, friends and sign-in sessions. Purchase records may be retained for required store reconciliation. Leave your active table first. This cannot be undone.",240);
                    var confirm=Input("Type DELETE to confirm",6);Button("Permanently delete account",async()=>{if(confirm.text!="DELETE")throw new InvalidOperationException("Type DELETE to confirm.");await _api.Request("DELETE","/v1/account",null,token);_api.Forget();Close();GetComponent<LoginScreen>().Show();});break;
                case "Private table":
                    Text("Create a table and share its code. All players must be ready before the host starts.",130);
                    Button("Host a private table",()=>{LaunchPrivate(null);return Task.CompletedTask;});
                    var code=Input("Enter table code",8);Button("Join table",()=>{var value=code.text.Trim().ToUpperInvariant();if(value.Length<4||value.Any(c=>!char.IsLetterOrDigit(c)))throw new InvalidOperationException("Enter a valid table code.");LaunchPrivate(value);return Task.CompletedTask;});Nav("Friends","Friends");break;
                case "Friends":
                    var friends=(await Get("/v1/friends",token)).Arr("friends")??new List<JsonValue>();
                    if(friends.Count==0)Text("Your friends list is empty. Add a friend using their player ID, then share a private table code.",160);
                    foreach(var f in friends) {Text(f.Str("name")+"\n"+(f.Flag("accepted")?"Friend":f.Flag("incoming")?"Incoming request":"Request sent"),100);var path="/v1/friends/"+Uri.EscapeDataString(f.Str("id"));if(!f.Flag("accepted")&&f.Flag("incoming"))Button("Accept",async()=>{await _api.Request("PATCH",path,JsonValue.FromObject(),token);Show("Friends");});Button(f.Flag("accepted")?"Remove friend":"Cancel request",async()=>{await _api.Request("DELETE",path,null,token);Show("Friends");});}
                    var friendId=Input("Friend's full player ID",36);Button("Add friend",async()=>{await _api.Request("POST","/v1/friends",JsonValue.FromObject(("accountId",JsonValue.FromString(friendId.text.Trim()))),token);Show("Friends");});Nav("Private table","Private table");break;
                case "Ranked":
                    await _api.RefreshProfile(token);token.ThrowIfCancellationRequested();Text("Rating  "+_api.Profile.Int("rating",1000)+"\nRanked matches use public human opponents. Private tables and practice games do not change rating.",180);Button("Find ranked match",()=>{LaunchQueue("ranked");return Task.CompletedTask;});Nav("Leaderboard","Leaderboard");break;
                case "Leaderboard":
                    var players=(await Get("/v1/leaderboard",token)).Arr("players")??new List<JsonValue>();
                    if(players.Count==0)Text("No ranked results yet. Complete a ranked match to appear here.");int rank=1;foreach(var player in players)Text((rank++)+".  "+player.Str("name")+"  ·  "+player.Int("rating",1000),70);break;
                case "History":
                    var matches=(await Get("/v1/matches",token)).Arr("matches")??new List<JsonValue>();if(matches.Count==0)Text("No online match history yet.");
                    foreach(var match in matches)Text((match.Str("winner")==_api.AccountId?"VICTORY":"MATCH FINISHED")+"  ·  "+match.Str("mode")+"\n"+match.Str("endedAt"),120);break;
                case "Wallet":
                    await _api.RefreshProfile(token);token.ThrowIfCancellationRequested();Text("Coins  "+_api.Profile.Int("coins",0)+"\nGems  "+_api.Profile.Int("gems",0),100);
                    var ledger=(await Get("/v1/wallet/ledger",token)).Arr("ledger")??new List<JsonValue>();if(ledger.Count==0)Text("No transactions yet. Earn coins by completing daily missions.");foreach(var row in ledger)Text(row.Int("delta",0)+" "+row.Str("currency")+"\n"+row.Str("reason"),100);Nav("Shop","Shop");break;
                case "Missions":
                    var missions=(await Get("/v1/missions",token)).Arr("missions")??new List<JsonValue>();Text("Daily missions reset at 00:00 UTC. Public matches with active human players count; practice and private matches do not.",150);
                    foreach(var mission in missions) {Text(mission.Str("title")+"\n"+Mathf.Min(mission.Int("progress",0),mission.Int("target",1))+" / "+mission.Int("target",1)+"  ·  "+mission.Int("coins",0)+" coins",110);Button(mission.Flag("claimed")?"Claimed":"Claim reward",async()=>{await _api.Request("POST","/v1/missions/"+mission.Str("id")+"/claim",JsonValue.FromObject(),token);Show("Missions");},!mission.Flag("claimed")&&mission.Int("progress",0)>=mission.Int("target",1));}break;
                case "Collection":
                    Text("THE FIVE ROLES\nAll five roles are available in every game.",110);
                    foreach(Role role in Enum.GetValues(typeof(Role))) {var art=_art?.For(role);if(art==null)continue;var row=Row(role.ToString(),280);UIFactory.Image("Card",row,art.cardFace,new Vector2(165,260),new Vector2(-300,0));var text=UIFactory.Text("Body",row,art.displayName+"\n\n"+art.ability,29,new Vector2(540,250),new Vector2(100,0),TextAlignmentOptions.MidlineLeft);text.richText=false;}break;
                case "Shop":
                    var products=(await Get("/v1/catalog",token)).Arr("products")??new List<JsonValue>();
                    _store=Storefront.Instance;if(_storeStatus==null){_store.Changed+=OnStoreChanged;_storeStatus="";}
                    Text("Only your store can confirm prices and authorize payment. Currency is granted after server verification.",140);
                    if(products.Count==0)Text("No offers are currently available. No payment can be made.",120);
                    await _store.Initialize(products);token.ThrowIfCancellationRequested();_status.text=_store.Status;
                    foreach(var product in _store.Products)Button(product.metadata.localizedTitle+" · "+product.metadata.localizedPriceString,()=>{_store.Buy(product);return Task.CompletedTask;},product.availableToPurchase&&!_store.Busy);
                    Button("Restore / retry pending purchases",()=>{_store.Restore();return Task.CompletedTask;},_store.Products.Count>0);Nav("Wallet history","Wallet");break;
                case "Season": case "Event":
                    var content=await Get("/v1/content",token);var item=content.Get(page=="Season"?"season":"event");
                    if(item==null||item.Type==JsonValue.Kind.Null)Text(page=="Season"?"No season is active. Rewards and paid passes are unavailable until a season is announced.":"No event is active. Check back for the next Crown of Nations event.",190);
                    else Text(item.Str("title")+"\n\n"+item.Str("description"),320);break;
                case "Settings":
                    Button(Sfx.Muted?"Sound: OFF":"Sound: ON",()=>{Sfx.Muted=!Sfx.Muted;Show("Settings");return Task.CompletedTask;});
                    Button(Sfx.MusicMuted?"Music: OFF":"Music: ON",()=>{Sfx.MusicMuted=!Sfx.MusicMuted;Show("Settings");return Task.CompletedTask;},!Sfx.Muted);
                    Nav("Account and privacy","Account");Nav("Privacy and support","Privacy and support");Nav("Friends","Friends");Nav("Match history","History");Nav("Rules","Rules");Nav("Connection status","Connection");break;
                case "Connection":
                    await Get("/v1/status",token);Text("Connected to the game server. Your progress and online matches are stored on the server.",150);Button("Refresh connection",()=>{Show("Connection");return Task.CompletedTask;});break;
                case "Rules":
                    Text("Be the last player with influence. Begin with two hidden role cards and two coins (the first player starts with one coin in a two-player game).\n\nIncome gives 1 coin. Foreign Aid gives 2 and can be blocked. A Coup costs 7 and cannot be blocked or challenged. At 10 coins you must Coup.\n\nChallenge a role claim if you suspect a bluff. A failed challenge costs influence; a successful one exposes the bluffer. Lose both cards and you are out.\n\nOnline actions have a time limit. A missed decision passes, takes Income (or a forced Coup), or chooses a legal card. Reconnecting restores the authoritative table.",800);Nav("The five roles","Collection");break;
            }
        }
        private void OnStoreChanged() {if(this!=null && _page=="Shop" && IsOpen)_status.text=_store.Status;}
        private void Nav(string label,string page) => Button(label,()=>{Show(page);return Task.CompletedTask;});
        private async Task Link(string provider,CancellationToken token,bool link=true) {
            var begin=await _api.Request("POST","/v1/auth/browser/start",JsonValue.FromObject(("provider",JsonValue.FromString(provider)),("link",JsonValue.FromBool(link))),token);
            Application.OpenURL(begin.Str("url"));_status.text="Finish signing in in your browser, then return to Coup.";
            for(int i=0;i<90;i++) {await Task.Delay(3000,token);var result=await _api.PublicRequest("/v1/auth/browser/poll",JsonValue.FromObject(("state",JsonValue.FromString(begin.Str("state"))),("pollToken",JsonValue.FromString(begin.Str("pollToken")))),token);if(!result.Flag("pending")){_api.AcceptSession(result);Show("Profile");return;}}
            throw new InvalidOperationException("Sign-in timed out. Please try again.");
        }
        private async void LaunchQueue(string mode) {if(_launching)return;_launching=true;try {await _api.AccessToken();if(this==null)return;MatchLaunch.Queue(mode);OpenGame();}catch(Exception){_launching=false;if(this!=null)Show("Connection");}}
        private async void LaunchPrivate(string code) {if(_launching)return;_launching=true;try {await _api.AccessToken();if(this==null)return;if(code==null)MatchLaunch.Host();else MatchLaunch.Join(code);OpenGame();}catch(Exception){_launching=false;if(this!=null)Show("Connection");}}
        private static void LaunchLocal() {MatchLaunch.Local();OpenGame();}
        private static void OpenGame() => ScreenTransition.Load(MatchLaunch.GameScene);
    }
}
