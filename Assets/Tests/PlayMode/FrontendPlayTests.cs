using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Coup.Game;
using Coup.Game.UI;
using Coup.Net;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace Coup.Frontend.Tests
{
    public class FrontendPlayTests
    {
        [UnitySetUp]
        public IEnumerator IsolateGuestSession() {
            // Test accounts are ephemeral Editor guests; every flow gets independent server state.
            PlatformClient.Instance.Forget();
            yield return null;
        }
        private static IEnumerator Until(Func<bool> ready,float timeout=25) {
            var end=Time.realtimeSinceStartup+timeout;
            while(!ready()&&Time.realtimeSinceStartup<end)yield return null;
            Assert.IsTrue(ready(),"Timed out waiting for the frontend flow.");
        }
        [UnityTest,Category("Integration")]
        public IEnumerator LoginRequiresChoiceAndGuestEntersLobby() {
            yield return SceneManager.LoadSceneAsync("Lobby");yield return null;
            var login=UnityEngine.Object.FindAnyObjectByType<LoginScreen>();
            yield return Until(()=>!login.IsStarting);
            Assert.IsTrue(login.IsVisible);Assert.IsFalse(PlatformClient.Instance.HasAccount);
            Assert.IsFalse(login.transform.Find("Actions").gameObject.activeSelf);
            Assert.NotNull(GameObject.Find("GoogleSignIn"));
            foreach(var text in login.transform.Find("LoginPage").GetComponentsInChildren<TMP_Text>()) {
                text.ForceMeshUpdate();Assert.IsFalse(text.isTextOverflowing,text.text);
            }
            GameObject.Find("GuestSignIn").GetComponent<Button>().onClick.Invoke();
            yield return Until(()=>!login.IsVisible);
            Assert.NotNull(PlatformClient.Instance.Profile);
            Assert.IsTrue(login.transform.Find("Actions").gameObject.activeSelf);
            yield return SceneManager.LoadSceneAsync("Lobby");yield return null;
            login=UnityEngine.Object.FindAnyObjectByType<LoginScreen>();
            yield return Until(()=>!login.IsVisible);
            var panels=UnityEngine.Object.FindAnyObjectByType<PlatformPanels>();
            panels.Show("Sign out");yield return Until(()=>!panels.Loading);
            var confirm=panels.transform.Find("PlatformPanels/Card/Viewport/Content/Confirm sign out").GetComponent<Button>();
            confirm.onClick.Invoke();yield return Until(()=>login.IsVisible&&!PlatformClient.Instance.HasAccount);
            Assert.IsFalse(login.transform.Find("Actions").gameObject.activeSelf);
        }
        [UnityTest,Category("Integration")]
        public IEnumerator OnlinePanelsLoadFromBackendAndRemainUsableAfterCancellation() {
            yield return SceneManager.LoadSceneAsync("Lobby");
            yield return Until(()=>GameObject.Find("GuestSignIn")!=null && !UnityEngine.Object.FindAnyObjectByType<LoginScreen>().IsStarting);
            GameObject.Find("GuestSignIn").GetComponent<Button>().onClick.Invoke();
            yield return Until(()=>PlatformClient.Instance.Profile!=null);
            var panels=UnityEngine.Object.FindAnyObjectByType<PlatformPanels>();Assert.NotNull(panels);
            foreach(var page in new[]{"Profile","Account","Friends","Private table","Ranked","Leaderboard","History","Wallet","Missions","Collection","Shop","Season","Event","Settings","Rules","Connection","Delete account","Sign out","Play"}) {
                panels.Show(page);yield return Until(()=>!panels.Loading);yield return null;
                var root=panels.transform.Find("PlatformPanels");Assert.IsTrue(root.gameObject.activeSelf,page);
                var texts=root.GetComponentsInChildren<TMP_Text>(false);
                Assert.IsFalse(texts.Any(t=>t.text.Contains("This screen could not load")||t.text.Contains("API not found")||t.text.Contains("The server could not complete")),page);
                foreach(var text in texts){text.ForceMeshUpdate();Assert.IsFalse(text.isTextOverflowing,page+" overflow: "+text.text);}
                Assert.Greater(root.GetComponentsInChildren<Button>(false).Length,0,page);
            }
            panels.Show("Profile");panels.Close();panels.Show("Settings");yield return null;
            Assert.AreEqual("Settings",panels.CurrentPage);Assert.IsTrue(panels.IsOpen);
            panels.Close();Assert.IsFalse(panels.IsOpen);
        }
        [UnityTest,Category("Integration")]
        public IEnumerator PublicSocketAbortAutomaticallyRecoversSameSeatAndPrivateHand() {
            MatchLaunch.Host();yield return SceneManager.LoadSceneAsync("Game");
            var match=UnityEngine.Object.FindAnyObjectByType<MatchController>();
            yield return Until(()=>match.OnlineConnected);
            yield return Until(()=>GameObject.Find("NetworkOverlay/Card/Fill with bots")!=null);
            var fill=GameObject.Find("NetworkOverlay/Card/Fill with bots");fill.GetComponent<Button>().onClick.Invoke();
            yield return Until(()=>match.Game!=null&&match.OnlineConnected);
            var hand=string.Join(",",match.Game.Players[match.humanSeat].Influence);
            var online=(OnlineSession)typeof(MatchController).GetField("_online",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(match);
            var code=(string)typeof(OnlineSession).GetField("_code",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(online);
            var client=(SocketIoClient)typeof(OnlineSession).GetField("_socket",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(online);
            var socket=(System.Net.WebSockets.ClientWebSocket)typeof(SocketIoClient).GetField("_socket",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(client);
            var replayedEffects=0;match.EventOccurred+=_=>replayedEffects++;
            socket.Abort();yield return Until(()=>!match.OnlineConnected);yield return Until(()=>match.OnlineConnected);
            Assert.AreEqual(code,(string)typeof(OnlineSession).GetField("_code",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(online));
            Assert.AreEqual(0,replayedEffects,"Recovery must not replay historical action VFX.");
            Assert.AreEqual(hand,string.Join(",",match.Game.Players[match.humanSeat].Influence));
            Assert.IsFalse(GameObject.Find("NetworkOverlay")?.activeSelf??false);
            var leave=client.Emit("lobby:leave",JsonValue.FromObject());yield return Until(()=>leave.IsCompleted);Assert.IsTrue(leave.Result.Flag("ok"));
            match.ReturnToLobby();yield return null;
        }
        [UnityTest,Category("Integration")]
        public IEnumerator RankedQueueStartsWithTwoHumanAccounts() {
            var api=PlatformClient.Instance;
            var guest=api.PublicRequest("/v1/auth/guest",JsonValue.FromObject(("name",JsonValue.FromString("Queue Tester"))),System.Threading.CancellationToken.None);
            yield return Until(()=>guest.IsCompleted);Assert.IsFalse(guest.IsFaulted);
            var actions=new System.Collections.Generic.Queue<Action>();
            var peer=new SocketIoClient(action=>actions.Enqueue(action));
            try {
                var connecting=peer.Connect(api.BaseUrl,guest.Result.Str("accessToken"));
                while(!connecting.IsCompleted){while(actions.Count>0)actions.Dequeue()();yield return null;}
                Assert.IsFalse(connecting.IsFaulted);
                MatchLaunch.Queue("ranked");yield return SceneManager.LoadSceneAsync("Game");
                var match=UnityEngine.Object.FindAnyObjectByType<MatchController>();yield return Until(()=>match.OnlineConnected);
                var join=peer.Emit("queue:join",JsonValue.FromObject(("mode",JsonValue.FromString("ranked")),("name",JsonValue.FromString("Queue Tester"))));
                var deadline=Time.realtimeSinceStartup+25;
                while((!join.IsCompleted||match.Game==null)&&Time.realtimeSinceStartup<deadline){while(actions.Count>0)actions.Dequeue()();yield return null;}
                Assert.IsTrue(join.IsCompleted&&!join.IsFaulted);Assert.NotNull(match.Game);Assert.AreEqual(2,match.Game.Players.Count);
                var online=(OnlineSession)typeof(MatchController).GetField("_online",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(match);
                var client=(SocketIoClient)typeof(OnlineSession).GetField("_socket",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(online);
                var leave=client.Emit("lobby:leave",JsonValue.FromObject());yield return Until(()=>leave.IsCompleted);
                match.ReturnToLobby();yield return null;
            } finally {peer.Dispose();}
        }
    }
}
