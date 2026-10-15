using NUnit.Framework;

namespace DCL.WebRequests.Tests
{
    public class WebRequestHeaderShould
    {
        private const string SECRET = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";

        [TestCase(WebRequestHeader.IDENTITY_VERIFIER)]
        [TestCase("X-Identity-Verifier")]
        public void RedactTheIdentityVerifierValue(string name)
        {
            Assert.AreEqual($"WebRequestHeader({name}: <redacted>)", new WebRequestHeader(name, SECRET).ToString());
        }

        [Test]
        public void PrintOtherHeaderValues()
        {
            Assert.AreEqual("WebRequestHeader(x-identity-timestamp: 123)", new WebRequestHeader("x-identity-timestamp", "123").ToString());
        }
    }
}
