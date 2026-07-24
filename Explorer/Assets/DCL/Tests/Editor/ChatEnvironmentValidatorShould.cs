using DCL.Chat.Commands;
using DCL.Multiplayer.Connections.DecentralandUrls;
using NUnit.Framework;

namespace DCL.Tests.Editor
{
    public class ChatEnvironmentValidatorShould
    {
        [TestCase("https://peer.decentraland.org", true)]
        [TestCase("https://worlds-content-server.decentraland.org/world/x.dcl.eth", true)]
        [TestCase("https://evil.example/org", false)]              // substring "org" no longer passes
        [TestCase("https://decentraland.org.attacker.com", false)] // suffix-spoof
        [TestCase("https://attacker-decentraland.org", false)]     // not a real subdomain boundary
        [TestCase("https://decentraland.org@evil.com", false)]      // userinfo spoof — real host is evil.com
        [TestCase("https://decentraland.org:1234@evil.com", false)] // colon-before-@ userinfo spoof (real host evil.com)
        [TestCase("https://peer.decentraland.org:443", true)]       // legit host with an explicit port
        public void ValidateOrgBySuffix(string realm, bool expectSuccess)
        {
            var validator = new ChatEnvironmentValidator(DecentralandEnvironment.Org);
            Assert.AreEqual(expectSuccess, validator.ValidateTeleport(realm).Success, realm);
        }

        [TestCase("https://peer.decentraland.zone", true)]
        [TestCase("https://evil.example/zone", false)]
        public void ValidateZoneBySuffix(string realm, bool expectSuccess)
        {
            var validator = new ChatEnvironmentValidator(DecentralandEnvironment.Zone);
            Assert.AreEqual(expectSuccess, validator.ValidateTeleport(realm).Success, realm);
        }
    }
}
