using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Coup.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Coup.Game.UI
{
    /// <summary>Account entry gate over the authored lobby. No session is created until a choice is made.</summary>
    public sealed class LoginScreen : MonoBehaviour
    {
        private GameObject _root;
        private TMP_Text _status;
        private Button _guest, _google, _cancel;
        private CancellationTokenSource _attempt;
        private PlatformClient _api;
        public bool IsVisible => _root != null && _root.activeSelf;
        public bool Busy { get; private set; }
        private static bool _startupShown;
        public bool IsStarting { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStartup() => _startupShown=false;
        private IEnumerator Start() {
            _api=PlatformClient.Instance;
            Build();
            Show();
            if(!_startupShown) {
                IsStarting=true;_root.SetActive(false);
                var restored=false;
                var loading=gameObject.AddComponent<StartupLoadingScreen>();loading.Show(GetComponent<LobbyScreen>().art);
                yield return loading.Prepare();
                if(_api.HasAccount) {
                    loading.Restoring();
                    _attempt=new CancellationTokenSource();
                    var restore=_api.RefreshProfile(_attempt.Token);
                    while(!restore.IsCompleted)yield return null;
                    if(restore.IsCompletedSuccessfully){EnterLobby();restored=true;}
                    else if(restore.IsFaulted) {
                        // Observe the task error and retain a usable login page.
                        var error=restore.Exception.GetBaseException();
                        _status.text=error is ApiException||error is InvalidOperationException?error.Message:"Could not restore your account. Please sign in again.";
                    }
                }
                if(!restored)_root.SetActive(true);
                yield return loading.Dismiss();
                _startupShown=true;IsStarting=false;
            } else if(_api.HasAccount) SignIn(false);
        }
        private void Build() {
            var art=GetComponent<LobbyScreen>().art;
            var root=UIFactory.Rect("LoginPage",transform,Vector2.zero,Vector2.zero);
            root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
            var veil=root.gameObject.AddComponent<Image>();veil.color=new Color(.01f,.02f,.04f,.60f);
            _root=root.gameObject;
            var canvas=GetComponentInParent<Canvas>().transform as RectTransform;
            var composition=UIFactory.Rect("Composition",root,new Vector2(1080,1920),Vector2.zero);
            float scale=Mathf.Min(1f,canvas.rect.height/1920f,canvas.rect.width/1080f);
            composition.localScale=Vector3.one*scale;
            UIFactory.Text("Overline",composition,"THE COURT AWAITS",25,new Vector2(800,60),new Vector2(0,760)).color=UIFactory.Gold;
            UIFactory.Text("Title",composition,"COUP",118,new Vector2(880,170),new Vector2(0,650)).color=UIFactory.Parchment;
            UIFactory.Text("Subtitle",composition,"VEILED COURT",38,new Vector2(800,70),new Vector2(0,550)).color=UIFactory.Gold;
            var card=UIFactory.Rect("AccountCard",composition,new Vector2(880,680),new Vector2(0,-470));
            var theme=GetComponent<LobbyScreen>();
            var fill=card.gameObject.AddComponent<Image>();fill.sprite=theme.loginPanel ?? art.roundedPanel;fill.type=Image.Type.Sliced;fill.color=Color.white;PanelTheme.Surface(fill);
            
            UIFactory.Text("Welcome",card,"TAKE YOUR SEAT",40,new Vector2(760,85),new Vector2(0,260)).color=UIFactory.Gold;
            UIFactory.Text("Body",card,"Sign in to keep your court within reach.",29,new Vector2(760,70),new Vector2(0,182),prose:true);
            _google=Choice(card,"GoogleSignIn","Sign in with Google",60,Color.white,new Color(.08f,.12f,.18f,1));
            _google.onClick.AddListener(()=>SignIn(true));
            UIFactory.Text("Divider",card,"OR",23,new Vector2(700,40),new Vector2(0,-22)).color=new Color(.65f,.62f,.53f);
            _guest=Choice(card,"GuestSignIn","Continue as Guest",-108,UIFactory.Gold,new Color(.13f,.105f,.055f,1));
            _guest.onClick.AddListener(()=>SignIn(false));
            _status=UIFactory.Text("Status",card,"Guest progress is saved online. Link Google for account recovery.",26,new Vector2(740,100),new Vector2(0,-222),prose:true);
            _status.richText=false;
            _cancel=UIFactory.Button("Cancel",card,null,new Vector2(280,48),new Vector2(0,-302));
            UIFactory.Text("Label",_cancel.transform,"CANCEL",22,new Vector2(270,46),Vector2.zero);
            _cancel.onClick.AddListener(Cancel);_cancel.gameObject.SetActive(false);
            var support=UIFactory.Button("PrivacySupport",composition,null,new Vector2(780,64),new Vector2(0,-875));
            UIFactory.Text("Label",support.transform,"FIESTA GAMES · PRIVACY & SUPPORT",23,new Vector2(760,60),Vector2.zero).color=UIFactory.Gold;
            support.onClick.AddListener(()=>Application.OpenURL(_api.BaseUrl.TrimEnd('/')+"/support/account-deletion"));

        }
        private Button Choice(Transform parent,string name,string caption,float y,Color ink,Color fill) {
            var art=GetComponent<LobbyScreen>().art;
            var theme=GetComponent<LobbyScreen>();
            var b=UIFactory.Button(name,parent,theme.loginButtonFill ?? art.roundedPanel,new Vector2(748,112),new Vector2(0,y));
            var image=b.GetComponent<Image>();image.type=Image.Type.Sliced;image.preserveAspect=false;image.pixelsPerUnitMultiplier=2f;image.color=fill;
            var rim=UIFactory.Image("Rim",b.transform,theme.loginButtonFrame ?? art.roundedBorder,new Vector2(748,112),Vector2.zero,false);rim.type=Image.Type.Sliced;rim.pixelsPerUnitMultiplier=2f;rim.color=Color.white;
            var icons=PanelTheme.Current;
            var icon=icons==null?null:(name=="GoogleSignIn"?icons.googleIcon:icons.guestIcon);
            if(icon!=null)UIFactory.Image("Icon",b.transform,icon,new Vector2(name=="GoogleSignIn"?64:84,name=="GoogleSignIn"?64:84),new Vector2(-245,0),true);
            UIFactory.Text("Label",b.transform,caption,33,new Vector2(icon!=null?520:700,94),new Vector2(icon!=null?55:0,0)).color=ink;
            return b;
        }
        public void Show() {
            if(_root==null)return;
            Cancel();GetComponent<PlatformPanels>()?.Close();_root.SetActive(true);_root.transform.SetAsLastSibling();
            foreach(var name in new[]{"Hud","Actions"}) {var t=transform.Find(name);if(t!=null)t.gameObject.SetActive(false);}
        }
        private void EnterLobby() {
            _root.SetActive(false);
            foreach(var name in new[]{"Hud","Actions"}) {var t=transform.Find(name);if(t!=null)t.gameObject.SetActive(true);}
        }
        private void Controls(bool busy) {
            Busy=busy;_guest.interactable=_google.interactable=!busy;
            UIFactory.Group(_guest.gameObject).alpha=UIFactory.Group(_google.gameObject).alpha=busy?.5f:1f;_cancel.gameObject.SetActive(busy);
        }
        private async void SignIn(bool google) {
            if(Busy||IsStarting)return;
            _attempt?.Dispose();_attempt=new CancellationTokenSource();var token=_attempt.Token;
            Controls(true);_status.text=google?"Opening Google sign-in…":"Connecting to your court…";
            try {
                if(google) await _api.SignInWithGoogle(token,()=>{if(this!=null)_status.text="Finish signing in in your browser, then return here.";});
                else await _api.RefreshProfile(token);
                token.ThrowIfCancellationRequested();if(this!=null)EnterLobby();
            }
            catch(OperationCanceledException) { }
            catch(Exception error) {if(this!=null&&!token.IsCancellationRequested)_status.text=error is ApiException||error is InvalidOperationException?error.Message:"Could not sign in. Please try again.";}
            finally {if(this!=null&&!token.IsCancellationRequested)Controls(false);}
        }
        private void Cancel() {_attempt?.Cancel();if(_guest!=null){Controls(false);_status.text="Guest progress is saved online. Link Google for account recovery.";}}
        private void OnDestroy(){_attempt?.Cancel();_attempt?.Dispose();}
    }
}
