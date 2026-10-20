using UnityEngine;
using UnityEngine.UI;
namespace Coup.Game.UI
{
    /// <summary>Shared image treatment for account pages, game decisions and network overlays.</summary>
    [CreateAssetMenu(menuName="Coup/Panel Theme")]
    public sealed class PanelTheme : ScriptableObject
    {
        public Sprite panel, panelFill, buttonFill, buttonFrame;
        public Sprite googleIcon, guestIcon;
        private static PanelTheme _cached;
        public static PanelTheme Current => _cached!=null?_cached:(_cached=Resources.Load<PanelTheme>("PanelTheme"));
        public static void Surface(Image background,Image oldBorder=null) {
            var theme=Current;if(theme==null||background==null||theme.panel==null)return;
            background.sprite=theme.panelFill ?? theme.buttonFill;background.type=Image.Type.Sliced;background.preserveAspect=false;
            background.pixelsPerUnitMultiplier=1f;background.color=new Color(.025f,.045f,.075f,1f);
            var frame=background.transform.Find("SurfaceRim")?.GetComponent<Image>();
            if(frame==null)frame=UIFactory.Image("SurfaceRim",background.transform,theme.panel,Vector2.zero,Vector2.zero,false);
            frame.sprite=theme.panel;frame.type=Image.Type.Sliced;frame.fillCenter=false;
            frame.pixelsPerUnitMultiplier=3f;frame.color=Color.white;frame.raycastTarget=false;
            var rect=frame.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            frame.transform.SetAsFirstSibling();
            if(oldBorder!=null)oldBorder.enabled=false;
        }
        public static void Button(Button button) {
            var theme=Current;if(theme==null||button==null||theme.buttonFrame==null)return;
            var image=button.GetComponent<Image>();if(image!=null){image.sprite=theme.buttonFill;image.type=Image.Type.Sliced;image.preserveAspect=false;image.pixelsPerUnitMultiplier=2f;image.color=new Color(.025f,.045f,.075f,1);}
            foreach(var name in new[]{"Rim","Outline","Border"}) {var old=button.transform.Find(name)?.GetComponent<Image>();if(old!=null)old.enabled=false;}
            var frame=button.transform.Find("ThemeFrame")?.GetComponent<Image>();
            if(frame==null)frame=UIFactory.Image("ThemeFrame",button.transform,theme.buttonFrame,Vector2.zero,Vector2.zero,false);
            frame.sprite=theme.buttonFrame;frame.type=Image.Type.Sliced;frame.pixelsPerUnitMultiplier=2f;frame.color=Color.white;frame.raycastTarget=false;
            var rect=frame.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            frame.transform.SetAsFirstSibling();
        }
    }
}
