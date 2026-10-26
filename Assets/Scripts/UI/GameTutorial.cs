using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Coup.Core;

namespace Coup.Game.UI
{
    /// <summary>Illustrated, replayable tour. Examples never submit live match actions.</summary>
    public sealed class GameTutorial : MonoBehaviour
    {
        static readonly string[] Titles={"KEEP YOUR INFLUENCE","YOUR CARDS & ACTIONS","COINS & ACTIONS","CHOOSE AN OPPONENT","CALL BLUFF OR LET IT","BLOCKS & YOUR FIRST MATCH"};
        static readonly string[] Subtitles={"Two cards. One survivor.","Learn the connection","Know what each action costs","Tap a glowing opponent to target them","Decide whether to challenge","Defend your influence"};
        static readonly string[] Bodies={
            "Your hidden cards are your <color=#EBD080>influence</color>. Lose both and you are out.\n\nBe the last player with a hidden card to win.",
            "<color=#EBD080>Glowing actions</color> match your hidden cards.\n\nYou can also <color=#EBD080>bluff</color> with other roles — but opponents can <color=#EBD080>challenge</color> you.",
            "At <color=#EBD080>10 coins</color>, you must Coup.\n\nForeign Aid can be blocked by Duke. Tax claims Duke. Hold an action in the game to read its details.",
            "After Assassinate, Steal or Coup, <color=#EBD080>choose a target</color>. Tap a glowing profile or a large opponent name button.\n\nCancel returns to your actions without spending coins.",
            "If they prove the role, <color=#EBD080>you lose a card</color> and they draw a replacement. If they cannot, <color=#EBD080>they lose a card</color>.\n\nLet It means no challenge; it does not block the action.",
            "Blocks can also be <color=#EBD080>bluffs</color> and can be challenged.\n\nTry a practice match. Replay this guide anytime from <color=#EBD080>Rules → Tutorial</color>."};
        // Backdrop.png has its gold frame painted in. These are its measured edges as fractions of the
        // image from the top-left, so the guide can sit inside the frame at any screen aspect.
        static readonly Rect FrameInterior=Rect.MinMaxRect(135/941f,111/1672f,806/941f,1612/1672f);
        const float CornerTop=228/1672f,CornerBottom=1500/1672f;
        const float SheetWidth=900,IllustrationHeight=830;
        TableView _table;
        TutorialTheme _theme;
        Texture2D _atlas;
        Sprite _headingArt,_plaqueArt,_buttonArt;
        readonly System.Collections.Generic.List<Sprite> _ownedSprites=new System.Collections.Generic.List<Sprite>();
        readonly System.Collections.Generic.Dictionary<int,Sprite> _marble=new System.Collections.Generic.Dictionary<int,Sprite>();
        Sprite Atlas(Texture2D texture,int x,int y,int width,int height,Vector4 border)
        {
            // Measured in pixels on the 1254px source, from the top-left; border is left, bottom, right, top.
            float k=texture.width/1254f;
            var rect=new Rect(x*k,texture.height-(y+height)*k,width*k,height*k);
            var sprite=Sprite.Create(texture,rect,new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,border*k);
            _ownedSprites.Add(sprite);return sprite;
        }
        /// <summary>Plain marble from inside the atlas's tall tile, cropped to the panel's shape so it is never stretched.</summary>
        Sprite Marble(float aspect)
        {
            int key=Mathf.RoundToInt(aspect*40);
            if(_marble.TryGetValue(key,out var sprite))return sprite;
            float width=285,height=340;
            if(aspect>width/height)height=width/aspect;else width=height*aspect;
            sprite=Atlas(_atlas,Mathf.RoundToInt(312-width/2),Mathf.RoundToInt(880-height/2),Mathf.RoundToInt(width),Mathf.RoundToInt(height),Vector4.zero);
            _marble[key]=sprite;return sprite;
        }
        RectTransform _root,_sheet,_heading,_explanation,_visual,_skip,_nextButton;
        CanvasGroup _backGroup;
        Image _backdrop;
        TMP_Text _title,_subtitle,_body,_progress,_next;
        Button _back;
        Image[] _dots;
        float _explanationHeight;
        int _step;
        float _oldTime;
        bool _paused;
        readonly System.Collections.Generic.List<Image> _targetRims = new System.Collections.Generic.List<Image>();
        public static void Open(TableView table)
        {
            var tutorial=table.GetComponent<GameTutorial>();
            if(tutorial==null)tutorial=table.gameObject.AddComponent<GameTutorial>();
            tutorial.Begin(table);
        }
        void Begin(TableView table)
        {
            if(_root!=null&&_root.gameObject.activeSelf)return;
            _table=table;
            if(_root==null)Build();
            _step=0;_root.gameObject.SetActive(true);_root.SetAsLastSibling();
            if(!table.match.playOnline){_oldTime=Time.timeScale;Time.timeScale=0;_paused=true;}
            Fit();Draw();
        }
        void Build()
        {
            _theme=Resources.Load<TutorialTheme>("Tutorial/Theme");
            _atlas=Resources.Load<Texture2D>("Tutorial/Ornaments");
            if(_atlas!=null)
            {
                _headingArt=Atlas(_atlas,10,239,661,157,new Vector4(150,60,150,60));
                _plaqueArt=Atlas(_atlas,691,234,549,165,new Vector4(60,58,60,58));
                _buttonArt=Atlas(_atlas,610,829,628,160,new Vector4(100,70,100,70));
            }
            _root=UIFactory.Rect("GuidedTutorial",transform,Vector2.zero,Vector2.zero);
            _root.anchorMin=Vector2.zero;_root.anchorMax=Vector2.one;_root.offsetMin=_root.offsetMax=Vector2.zero;
            _backdrop=UIFactory.FullScreen("Backdrop",_root,Resources.Load<Sprite>("Tutorial/Backdrop"));
            _backdrop.color=_backdrop.sprite!=null?Color.white:new Color(.015f,.025f,.045f,1);
            _backdrop.raycastTarget=true;
            _sheet=UIFactory.Rect("Guide",_root,new Vector2(SheetWidth,1740),Vector2.zero);
            _heading=UIFactory.Image("Heading",_sheet,_headingArt!=null?_headingArt:_table.art.roundedPanel,new Vector2(SheetWidth,180),Vector2.zero,false).rectTransform;
            var headingArt=_heading.GetComponent<Image>();headingArt.type=Image.Type.Sliced;
            if(_headingArt!=null)headingArt.pixelsPerUnitMultiplier=_headingArt.rect.height/180;else headingArt.color=UIFactory.Navy;
            _title=Label("Title",_heading,"",43,new Vector2(680,60),new Vector2(0,26));_title.color=UIFactory.Gold;PanelTheme.Title(_title);
            _subtitle=Label("Subtitle",_heading,"",30,new Vector2(680,46),new Vector2(0,-30));
            _visual=UIFactory.Rect("Illustration",_sheet,new Vector2(SheetWidth,IllustrationHeight),Vector2.zero);
            // One plaque height for every page, sized to the longest explanation, so nothing jumps.
            _body=Label("Body",_sheet,"",38,new Vector2(SheetWidth-150,220),Vector2.zero);
            float tallest=0;
            foreach(var body in Bodies)tallest=Mathf.Max(tallest,_body.GetPreferredValues(body,SheetWidth-150,0).y);
            _explanationHeight=Mathf.Clamp(tallest+100,260,480);
            _explanation=Plaque("Explanation",_sheet,new Vector2(SheetWidth,_explanationHeight),Vector2.zero,200);
            PanelAura.Attach(_explanation,_table.art,UIFactory.Gold,16);
            _body.rectTransform.SetParent(_explanation,false);_body.rectTransform.anchoredPosition=Vector2.zero;
            _body.rectTransform.sizeDelta=new Vector2(SheetWidth-150,_explanationHeight-84);
            _progress=Label("Progress",_sheet,"",23,new Vector2(SheetWidth-40,38),Vector2.zero);_progress.color=UIFactory.Gold;
            _dots=new Image[Titles.Length];
            for(int i=0;i<_dots.Length;i++)_dots[i]=UIFactory.Image("Step"+i,_sheet,_table.art.vfxSpark,new Vector2(18,18),Vector2.zero);
            _back=MakeButton("Back","‹  BACK",new Vector2(230,120),Vector2.zero,()=>{_step--;Draw();});
            _backGroup=_back.gameObject.AddComponent<CanvasGroup>();
            var next=MakeButton("Next","NEXT  ›",new Vector2(SheetWidth-260,120),Vector2.zero,()=>{if(_step==Titles.Length-1)Close();else{_step++;Draw();}},true);
            _next=next.GetComponentInChildren<TMP_Text>();_nextButton=(RectTransform)next.transform;
            _skip=(RectTransform)MakeButton("Skip","SKIP  ×",new Vector2(180,70),Vector2.zero,Close).transform;
        }
        TMP_Text Label(string name,Transform parent,string text,float size,Vector2 bounds,Vector2 at)
        {
            var t=UIFactory.Text(name,parent,text,size,bounds,at);
            if(_theme!=null)t.font=name=="Body"||name=="Subtitle"||name=="Caption"?_theme.bodyFont:_theme.headingFont;t.enableAutoSizing=true;t.fontSizeMin=size-4;t.fontSizeMax=size;return t;
        }
        RectTransform Plaque(string name,Transform parent,Vector2 size,Vector2 at,float artHeight)
        {
            var rt=UIFactory.Rect(name,parent,size,at);
            Dress(rt,size,artHeight);return rt;
        }
        /// <summary>
        /// The navy plaque, drawn as though its art were artHeight tall so the corners keep their shape.
        /// Its own centre is a thin strip that streaks when stretched, so the marble is a separate
        /// unstretched crop under a 9-sliced frame. Returns the frame.
        /// </summary>
        Image Dress(RectTransform host,Vector2 size,float artHeight)
        {
            if(_plaqueArt==null)
            {
                var plain=UIFactory.Image("Frame",host,_table.art.roundedPanel,size,Vector2.zero,false);
                plain.type=Image.Type.Sliced;plain.color=UIFactory.Navy;return plain;
            }
            var inner=size-Vector2.one*(48*artHeight/165);
            UIFactory.Image("Marble",host,Marble(inner.x/inner.y),inner,Vector2.zero,false);
            var frame=UIFactory.Image("Frame",host,_plaqueArt,size,Vector2.zero,false);
            frame.type=Image.Type.Sliced;frame.fillCenter=false;frame.pixelsPerUnitMultiplier=_plaqueArt.rect.height/artHeight;
            return frame;
        }
        RectTransform Panel(string name,Transform parent,Vector2 size,Vector2 at,Color accent)
        {
            var rt=UIFactory.Rect(name,parent,size,at);
            if(accent!=UIFactory.Gold)
            {
                // Outside the gold frame, like the card glows, so it never crosses the corner flourishes.
                var halo=UIFactory.Image("Accent",rt,_table.art.roundedBorder,size+new Vector2(16,16),Vector2.zero,false);
                halo.type=Image.Type.Sliced;halo.color=new Color(accent.r,accent.g,accent.b,.85f);
            }
            Dress(rt,size,170);return rt;
        }
        Button MakeButton(string name,string text,Vector2 size,Vector2 at,UnityEngine.Events.UnityAction click,bool primary=false,Transform parent=null)
        {
            var button=UIFactory.Button(name,parent!=null?parent:_sheet,primary?_buttonArt:null,size,at);
            var fill=button.GetComponent<Image>();fill.preserveAspect=false;
            if(!primary){fill.color=Color.clear;button.targetGraphic=Dress((RectTransform)button.transform,size,Mathf.Min(size.y,120));}
            else if(_buttonArt!=null){fill.color=Color.white;fill.type=Image.Type.Sliced;fill.pixelsPerUnitMultiplier=_buttonArt.rect.height/size.y;}
            var rim=UIFactory.Image("Border",button.transform,_table.art.roundedBorder,size,Vector2.zero,false);rim.type=Image.Type.Sliced;rim.color=new Color(1,.8f,.3f,0);
            var textView=Label("Label",button.transform,text,29,size-new Vector2(48,16),Vector2.zero);if(primary)textView.color=new Color(.04f,.025f,.01f);
            button.onClick.AddListener(click);return button;
        }
        void Card(Role role,float x,float y,float width,bool hidden=false)
        {
            if(hidden)
            {
                UIFactory.Image("HiddenInfluence",_visual,_table.art.cardBack,new Vector2(width,width*1.47f),new Vector2(x,y));
                return;
            }
            var size=new Vector2(width,width*1.5f);
            var glow=UIFactory.Image("CardGlow",_visual,_table.art.roundedBorder,size+new Vector2(10,10),new Vector2(x,y),false);
            glow.type=Image.Type.Sliced;glow.color=Color.Lerp(_table.art.Accent(role),Color.white,.45f);
            UIFactory.Image("Example_"+role,_visual,_table.art.CardFace(role),size,new Vector2(x,y));
        }

