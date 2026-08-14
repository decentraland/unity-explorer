using CommunicationData.URLHelpers;
using DCL.Browser.DecentralandUrls;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utility;
using Global.Dynamic;
using NUnit.Framework;

namespace Global.Tests.EditMode
{
    public class RealmControllerShould
    {
        private static readonly URLDomain MAIN_REALM = URLDomain.FromString("https://realm-provider-ea.decentraland.org/main");

        [TestCase(DecentralandEnvironment.Org, null, "realm-provider.decentraland.org")]
        [TestCase(DecentralandEnvironment.Zone, null, "realm-provider.decentraland.org")]                             // every decentraland environment shares the single org realm provider
        [TestCase(DecentralandEnvironment.Org, "interconnected.online", "realm-provider.interconnected.online")]      // a custom base domain hosts its own realm provider
        [TestCase(DecentralandEnvironment.Org, "decentraland.attacker.com", "realm-provider.decentraland.attacker.com")] // spoof-shaped domain must not classify as an environment
        public void DeriveMainRealmCommsHostnameFromHostDomain(DecentralandEnvironment environment, string? customBaseDomain, string expectedHostname)
        {
            //Arrange
            var urlsSource = DecentralandUrlsSource.CreateForTest(environment, ILaunchMode.PLAY, customBaseDomain);

            //Act
            string hostname = RealmController.ResolveHostname(MAIN_REALM, MainRealmAbout(), urlsSource);

            //Assert
            Assert.AreEqual(expectedHostname, hostname);
        }

        [Test]
        public void PassRealmHostThroughWhenAboutHasComms()
        {
            //Arrange
            var urlsSource = DecentralandUrlsSource.CreateForTest(DecentralandEnvironment.Org, ILaunchMode.PLAY);
            var about = new ServerAbout(comms: new CommsInfo());

            //Act
            string hostname = RealmController.ResolveHostname(URLDomain.FromString("https://peer.decentraland.org"), about, urlsSource);

            //Assert
            Assert.AreEqual("peer.decentraland.org", hostname);
        }

        private static ServerAbout MainRealmAbout()
        {
            // comms == null and a non-ENS realm name select the main-realm fallback branch.
            var about = new ServerAbout();
            about.configurations.realmName = "main";
            return about;
        }
    }
}
