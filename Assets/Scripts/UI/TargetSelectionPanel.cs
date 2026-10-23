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
        RectTransform _root;
        string _key;
        Action<int> _choose;
        Action _cancel;
        public void Present(ActionKind? action, CoupGame game, int me, CoupArt art, RectTransform actionBar, Action<int> choose, Action cancel)
        {
            _choose=choose;_cancel=cancel;
            if (!action.HasValue) { if(_root!=null)_root.gameObject.SetActive(false);return; }
            if(_root==null)
            {
                _root=UIFactory.Rect("ChooseOpponent",actionBar.parent,new Vector2(1032,410),Vector2.zero);
                var fill=_root.gameObject.AddComponent<Image>();fill.sprite=art?.roundedPanel;fill.type=Image.Type.Sliced;fill.color=new Color(.015f,.04f,.065f,.99f);
            }
            _root.anchorMin=actionBar.anchorMin;_root.anchorMax=actionBar.anchorMax;
            _root.anchoredPosition=actionBar.anchoredPosition;
            _root.gameObject.SetActive(true);actionBar.gameObject.SetActive(false);
            var seats=game.Players.Where(p=>game.CanDeclare(me,action.Value,p.Index)).ToArray();
            var key=action+":"+string.Join(",",seats.Select(p=>p.Index+"="+p.Name));
            if(_key==key)return;_key=key;
            foreach(Transform child in _root){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            UIFactory.Text("Title",_root,"CHOOSE AN OPPONENT",29,new Vector2(960,40),new Vector2(0,174)).color=UIFactory.Gold;
            for(int i=0;i<seats.Length;i++)
            {
                var seat=seats[i];int row=i/3,cols=Mathf.Min(3,seats.Length-row*3);
                var at=new Vector2((i%3-(cols-1)*.5f)*328,92-row*122);
                var button=UIFactory.Button("Target_"+seat.Index,_root,art?.roundedPanel,new Vector2(310,108),at);
                button.GetComponent<Image>().color=new Color(.08f,.15f,.21f);button.GetComponent<Image>().type=Image.Type.Sliced;button.GetComponent<Image>().preserveAspect=false;
                var rim=UIFactory.Image("Rim",button.transform,art?.roundedBorder,new Vector2(310,108),Vector2.zero,false);rim.type=Image.Type.Sliced;rim.color=UIFactory.Gold;
                var label=UIFactory.Text("Name",button.transform,seat.Name+"\nSELECT",28,new Vector2(290,96),Vector2.zero);label.richText=false;
                button.onClick.AddListener(()=>_choose?.Invoke(seat.Index));
            }
            var back=UIFactory.Button("Cancel",_root,art?.roundedPanel,new Vector2(450,70),new Vector2(0,-155));
            var cancelFill=back.GetComponent<Image>();cancelFill.type=Image.Type.Sliced;cancelFill.preserveAspect=false;cancelFill.color=new Color(.08f,.12f,.18f,1);
            var cancelRim=UIFactory.Image("Rim",back.transform,art?.roundedBorder,new Vector2(450,70),Vector2.zero,false);cancelRim.type=Image.Type.Sliced;cancelRim.color=UIFactory.Gold;
            UIFactory.Text("Label",back.transform,"CANCEL ACTION",26,new Vector2(430,60),Vector2.zero);
            back.onClick.AddListener(()=>_cancel?.Invoke());
        }
    }
}