        void Tile(ActionKind action,string title,string detail,float x,float y,Color color,float width=360,float height=270)
        {
            var tile=Panel(title,_visual,new Vector2(width,height),new Vector2(x,y),color);
            float icon=Mathf.Min(100,height*.38f),iconY=height/2-24-icon/2,titleY=iconY-icon/2-28;
            float detailTop=titleY-23,detailBottom=-height/2+36;
            UIFactory.Image("Icon",tile,_table.art.For(action)?.icon,new Vector2(icon,icon),new Vector2(0,iconY));
            Label("Action",tile,title,32,new Vector2(width-60,42),new Vector2(0,titleY)).color=color;
            Label("Body",tile,detail,30,new Vector2(width-60,detailTop-detailBottom),new Vector2(0,(detailTop+detailBottom)/2));
        }
        void Link(float x,float top,float bottom,Color color)
        {
            for(int i=0;i<18;i++)
            {
                float t=i/17f;
                var point=new Vector2(x+Mathf.Sin(t*Mathf.PI*2)*20,Mathf.Lerp(top,bottom,t));
                var light=UIFactory.Image("Connection",_visual,_table.art.vfxSpark,new Vector2(34,34),point);
                light.color=new Color(color.r,color.g,color.b,.7f);
                var core=UIFactory.Image("Core",_visual,_table.art.vfxSpark,new Vector2(10,10),point);core.color=Color.white;
            }
        }

