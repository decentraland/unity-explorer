using NUnit.Framework;
using Utility.Networking;

namespace Utility.Tests
{
    /// <summary>
    ///     Pins the predicate every "this endpoint is local" gate reads. A false positive here hands a remote
    ///     host whatever the gate reading it relaxes.
    /// </summary>
    public class LoopbackUrlsShould
    {
        [TestCase("http://localhost")]
        [TestCase("http://localhost/comms")]
        [TestCase("http://localhost:8080/comms")]
        [TestCase("http://LOCALHOST:8080")]
        [TestCase("HTTP://localhost")]
        [TestCase("http://127.0.0.1")]
        [TestCase("http://127.0.0.1:8080/comms?a=b")]
        [TestCase("http://127.0.0.1#fragment")]
        [TestCase("http://[::1]")]
        [TestCase("http://[::1]:8080/comms")]
        // The host is what counts, so credentials in front of a loopback host do not disqualify it.
        [TestCase("http://user:pass@127.0.0.1:8080/comms")]
        public void MatchCleartextLoopback(string url) =>
            Assert.IsTrue(LoopbackUrls.IsLoopbackHttpUrl(url), url);

        [TestCase("ws://localhost")]
        [TestCase("ws://localhost:8080/comms")]
        [TestCase("WS://LOCALHOST:8080")]
        [TestCase("ws://127.0.0.1:8080/comms?a=b")]
        [TestCase("ws://[::1]:8080/comms")]
        public void MatchCleartextLoopbackWebSocket(string url) =>
            Assert.IsTrue(LoopbackUrls.IsLoopbackWsUrl(url), url);

        // A remote host wearing a loopback host's name.
        [TestCase("http://127.0.0.1.attacker.example/comms")]
        [TestCase("http://localhost.attacker.example")]
        [TestCase("http://127.0.0.1@attacker.example/comms")]
        [TestCase("http://localhost@attacker.example")]
        // A loopback host that is only mentioned past the authority.
        [TestCase("http://attacker.example/@127.0.0.1")]
        [TestCase("http://attacker.example/?next=http://127.0.0.1")]
        [TestCase("http://attacker.example#http://localhost")]
        // Neither an unrelated address nor the rest of 127.0.0.0/8 is loopback here.
        [TestCase("http://192.168.1.10:8080")]
        [TestCase("http://127.0.0.2:8080")]
        [TestCase("http://0.0.0.0:8080")]
        // Another scheme, or no url at all.
        [TestCase("https://127.0.0.1:8080/comms")]
        [TestCase("ws://127.0.0.1:8080")]
        [TestCase("ftp://127.0.0.1")]
        [TestCase("")]
        [TestCase("localhost:8080")]
        [TestCase("http://")]
        [TestCase("http:///comms")]
        // Only an absolute url qualifies: an adapter address has to be refined before it is read.
        [TestCase("fixed-adapter:signed-login:http://127.0.0.1:8080/comms")]
        public void RejectEverythingElseAsCleartextLoopback(string url) =>
            Assert.IsFalse(LoopbackUrls.IsLoopbackHttpUrl(url), url);

        [TestCase("wss://127.0.0.1:8080/comms")]
        [TestCase("http://127.0.0.1:8080/comms")]
        [TestCase("ws://127.0.0.1.attacker.example/comms")]
        [TestCase("ws://127.0.0.1@attacker.example/comms")]
        [TestCase("ws://attacker.example/?next=ws://127.0.0.1")]
        [TestCase("ftp://127.0.0.1")]
        [TestCase("")]
        [TestCase("localhost:8080")]
        [TestCase("ws://")]
        [TestCase("ws:///comms")]
        [TestCase("archipelago:archipelago:ws://127.0.0.1:8080/comms")]
        public void RejectEverythingElseAsCleartextLoopbackWebSocket(string url) =>
            Assert.IsFalse(LoopbackUrls.IsLoopbackWsUrl(url), url);

        [TestCase("https://localhost")]
        [TestCase("https://127.0.0.1:8080/comms")]
        [TestCase("https://[::1]")]
        [TestCase("http://127.0.0.1:8080")]
        public void MatchLoopbackUnderEitherWebScheme(string url) =>
            Assert.IsTrue(LoopbackUrls.IsLoopbackWebUrl(url), url);

        [TestCase("https://127.0.0.1.attacker.example")]
        [TestCase("https://attacker.example")]
        [TestCase("ftp://127.0.0.1")]
        [TestCase("file:///etc/passwd")]
        [TestCase("not-a-url")]
        public void RejectEverythingElseAsLoopbackWeb(string url) =>
            Assert.IsFalse(LoopbackUrls.IsLoopbackWebUrl(url), url);

        // The host tier on its own, as Uri.Host hands it over — IPv6 bracketed, no port, no userinfo.
        [TestCase("localhost", true)]
        [TestCase("LocalHost", true)]
        [TestCase("127.0.0.1", true)]
        [TestCase("[::1]", true)]
        [TestCase("::1", false)]
        [TestCase("127.0.0.1:8080", false)]
        [TestCase("127.0.0.1.attacker.example", false)]
        [TestCase("attacker.example", false)]
        [TestCase("", false)]
        public void ClassifyAHostOnItsOwn(string host, bool expected) =>
            Assert.AreEqual(expected, LoopbackUrls.IsLoopbackHost(host), host);
    }
}
