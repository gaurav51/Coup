using System;
using Coup.Net;
using NUnit.Framework;
namespace Coup.Frontend.Tests
{
    public class FrontendUnitTests
    {
        [TestCase("https://example.com","wss",443)]
        [TestCase("http://localhost:3001","ws",3001)]
        [TestCase("wss://example.com","wss",443)]
        public void SocketUsesExpectedSchemePortAndEngineVersion(string value,string scheme,int port) {
            var uri=SocketIoClient.SocketUri(value);
            Assert.AreEqual(scheme,uri.Scheme);Assert.AreEqual(port,uri.Port);Assert.AreEqual("/socket.io/",uri.AbsolutePath);
            Assert.AreEqual("?EIO=4&transport=websocket",uri.Query);
        }
        [TestCase("file:///tmp/x")][TestCase("not a url")][TestCase("javascript:alert(1)")]
        public void NonNetworkSocketAddressesAreRejected(string value) => Assert.Throws<ArgumentException>(()=>SocketIoClient.SocketUri(value));
        [Test] public void NamesAndPrivatePayloadsPreserveUnicodeAndEscapeControlCharacters() {
            var original="Name \"quoted\" \\ 😀\n\t\b\f\0";
            var encoded=JsonValue.FromObject(("name",JsonValue.FromString(original))).Encode();
            Assert.AreEqual(original,JsonValue.Parse(encoded).Str("name"));
            Assert.IsFalse(encoded.Contains("\0"));
        }
        [Test] public void RawControlsAndTruncatedJsonAreRejected() {
            Assert.Throws<FormatException>(()=>JsonValue.Parse("\"a\nb\""));
            Assert.Throws<FormatException>(()=>JsonValue.Parse("{\"a\": [1,"));
        }
        [Test] public void SeatLaunchDoesNotLeakPreviousQueueOrResumeMode() {
            Coup.Game.MatchLaunch.Queue("ranked");Assert.AreEqual("ranked",Coup.Game.MatchLaunch.QueueMode);
            Coup.Game.MatchLaunch.Join("ABCD");Assert.AreEqual("",Coup.Game.MatchLaunch.QueueMode);
            Assert.IsFalse(Coup.Game.MatchLaunch.ResumeRequested);
            Coup.Game.MatchLaunch.Resume();Assert.IsTrue(Coup.Game.MatchLaunch.ResumeRequested);
            Coup.Game.MatchLaunch.Local();Assert.IsFalse(Coup.Game.MatchLaunch.Online);Assert.IsFalse(Coup.Game.MatchLaunch.ResumeRequested);
        }
    }
}