        // Each page is drawn in a 900x830 box centered on the illustration area, which Fit scales uniformly.
        void Draw()
        {
            _targetRims.Clear();
            foreach(Transform child in _visual){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            _title.text=Titles[_step];_subtitle.text=Subtitles[_step];_body.text=Bodies[_step];
            _progress.text=(_step+1)+" / "+Titles.Length+(_table.match.playOnline?"   ·   LIVE MATCH CONTINUES":"   ·   PRACTICE PAUSED");
            _back.interactable=_step>0;_backGroup.alpha=_step>0?1:.45f;_next.text=_step==Titles.Length-1?"START PLAYING  ›":"NEXT  ›";
            for(int i=0;i<_dots.Length;i++)_dots[i].color=i==_step?UIFactory.Gold:new Color(.35f,.4f,.48f);
            var purple=UIFactory.Readable(_table.art.Accent(Role.Duke));var amber=UIFactory.Readable(_table.art.Accent(Role.Ambassador));
            switch(_step)
            {
                case 0:
                    Card(Role.Duke,-215,120,330,true);Card(Role.Ambassador,215,120,330,true);
                    for(int i=0;i<2;i++)
                    {
                        var diamond=UIFactory.Rect("InfluenceDiamond",_visual,new Vector2(30,30),new Vector2(i==0?-65:65,-195));
                        diamond.localRotation=Quaternion.Euler(0,0,45);
                        var symbol=diamond.gameObject.AddComponent<Image>();symbol.color=UIFactory.Gold;symbol.raycastTarget=false;
                    }
                    Label("Caption",_visual,"2 HIDDEN CARDS = 2 INFLUENCE",34,new Vector2(850,70),new Vector2(0,-285));break;
                case 1:
                    Card(Role.Duke,-220,190,290);Card(Role.Ambassador,220,190,290);
                    Link(-220,-40,-118,purple);Link(220,-40,-118,amber);
                    Tile(ActionKind.Tax,"TAX","Take 3 coins",-220,-270,purple,380,290);
                    Tile(ActionKind.Exchange,"EXCHANGE","Draw 2 cards.\nChoose which to keep.",220,-270,amber,380,290);break;
                case 2:
                    Tile(ActionKind.Income,"INCOME","Get 1 coin",-225,285,UIFactory.Gold,420,250);
                    Tile(ActionKind.ForeignAid,"FOREIGN AID","Get 2 coins",225,285,UIFactory.Gold,420,250);
                    Tile(ActionKind.Tax,"TAX","Get 3 coins",-225,0,purple,420,250);
                    Tile(ActionKind.Assassinate,"ASSASSINATE","Pay 3 coins",225,0,UIFactory.Gold,420,250);
                    Tile(ActionKind.Coup,"COUP","Pay 7 coins",0,-285,UIFactory.Gold,420,250);break;
                case 3:
                    Tile(ActionKind.Assassinate,"ASSASSINATE","1. Choose the action",0,270,UIFactory.Gold,460,260);
                    Label("Arrow",_visual,"↓",65,new Vector2(100,80),new Vector2(0,92)).color=UIFactory.Gold;
                    for(int i=0;i<3;i++)
                    {
                        var role=i==0?Role.Captain:i==1?Role.Duke:Role.Ambassador;
                        float x=(i-1)*300;
                        var target=MakeButton("DemoTarget"+i,"TAP TO SELECT",new Vector2(270,250),new Vector2(x,-75),()=>_subtitle.text="Target selected — now you know where to tap!",false,_visual);
                        var text=target.GetComponentInChildren<TMP_Text>();text.rectTransform.anchoredPosition=new Vector2(0,-80);text.rectTransform.sizeDelta=new Vector2(230,50);text.fontSizeMax=25;text.fontSizeMin=21;
                        var seats=_table.GetComponentsInChildren<PlayerPanelView>();
                        var source=seats.Length>i?seats[i].transform.Find("PortraitMask"):null;
                        if(source!=null)
                        {
                            var avatar=Instantiate(source,target.transform) as RectTransform;
                            avatar.anchorMin=avatar.anchorMax=avatar.pivot=new Vector2(.5f,.5f);
                            avatar.anchoredPosition=new Vector2(0,32);avatar.localScale=Vector3.one*(130f/Mathf.Max(avatar.rect.width,1));
                            foreach(var graphic in avatar.GetComponentsInChildren<Graphic>())graphic.raycastTarget=false;
                        }
                        else UIFactory.Image("Portrait",target.transform,_table.art.Portrait(role),new Vector2(120,135),new Vector2(0,32));
                        _targetRims.Add(target.transform.Find("Border").GetComponent<Image>());
                    }
                    Label("Caption",_visual,"2. Tap an opponent above\nThis is a tutorial example.",32,new Vector2(850,100),new Vector2(0,-300));break;
                case 4:
                    Card(Role.Duke,0,190,280);
                    Label("Claim",_visual,"“I take 3 coins as Duke.”",35,new Vector2(850,65),new Vector2(0,-82));
                    var challenge=Panel("Challenge",_visual,new Vector2(420,170),new Vector2(-225,-300),new Color(1,.3f,.28f));
                    Label("Caption",challenge,"CALL BLUFF\nChallenge their role",30,new Vector2(360,120),Vector2.zero);
                    var allow=Panel("Allow",_visual,new Vector2(420,170),new Vector2(225,-300),new Color(.15f,.75f,1));
                    Label("Caption",allow,"LET IT\nDo not challenge",30,new Vector2(360,120),Vector2.zero);break;
                case 5:
                    Card(Role.Contessa,-300,200,240);Card(Role.Captain,0,200,240);Card(Role.Duke,300,200,240);
                    Label("Caption",_visual,"CONTESSA\nBlocks\nassassination",28,new Vector2(280,140),new Vector2(-300,-75));
                    Label("Caption",_visual,"CAPTAIN\nor AMBASSADOR\nBlocks stealing",28,new Vector2(280,140),new Vector2(0,-75));
                    Label("Caption",_visual,"DUKE\nBlocks\nForeign Aid",28,new Vector2(280,140),new Vector2(300,-75));
                    Label("Ready",_visual,"YOU ARE READY FOR THE COURT",35,new Vector2(880,80),new Vector2(0,-300)).color=UIFactory.Gold;break;
            }
        }
        void LateUpdate()
        {
            if(_root==null||!_root.gameObject.activeSelf)return;
            Fit();
            var pulse=PlayerPrefs.GetInt("Coup.ReducedMotion",0)!=0?0f:(Mathf.Sin(Time.unscaledTime*3)+1)*.3f;
            foreach(var rim in _targetRims)if(rim!=null)rim.color=Color.Lerp(UIFactory.Gold,Color.white,pulse);
        }
        void Fit()
        {
            var size=_root.rect.size;
            // Cover the screen without distorting the backdrop, then find where its painted frame landed.
            var frame=new Rect(-size.x/2,-size.y/2,size.x,size.y);
            float cornerTop=frame.yMax,cornerBottom=frame.yMin;
            var art=_backdrop.sprite;
            if(art!=null)
            {
                var drawn=art.rect.size*Mathf.Max(size.x/art.rect.width,size.y/art.rect.height);
                var backdrop=_backdrop.rectTransform;
                backdrop.anchorMin=backdrop.anchorMax=new Vector2(.5f,.5f);backdrop.sizeDelta=drawn;backdrop.anchoredPosition=Vector2.zero;
                frame=Rect.MinMaxRect((FrameInterior.xMin-.5f)*drawn.x,(.5f-FrameInterior.yMax)*drawn.y,(FrameInterior.xMax-.5f)*drawn.x,(.5f-FrameInterior.yMin)*drawn.y);
                cornerTop=(.5f-CornerTop)*drawn.y;cornerBottom=(.5f-CornerBottom)*drawn.y;
            }
            var safe=Screen.safeArea;
            float left=Mathf.Max(frame.xMin,(safe.xMin/Screen.width-.5f)*size.x,-size.x/2)+18;
            float right=Mathf.Min(frame.xMax,(safe.xMax/Screen.width-.5f)*size.x,size.x/2)-18;
            float top=Mathf.Min(frame.yMax,(safe.yMax/Screen.height-.5f)*size.y,size.y/2);
            float bottom=Mathf.Max(frame.yMin,(safe.yMin/Screen.height-.5f)*size.y,-size.y/2);
            float scale=(right-left)/SheetWidth,half=(top-bottom)/scale/2;
            _sheet.sizeDelta=new Vector2(SheetWidth,half*2);_sheet.localScale=Vector3.one*scale;
            _sheet.anchoredPosition=new Vector2((left+right)/2,(top+bottom)/2);
            // The frame's corner flourishes reach into the interior; keep content clear of them.
            // The band under the crown holds Skip, between the crest and the right-hand flourish.
            float topBand=Mathf.Max((top-cornerTop)/scale,100),bottomBand=Mathf.Max((cornerBottom-bottom)/scale,24);
            _skip.anchoredPosition=new Vector2(SheetWidth/2-150,half-topBand/2);
            float headingY=half-topBand-94;
            _heading.anchoredPosition=new Vector2(0,headingY);
            float navY=-half+bottomBand+66;
            _back.GetComponent<RectTransform>().anchoredPosition=new Vector2(-SheetWidth/2+115,navY);
            _nextButton.anchoredPosition=new Vector2(130,navY);
            float progressY=navY+98;
            _progress.rectTransform.anchoredPosition=new Vector2(0,progressY);
            for(int i=0;i<_dots.Length;i++)_dots[i].rectTransform.anchoredPosition=new Vector2((i-2.5f)*38,progressY+40);
            float explanationY=progressY+74+_explanationHeight/2;
            _explanation.anchoredPosition=new Vector2(0,explanationY);
            float illustrationTop=headingY-120,illustrationBottom=explanationY+_explanationHeight/2+30;
            _visual.localScale=Vector3.one*Mathf.Min(1,(illustrationTop-illustrationBottom)/IllustrationHeight);
            _visual.anchoredPosition=new Vector2(0,(illustrationTop+illustrationBottom)/2);
        }
        void Close()
        {
            _root.gameObject.SetActive(false);
            if(_paused){Time.timeScale=_oldTime;_paused=false;}
            PlayerPrefs.SetInt("Coup.TutorialSeen",1);PlayerPrefs.Save();
        }
        void OnDestroy(){if(_paused)Time.timeScale=_oldTime;foreach(var sprite in _ownedSprites)if(sprite!=null)Destroy(sprite);}
    }
}
