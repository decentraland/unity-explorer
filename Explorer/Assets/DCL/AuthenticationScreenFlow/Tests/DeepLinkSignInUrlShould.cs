using DCL.Web3;
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
                Web3Address.FromUntrusted("0x24E5F44999C151F08609F8E27B2238C773C4D020"));

            Assert.AreEqual(
                $"{BASE_URL}/{REQUEST_ID}?loginMethod={LOGIN_METHOD}&flow=deeplink&referrer=0x24e5f44999c151f08609f8e27b2238c773c4d020",
                url);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not-an-address")]
        [TestCase("0x123")]
        [TestCase("javascript:alert(1)")]
        public void OmitReferrerWhenInvalid(string? rawReferrer)
        {
            // FromUntrusted degrades every invalid value to null, matching how the
            // authenticator constructs the field.
            string url = DeepLinkSignInUrl.Build(BASE_URL, REQUEST_ID, LOGIN_METHOD, bridgeOnly: false, Web3Address.FromUntrusted(rawReferrer));

            StringAssert.DoesNotContain("referrer", url);
        }

        [Test]
        public void OmitReferrerWhenAddressWasBuiltUnvalidated()
        {
            // Defense-in-depth: even a Web3Address constructed directly from garbage
            // (the ctor does not validate) must not reach the URL.
            string url = DeepLinkSignInUrl.Build(BASE_URL, REQUEST_ID, LOGIN_METHOD, bridgeOnly: false, new Web3Address("not-an-address"));

            StringAssert.DoesNotContain("referrer", url);
        }
    }
}
