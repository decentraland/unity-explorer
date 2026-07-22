using DCL.Web3.Authenticators;
using NUnit.Framework;

namespace DCL.AuthenticationScreenFlow.Tests
{
    [TestFixture]
    public class DeepLinkSignInUrlShould
    {
        private const string BASE_URL = "https://decentraland.org/auth/requests";
        private const string REQUEST_ID = "req-1";
        private const string LOGIN_METHOD = "METAMASK";

        [Test]
        public void BuildBaseUrlWithoutReferrer()
        {
            string url = DeepLinkSignInUrl.Build(BASE_URL, REQUEST_ID, LOGIN_METHOD, bridgeOnly: false, referrer: null);

            Assert.AreEqual($"{BASE_URL}/{REQUEST_ID}?loginMethod={LOGIN_METHOD}&flow=deeplink", url);
        }

        [Test]
        public void AppendBridgeOnlyFlag()
        {
            string url = DeepLinkSignInUrl.Build(BASE_URL, REQUEST_ID, LOGIN_METHOD, bridgeOnly: true, referrer: null);

            Assert.AreEqual($"{BASE_URL}/{REQUEST_ID}?loginMethod={LOGIN_METHOD}&flow=deeplink&bridgeOnly", url);
        }

        [Test]
        public void AppendLowercasedReferrerWhenValid()
        {
            string url = DeepLinkSignInUrl.Build(BASE_URL, REQUEST_ID, LOGIN_METHOD, bridgeOnly: false,
                referrer: "0x24E5F44999C151F08609F8E27B2238C773C4D020");

            Assert.AreEqual(
                $"{BASE_URL}/{REQUEST_ID}?loginMethod={LOGIN_METHOD}&flow=deeplink&referrer=0x24e5f44999c151f08609f8e27b2238c773c4d020",
                url);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not-an-address")]
        [TestCase("0x123")]
        [TestCase("javascript:alert(1)")]
        public void OmitReferrerWhenInvalid(string? referrer)
        {
            string url = DeepLinkSignInUrl.Build(BASE_URL, REQUEST_ID, LOGIN_METHOD, bridgeOnly: false, referrer);

            StringAssert.DoesNotContain("referrer", url);
        }
    }
}
