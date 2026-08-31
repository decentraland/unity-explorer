using DCL.Chat.Commands;
using DCL.Multiplayer.Connections.DecentralandUrls;
using NSubstitute;
using NUnit.Framework;

namespace DCL.Tests.Editor
{
    public class ChatEnvironmentValidatorShould
    {
        private const string CUSTOM_DOMAIN = "interconnected.online";
        private const string LOOPBACK_GATEWAY = "http://127.0.0.1:8080/";

        // Org
        [TestCase(IDecentralandUrlsSource.ORG_DOMAIN, "https://peer.decentraland.org", true)]
        [TestCase(IDecentralandUrlsSource.ORG_DOMAIN, "https://worlds-content-server.decentraland.org/world/x.dcl.eth", true)]
        [TestCase(IDecentralandUrlsSource.ORG_DOMAIN, "https://peer.decentraland.org:443", true)]       // legit host with an explicit port
        [TestCase(IDecentralandUrlsSource.ORG_DOMAIN, "https://evil.example/org", false)]              // substring "org" does not pass
        [TestCase(IDecentralandUrlsSource.ORG_DOMAIN, "https://decentraland.org.attacker.com", false)] // suffix-spoof
        [TestCase(IDecentralandUrlsSource.ORG_DOMAIN, "https://attacker-decentraland.org", false)]     // not a real subdomain boundary
        [TestCase(IDecentralandUrlsSource.ORG_DOMAIN, "https://decentraland.org@evil.com", false)]      // userinfo spoof — real host is evil.com
        [TestCase(IDecentralandUrlsSource.ORG_DOMAIN, "https://decentraland.org:1234@evil.com", false)] // colon-before-@ userinfo spoof (real host evil.com)

        // Zone
        [TestCase(IDecentralandUrlsSource.ZONE_DOMAIN, "https://peer.decentraland.zone", true)]
        [TestCase(IDecentralandUrlsSource.ZONE_DOMAIN, "https://evil.example/zone", false)]
        [TestCase(IDecentralandUrlsSource.ZONE_DOMAIN, "https://peer.decentraland.org", false)] // another environment's domain is foreign

        // A --base-domain deployment: its own domain is accepted, decentraland's is as foreign to it as it is to them
        [TestCase(CUSTOM_DOMAIN, "https://" + CUSTOM_DOMAIN, true)]
        [TestCase(CUSTOM_DOMAIN, "https://peer." + CUSTOM_DOMAIN, true)]
        [TestCase(CUSTOM_DOMAIN, "https://worlds-content-server." + CUSTOM_DOMAIN + "/world/x.dcl.eth", true)]
        [TestCase(CUSTOM_DOMAIN, "https://peer.decentraland.org", false)]
        [TestCase(CUSTOM_DOMAIN, "https://" + CUSTOM_DOMAIN + ".attacker.com", false)] // suffix-spoof
        [TestCase(CUSTOM_DOMAIN, "https://" + CUSTOM_DOMAIN + "@evil.com", false)]     // userinfo spoof
        public void AcceptOnlyRealmsUnderTheBaseDomain(string baseDomain, string realm, bool expectSuccess)
        {
            var validator = new ChatEnvironmentValidator(UrlsSourceWithBaseDomain(baseDomain));
            Assert.AreEqual(expectSuccess, validator.ValidateTeleport(realm).Success, realm);
        }

        /// <summary>
        ///     A --gateway session routes every supported service, realms included, through one origin that is
        ///     allowed to sit outside the base domain: a local e2e fixture's gateway is loopback. Those realms are
        ///     this session's own, so they must pass while everything off that origin is judged as before.
        /// </summary>
        // Local fixture: the gateway origin is nowhere near decentraland.org, and its realms are still ours
        [TestCase(IDecentralandUrlsSource.ORG_DOMAIN, LOOPBACK_GATEWAY, LOOPBACK_GATEWAY + "worlds-content-server/world/x.dcl.eth", true)]
        [TestCase(IDecentralandUrlsSource.ORG_DOMAIN, LOOPBACK_GATEWAY, LOOPBACK_GATEWAY + "realm-provider-ea/main", true)]
        // The un-gatewayed domain keeps working in the same session
        [TestCase(IDecentralandUrlsSource.ORG_DOMAIN, LOOPBACK_GATEWAY, "https://peer.decentraland.org", true)]
        // A gateway does not make foreign realms reachable
        [TestCase(IDecentralandUrlsSource.ORG_DOMAIN, LOOPBACK_GATEWAY, "https://evil.example/world/x.dcl.eth", false)]
        // The origin's trailing '/' is the authority boundary: this host merely starts with the same characters
        [TestCase(IDecentralandUrlsSource.ORG_DOMAIN, LOOPBACK_GATEWAY, "http://127.0.0.1:8080.attacker.com/world/x.dcl.eth", false)]
        // Remote fixture (--base-domain plus a gateway on that same domain)
        [TestCase(CUSTOM_DOMAIN, "https://" + CUSTOM_DOMAIN + "/", "https://" + CUSTOM_DOMAIN + "/worlds-content-server/world/x.dcl.eth", true)]
        // Derived gateway subdomain, as the use-gateway flag builds it
        [TestCase(CUSTOM_DOMAIN, "https://gateway." + CUSTOM_DOMAIN + "/", "https://gateway." + CUSTOM_DOMAIN + "/worlds-content-server/world/x.dcl.eth", true)]
        public void AcceptRealmsServedByTheGatewayOrigin(string baseDomain, string gatewayOrigin, string realm, bool expectSuccess)
        {
            var validator = new ChatEnvironmentValidator(UrlsSourceWithBaseDomain(baseDomain, gatewayOrigin));
            Assert.AreEqual(expectSuccess, validator.ValidateTeleport(realm).Success, realm);
        }

        /// <summary>
        ///     <paramref name="gatewayOrigin" /> defaults to null, which is what a session with no gateway routing
        ///     reports — the cases above it therefore cover the un-gatewayed behaviour unchanged.
        /// </summary>
        private static IDecentralandUrlsSource UrlsSourceWithBaseDomain(string baseDomain, string? gatewayOrigin = null)
        {
            IDecentralandUrlsSource urlsSource = Substitute.For<IDecentralandUrlsSource>();
            urlsSource.BaseDomain.Returns(baseDomain);
            urlsSource.GatewayOrigin.Returns(gatewayOrigin);
            return urlsSource;
        }
    }
}
