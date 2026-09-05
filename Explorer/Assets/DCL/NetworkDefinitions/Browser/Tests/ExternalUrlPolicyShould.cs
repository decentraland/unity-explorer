using NUnit.Framework;
using System;

namespace DCL.Browser
{
    public class ExternalUrlPolicyShould
    {
        [TestCase("https://decentraland.org", true)]
        [TestCase("http://example.com", true)]
        [TestCase("smb://attacker/share", false)]
        [TestCase("file:///etc/passwd", false)]
        [TestCase("decentraland://?creator-hub-bin-path=x", false)]
        [TestCase("mailto:a@b.com", false)]
        [TestCase("steam://run/1", false)]
        [TestCase("not a url", false)]
        public void ClassifyScheme(string url, bool expected) =>
            Assert.AreEqual(expected, ExternalUrlPolicy.IsWebScheme(url));

        [Test]
        public void RefuseEmptyHostTrustKey()
        {
            var fileUri = new Uri("file:///x");
            Assert.IsFalse(ExternalUrlPolicy.TryGetTrustKey(fileUri, out _), "empty-host URIs must never be cacheable");
        }

        [Test]
        public void TrustKeyIsSchemeAndHost()
        {
            Assert.IsTrue(ExternalUrlPolicy.TryGetTrustKey(new Uri("https://decentraland.org/x"), out string key));
            Assert.AreEqual("https|decentraland.org", key);
        }
    }
}
