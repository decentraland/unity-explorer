using DCL.ChangeRealmPrompt;
using NUnit.Framework;

namespace DCL.Tests.Editor
{
    public class ChangeRealmPromptDestinationHostShould
    {
        [TestCase("https://evil.com/path", "evil.com")]
        [TestCase("https://evil.com:8080/path", "evil.com:8080")]        // port kept in the displayed authority
        [TestCase("https://decentraland.org@evil.com", "evil.com")]       // userinfo stripped — real host shown
        [TestCase("https://user:pass@evil.com:443/x", "evil.com:443")]    // userinfo + port
        [TestCase("world-name", "world-name")]                            // no scheme → shown unchanged
        [TestCase("https://host?q=1", "host")]                            // query stripped
        [TestCase("https://host#frag", "host")]                           // fragment stripped
        public void ExtractsTrueHostForDisplay(string realm, string expected)
        {
            Assert.AreEqual(expected, ChangeRealmPromptController.DestinationHostFor(realm), realm);
        }
    }
}
