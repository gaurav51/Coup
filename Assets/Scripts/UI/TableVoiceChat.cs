using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Coup.Net;
using TMPro;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.UI;

namespace Coup.Game.UI
{
    /// <summary>Opt-in table voice. Credentials/tokens are vended only by our authenticated game server.</summary>
    public sealed class TableVoiceChat : MonoBehaviour, IVivoxTokenProvider
    {
        static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        static bool _initialized;
        static TableVoiceChat _owner;
        MatchController _match;
        CoupArt _art;
        GameObject _panel;
        RectTransform _sheet;
        TMP_Text _status, _people, _joinText, _micText, _speakerText;
        Button _join, _mic, _speaker;
        string _channel;
        string _connectStage = "table access";
        bool _joined, _busy, _dead, _micMuted = true, _speakerMuted;
        float _nextRoster;
        readonly CancellationTokenSource _life = new CancellationTokenSource();

        void Start()
        {
            var table = GetComponent<TableView>(); _match = table.match; _art = table.art;
            var stage = transform.Find("Stage");
            var trigger = MakeButton("Voice", stage, "VOICE", new Vector2(160, 76), Vector2.zero, Open);
            var rt = (RectTransform)trigger.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(112, -218);
            Build();
        }

        Button MakeButton(string name, Transform parent, string label, Vector2 size, Vector2 at, UnityEngine.Events.UnityAction action)
        {
            var b = UIFactory.Button(name, parent, _art != null ? _art.roundedPanel : null, size, at);
            var image = b.GetComponent<Image>(); image.type = Image.Type.Sliced; image.preserveAspect = false;
            image.color = new Color(.025f, .065f, .10f, 1);
            var rim = UIFactory.Image("Rim", b.transform, _art != null ? _art.roundedBorder : null, size, Vector2.zero, false);
            rim.type = Image.Type.Sliced; rim.color = UIFactory.Gold;
            // Only buttons inside the voice sheet take the panel pill; the table trigger matches RULES.
            if (parent != transform.Find("Stage")) PanelTheme.Button(b);
            UIFactory.Text("Label", b.transform, label, 25, size - new Vector2(16, 10), Vector2.zero);
            b.onClick.AddListener(action); return b;
        }
        void Build()
        {
            var root = UIFactory.Rect("TableVoicePanel", transform, Vector2.zero, Vector2.zero);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            var dim = root.gameObject.AddComponent<Image>(); dim.color = new Color(.005f,.012f,.025f,.86f);
            _panel = root.gameObject;
            _sheet = UIFactory.Rect("VoiceSheet", root, new Vector2(900, 860), Vector2.zero);
            var bg = _sheet.gameObject.AddComponent<Image>(); bg.sprite = _art != null ? _art.roundedPanel : null;
            bg.type = Image.Type.Sliced; bg.color = new Color(.02f,.045f,.075f,1);
            var rim=UIFactory.Image("Rim",_sheet,_art != null ? _art.roundedBorder : null,_sheet.sizeDelta,Vector2.zero,false);
            rim.type=Image.Type.Sliced;rim.color=UIFactory.Gold;
            PanelTheme.Surface(bg,rim);
            PanelAura.Attach(_sheet,_art,UIFactory.Gold,20);
            var title=UIFactory.Text("Title",_sheet,"TABLE VOICE",40,new Vector2(780,70),new Vector2(0,345));title.color=UIFactory.Gold;PanelTheme.Title(title);
            _status=UIFactory.Text("Body",_sheet,"Join your table's voice channel.\nMicrophone access is required; you start muted.",29,new Vector2(790,130),new Vector2(0,245));
            _join=MakeButton("Join",_sheet,"JOIN VOICE",new Vector2(236,104),new Vector2(-258,100),ToggleJoin);
            _mic=MakeButton("Mic",_sheet,"MIC: OFF",new Vector2(236,104),new Vector2(0,100),ToggleMic);
            _speaker=MakeButton("Speaker",_sheet,"SOUND: ON",new Vector2(236,104),new Vector2(258,100),ToggleSpeaker);
            _joinText=_join.GetComponentInChildren<TMP_Text>();_micText=_mic.GetComponentInChildren<TMP_Text>();_speakerText=_speaker.GetComponentInChildren<TMP_Text>();
            _people=UIFactory.Text("Participants",_sheet,"You have not joined voice.",29,new Vector2(780,220),new Vector2(0,-70));
            _people.richText=false;
            UIFactory.Text("Body",_sheet,"Only players who join voice can hear you.\nLeaving the table disconnects voice.",25,new Vector2(780,80),new Vector2(0,-215));
            MakeButton("Close",_sheet,"BACK TO TABLE",new Vector2(440,88),new Vector2(0,-322),()=>PanelTransition.For(_panel,_sheet).Hide());
            _panel.SetActive(false);RefreshControls();
        }
        public void Open() { _panel.transform.SetAsLastSibling();PanelTransition.For(_panel,_sheet).Show(); }
        void RefreshControls()
        {
            if (_dead || _join == null) return;
            _join.interactable=!_busy;_mic.interactable=_speaker.interactable=_joined&&!_busy;
            _joinText.text=_busy?"CONNECTING…":_joined?"LEAVE VOICE":"JOIN VOICE";
            _micText.text=_micMuted?"MIC: OFF":"MIC: ON";
            _speakerText.text=_speakerMuted?"SOUND: OFF":"SOUND: ON";
        }
        async void ToggleJoin()
        {
            if (_busy) return;
            if (!_joined && !_match.OnlineConnected)
            {
                _status.text = _match.playOnline ? "Waiting for your online table to connect.\nTry again once you are seated." : "Voice is available at online tables.\nPractice games do not have a voice channel.";
                return;
            }
            _busy=true;RefreshControls();
            try
            {
                await Gate.WaitAsync(_life.Token);
                try
                {
                    if (_joined) { await Disconnect();_status.text="You left table voice."; }
                    else
                    {
                        _connectStage="table access";
                        var config=await _match.VoiceAccess("config");_life.Token.ThrowIfCancellationRequested();
                        _connectStage="microphone permission";
                        if (!await MicrophonePermission())
                        {
                            if (!_dead) _status.text="Allow microphone access in device settings to join voice.\nYour mic will still start muted.";
                            return;
                        }
                        _life.Token.ThrowIfCancellationRequested();
                        _owner=this;
                        _status.text="Connecting to table voice…";
                        if (!_initialized)
                        {
                            _connectStage="voice initialization";
                            var options=new InitializationOptions().SetVivoxCredentials(config.Str("server"),config.Str("domain"),config.Str("issuer"));
                            await UnityServices.InitializeAsync(options);
                            await VivoxService.Instance.InitializeAsync();_initialized=true;
                        }
                        VivoxService.Instance.SetTokenProvider(this);
                        if(VivoxService.Instance.IsLoggedIn)await VivoxService.Instance.LogoutAsync();
                        _connectStage="voice sign-in";
                        await VivoxService.Instance.LoginAsync(new LoginOptions {PlayerId=config.Str("user"),DisplayName=PlatformClient.Instance.Profile?.Str("name")??"Player"});
                        _life.Token.ThrowIfCancellationRequested();
                        _micMuted=true;_speakerMuted=false;VivoxService.Instance.MuteInputDevice();VivoxService.Instance.UnmuteOutputDevice();
                        _channel=config.Str("channel");
                        _connectStage="channel join";
                        await VivoxService.Instance.JoinGroupChannelAsync(_channel,ChatCapability.AudioOnly,new ChannelOptions{MakeActiveChannelUponJoining=true});
                        _life.Token.ThrowIfCancellationRequested();_joined=true;
                        _status.text="Connected to your table.\nMicrophone muted — tap MIC to speak.";
                    }
                }
                finally { Gate.Release(); }
            }
            catch(OperationCanceledException) { }
            catch(Exception error)
            {
                if(!_dead)
                {
                    var code = error is VivoxApiException vivox ? vivox.StatusCode.ToString() : error is RequestFailedException core ? core.ErrorCode.ToString() : error.GetType().Name;
                    Debug.LogWarning("[TableVoice] Failed at " + _connectStage + " (" + code + ").");
                    _status.text = "Could not connect at " + _connectStage + ".\nReference: " + code + ". Tap JOIN VOICE to retry.";
                    await Cleanup();
                }
            }
            finally { _busy=false;RefreshControls(); }
        }
        public async Task<string> GetTokenAsync(string issuer=null,TimeSpan? expiration=null,string targetUserUri=null,string action=null,string channelUri=null,string fromUserUri=null,string realm=null)
        {
            _life.Token.ThrowIfCancellationRequested();
            var reply=await _match.VoiceAccess(action);_life.Token.ThrowIfCancellationRequested();return reply.Str("token");
        }
        async void ToggleMic()
        {
            if(!_joined||_busy)return;
            if(_micMuted)
            {
                _busy=true;RefreshControls();
                try
                {
                    if(!await MicrophonePermission()||_dead){if(!_dead)_status.text="Microphone permission is off. You can still listen.";return;}
                    if(!_joined)return;VivoxService.Instance.UnmuteInputDevice();_micMuted=false;_status.text="Microphone on. Your table can hear you.";
                }
                catch(Exception){if(!_dead)_status.text="Microphone unavailable. Check device permissions.";}
                finally{_busy=false;RefreshControls();}
            }
            else{VivoxService.Instance.MuteInputDevice();_micMuted=true;_status.text="Microphone muted. You can still listen.";RefreshControls();}
        }
        async Task<bool> MicrophonePermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if(UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))return true;
            var completion=new TaskCompletionSource<bool>();var callbacks=new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted+=_=>completion.TrySetResult(true);
            callbacks.PermissionDenied+=_=>completion.TrySetResult(false);
            using(_life.Token.Register(()=>completion.TrySetResult(false)))
            {UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone,callbacks);return await completion.Task;}
