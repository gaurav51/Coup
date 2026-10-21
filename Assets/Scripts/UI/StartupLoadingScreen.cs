using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Coup.Game.UI
{
    /// <summary>First-launch curtain. Its indicator is indeterminate, not a fake percentage.</summary>
    public sealed class StartupLoadingScreen : MonoBehaviour
    {
        private GameObject _root;
        private RectTransform _spark;
        private TMP_Text _status;
        private CanvasGroup _group;
        private float _began;
        public bool IsVisible => _root!=null && _root.activeSelf;
        public void Show(CoupArt art) {
            _began=Time.realtimeSinceStartup;
            var root=UIFactory.Rect("StartupLoading",transform,Vector2.zero,Vector2.zero);
            root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
            _root=root.gameObject;
            var curtain=_root.AddComponent<Image>();curtain.color=new Color(.012f,.024f,.045f,1f);
            _group=_root.AddComponent<CanvasGroup>();
            var canvas=GetComponentInParent<Canvas>().transform as RectTransform;
            var composition=UIFactory.Rect("Composition",root,new Vector2(1080,1920),Vector2.zero);
            composition.localScale=Vector3.one*Mathf.Min(1,canvas.rect.width/1080f,canvas.rect.height/1920f);
            UIFactory.Text("Overline",composition,"THE COURT AWAITS",26,new Vector2(800,70),new Vector2(0,520)).color=UIFactory.Gold;
            UIFactory.Text("Title",composition,"COUP",132,new Vector2(900,180),new Vector2(0,380));
            UIFactory.Text("Subtitle",composition,"VEILED COURT",38,new Vector2(800,65),new Vector2(0,265)).color=UIFactory.Gold;
            for(int i=0;i<3;i++) {
                var back=art.opponentCardBack!=null?art.opponentCardBack:art.cardBack;
                var card=UIFactory.Image("Card"+i,composition,back,new Vector2(200,300),new Vector2((i-1)*108,40+(i==1?25:0)),true);
                card.rectTransform.localRotation=Quaternion.Euler(0,0,(1-i)*13);
            }
            var plate=UIFactory.Rect("LoadingPlate",composition,new Vector2(720,200),new Vector2(0,-330));
            PanelTheme.Surface(plate.gameObject.AddComponent<Image>());
            _status=UIFactory.Text("Status",plate,"Preparing the court…",31,new Vector2(620,70),new Vector2(0,28),prose:true);
            var track=UIFactory.Image("Track",plate,art.roundedPanel,new Vector2(560,8),new Vector2(0,-38),false);
            track.type=Image.Type.Sliced;track.color=new Color(.25f,.23f,.17f,1);
            var spark=UIFactory.Image("Activity",track.transform,art.roundedPanel,new Vector2(92,8),Vector2.zero,false);
            spark.type=Image.Type.Sliced;spark.color=UIFactory.Gold;_spark=spark.rectTransform;
            UIFactory.Text("Caption",composition,"A game of influence and deception",29,new Vector2(850,90),new Vector2(0,-650),prose:true).color=new Color(.69f,.67f,.60f);
            root.SetAsLastSibling();
        }
        private void Update() {
            if(_spark!=null)_spark.anchoredPosition=new Vector2(Mathf.Sin((Time.realtimeSinceStartup-_began)*2.6f)*230,0);
        }
        public IEnumerator Prepare() {
            var theme=Resources.LoadAsync<PanelTheme>("PanelTheme");
            yield return theme;
            Canvas.ForceUpdateCanvases();
            yield return null;
            // Keep the opening legible; subsequent lobby visits skip this curtain.
            while(Time.realtimeSinceStartup-_began<1.2f)yield return null;
        }
        public void Restoring() {if(_status!=null)_status.text="Restoring your account…";}
        public IEnumerator Dismiss() {
            var started=Time.realtimeSinceStartup;
            while(this!=null && Time.realtimeSinceStartup-started<.25f) {
                _group.alpha=1-(Time.realtimeSinceStartup-started)/.25f;yield return null;
            }
            if(_root!=null)Destroy(_root);
            Destroy(this);
        }
        private void OnDestroy(){if(_root!=null)Destroy(_root);}
    }
}
