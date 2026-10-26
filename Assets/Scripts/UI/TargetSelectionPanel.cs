using System;
using System.Linq;
using Coup.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Coup.Game.UI
{
    /// <summary>An explicit, thumb-sized alternative to tapping an opponent's portrait.</summary>
    public sealed class TargetSelectionPanel : MonoBehaviour
    {
        RectTransform _root,_content;
        string _key;
        Action<int> _choose;
        Action _cancel;
        CanvasGroup _group;
        float _shown = -1;
        public void Present(ActionKind? action, CoupGame game, int me, CoupArt art, RectTransform actionBar, Action<int> choose, Action cancel)
        {
            _choose=choose;_cancel=cancel;
            if (!action.HasValue) { if(_root!=null)_root.gameObject.SetActive(false);return; }
            if(_root==null)
            {
                _root=UIFactory.Rect("ChooseOpponent",actionBar.parent,new Vector2(1032,450),Vector2.zero);
                var fill=_root.gameObject.AddComponent<Image>();fill.sprite=art?.roundedPanel;fill.type=Image.Type.Sliced;fill.color=new Color(.015f,.04f,.065f,.99f);
                PanelTheme.Surface(fill);
                _group=UIFactory.Group(_root.gameObject);
                PanelAura.Attach(_root,art,UIFactory.Gold,18);
                // Rebuilt rows live in their own layer so clearing them keeps the frame and aura.
                _content=UIFactory.Rect("Content",_root,Vector2.zero,Vector2.zero);
                _content.anchorMin=Vector2.zero;_content.anchorMax=Vector2.one;_content.offsetMin=_content.offsetMax=Vector2.zero;
                _root.gameObject.SetActive(false);
            }
            if(!_root.gameObject.activeSelf)_shown=Time.unscaledTime;
            _root.anchorMin=actionBar.anchorMin;_root.anchorMax=actionBar.anchorMax;
            _root.anchoredPosition=actionBar.anchoredPosition;
            _root.gameObject.SetActive(true);actionBar.gameObject.SetActive(false);
            var seats=game.Players.Where(p=>game.CanDeclare(me,action.Value,p.Index)).ToArray();
            var key=action+":"+string.Join(",",seats.Select(p=>p.Index+"="+p.Name));
            if(_key==key)return;_key=key;
            foreach(Transform child in _content){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            var title=UIFactory.Text("Title",_content,"CHOOSE AN OPPONENT",29,new Vector2(900,40),new Vector2(0,180));title.color=UIFactory.Gold;PanelTheme.Title(title);
            for(int i=0;i<seats.Length;i++)
            {
                var seat=seats[i];int row=i/3,cols=Mathf.Min(3,seats.Length-row*3);
                var at=new Vector2((i%3-(cols-1)*.5f)*304,108-row*114);
                var button=UIFactory.Button("Target_"+seat.Index,_content,art?.roundedPanel,new Vector2(284,98),at);
                button.GetComponent<Image>().type=Image.Type.Sliced;button.GetComponent<Image>().preserveAspect=false;
                PanelTheme.Button(button);
                // Player names come from the server, so they stay plain text.
                var label=UIFactory.Text("Name",button.transform,seat.Name.ToUpperInvariant(),29,new Vector2(260,44),new Vector2(0,14));label.richText=false;
                label.enableAutoSizing=true;label.fontSizeMin=20;label.fontSizeMax=29;
                var coins=UIFactory.Text("Coins",button.transform,seat.Coins+(seat.Coins==1?" COIN":" COINS"),21,new Vector2(260,30),new Vector2(0,-24));coins.color=UIFactory.Gold;
                button.onClick.AddListener(()=>_choose?.Invoke(seat.Index));
            }
            var back=UIFactory.Button("Cancel",_content,art?.roundedPanel,new Vector2(400,64),new Vector2(0,-118));
            var cancelFill=back.GetComponent<Image>();cancelFill.type=Image.Type.Sliced;cancelFill.preserveAspect=false;
            PanelTheme.Button(back);
            UIFactory.Text("Label",back.transform,"CANCEL ACTION",26,new Vector2(380,56),Vector2.zero);
            back.onClick.AddListener(()=>_cancel?.Invoke());
        }
        void LateUpdate()
        {
            if(_root==null||_group==null||!_root.gameObject.activeSelf)return;
            // Rise out of where the action bar was, so the swap reads as one control changing mode.
            var t=PlayerPrefs.GetInt("Coup.ReducedMotion",0)!=0?1f:Mathf.Clamp01((Time.unscaledTime-_shown)/.26f);
            var ease=1-Mathf.Pow(1-t,3);
            _group.alpha=ease;
            _root.localScale=Vector3.one*Mathf.Lerp(.96f,1f,ease);
        }
    }
}