#else
            if(Application.HasUserAuthorization(UserAuthorization.Microphone))return true;
            var request=Application.RequestUserAuthorization(UserAuthorization.Microphone);
            while(!request.isDone&&!_dead)await Task.Yield();
            return !_dead&&Application.HasUserAuthorization(UserAuthorization.Microphone);
#endif
        }
        void ToggleSpeaker()
        {
            if(!_joined)return;_speakerMuted=!_speakerMuted;
            if(_speakerMuted)VivoxService.Instance.MuteOutputDevice();else VivoxService.Instance.UnmuteOutputDevice();RefreshControls();
        }
        void Update()
        {
            if(!_joined||_dead||Time.unscaledTime<_nextRoster)return;_nextRoster=Time.unscaledTime+.2f;
            if(!VivoxService.Instance.IsLoggedIn||!VivoxService.Instance.ActiveChannels.ContainsKey(_channel))
            {_joined=false;_status.text="Voice disconnected. Tap JOIN VOICE to reconnect.";RefreshControls();}
            if(!_panel.activeSelf)return;
            if(!_joined){_people.text="You have not joined voice.";return;}
            var text=new StringBuilder();
            foreach(var p in VivoxService.Instance.ActiveChannels[_channel])
                text.Append(p.SpeechDetected?"SPEAKING  ·  ":"•  ").Append(p.DisplayName).Append(p.IsSelf?" (You)":"").AppendLine();
            _people.text=text.ToString();
        }
        async Task Disconnect()
        {
            if(_initialized && _owner==this){VivoxService.Instance.MuteInputDevice();if(VivoxService.Instance.IsLoggedIn)await VivoxService.Instance.LogoutAsync();_owner=null;}
            _joined=false;_micMuted=true;_speakerMuted=false;_channel=null;if(!_dead)_people.text="You have not joined voice.";
        }
        async Task Cleanup(){await Gate.WaitAsync();try{await Disconnect();}catch(Exception){}finally{Gate.Release();}}
        void OnApplicationPause(bool paused){if(paused&&_joined){VivoxService.Instance.MuteInputDevice();_micMuted=true;RefreshControls();}}
        async void OnDestroy(){_dead=true;_life.Cancel();await Cleanup();_life.Dispose();}
    }
}
