using DCL.Chat.Commands;
using DCL.Multiplayer.Connections.DecentralandUrls;
using NSubstitute;
using NUnit.Framework;

namespace DCL.Tests.Editor
{
    public class ChatEnvironmentValidatorShould
    {
        private const string CUSTOM_DOMAIN = "interconnected.online";

        // Org
        [TestCase(DecentralandEnvironment.Org, IDecentralandUrlsSource.ORG_DOMAIN, "https://peer.decentraland.org", true)]
        [TestCase(DecentralandEnvironment.Org, IDecentralandUrlsSource.ORG_DOMAIN, "https://worlds-content-server.decentraland.org/world/x.dcl.eth", true)]
        [TestCase(DecentralandEnvironment.Org, IDecentralandUrlsSource.ORG_DOMAIN, "https://peer.decentraland.org:443", true)]       // legit host with an explicit port
        [TestCase(DecentralandEnvironment.Org, IDecentralandUrlsSource.ORG_DOMAIN, "https://evil.example/org", false)]              // substring "org" does not pass
        [TestCase(DecentralandEnvironment.Org, IDecentralandUrlsSource.ORG_DOMAIN, "https://decentraland.org.attacker.com", false)] // suffix-spoof
        [TestCase(DecentralandEnvironment.Org, IDecentralandUrlsSource.ORG_DOMAIN, "https://attacker-decentraland.org", false)]     // not a real subdomain boundary
        [TestCase(DecentralandEnvironment.Org, IDecentralandUrlsSource.ORG_DOMAIN, "https://decentraland.org@evil.com", false)]      // userinfo spoof — real host is evil.com
        [TestCase(DecentralandEnvironment.Org, IDecentralandUrlsSource.ORG_DOMAIN, "https://decentraland.org:1234@evil.com", false)] // colon-before-@ userinfo spoof (real host evil.com)

        // Zone
        [TestCase(DecentralandEnvironment.Zone, IDecentralandUrlsSource.ZONE_DOMAIN, "https://peer.decentraland.zone", true)]
        [TestCase(DecentralandEnvironment.Zone, IDecentralandUrlsSource.ZONE_DOMAIN, "https://evil.example/zone", false)]
        [TestCase(DecentralandEnvironment.Zone, IDecentralandUrlsSource.ZONE_DOMAIN, "https://peer.decentraland.org", false)] // another environment's domain is foreign

        // A --base-domain deployment: its own domain is accepted, decentraland's is as foreign to it as it is to them
        [TestCase(DecentralandEnvironment.Custom, CUSTOM_DOMAIN, "https://" + CUSTOM_DOMAIN, true)]
        [TestCase(DecentralandEnvironment.Custom, CUSTOM_DOMAIN, "https://peer." + CUSTOM_DOMAIN, true)]
        [TestCase(DecentralandEnvironment.Custom, CUSTOM_DOMAIN, "https://worlds-content-server." + CUSTOM_DOMAIN + "/world/x.dcl.eth", true)]
        [TestCase(DecentralandEnvironment.Custom, CUSTOM_DOMAIN, "https://peer.decentraland.org", false)]
        [TestCase(DecentralandEnvironment.Custom, CUSTOM_DOMAIN, "https://" + CUSTOM_DOMAIN + ".attacker.com", false)] // suffix-spoof
        [TestCase(DecentralandEnvironment.Custom, CUSTOM_DOMAIN, "https://" + CUSTOM_DOMAIN + "@evil.com", false)]     // userinfo spoof
        public void AcceptOnlyRealmsUnderTheBaseDomain(DecentralandEnvironment environment, string baseDomain, string realm, bool expectSuccess)
        {
            var validator = new ChatEnvironmentValidator(environment, UrlsSourceWithBaseDomain(baseDomain));
            Assert.AreEqual(expectSuccess, validator.ValidateTeleport(realm).Success, realm);
        }

        /// <summary>
        ///     Today is the one environment that cannot follow a realm change at all, so even a host under its own
        ///     base domain is rejected.
        /// </summary>
        [TestCase("https://peer.decentraland.today")]
        [TestCase("https://peer.decentraland.org")]
        public void RejectEveryRealmInTheTodayEnvironment(string realm)
        {
            var validator = new ChatEnvironmentValidator(DecentralandEnvironment.Today, UrlsSourceWithBaseDomain(IDecentralandUrlsSource.TODAY_DOMAIN));
            Assert.IsFalse(validator.ValidateTeleport(realm).Success, realm);
        }

        private static IDecentralandUrlsSource UrlsSourceWithBaseDomain(string baseDomain)
        {
            IDecentralandUrlsSource urlsSource = Substitute.For<IDecentralandUrlsSource>();
            urlsSource.BaseDomain.Returns(baseDomain);
            return urlsSource;
        }
    }
}
