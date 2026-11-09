using DCL.Web3.Authenticators;
using NUnit.Framework;
using System.Text.RegularExpressions;

namespace DCL.AuthenticationScreenFlow.Tests
{
    [TestFixture]
    public class DeepLinkSignInVerifierShould
    {
        // Shared with the auth server and the auth web app tests.
        private const string VERIFIER = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        private const string DERIVED_AUTH_REQUEST_ID = "13d31e96-1a1a-48ec-af16-b10c4c982e08";

        private static readonly Regex BASE64_URL_VERIFIER = new ("^[A-Za-z0-9_-]{43}$");
        private static readonly Regex UUID_V4 = new ("^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$");

        [Test]
        public void DeriveTheSharedTestVector()
        {
            Assert.AreEqual(DERIVED_AUTH_REQUEST_ID, DeepLinkSignInVerifier.DeriveAuthRequestId(VERIFIER));
        }

        [Test]
        public void CreateUnpaddedBase64UrlVerifier()
        {
            string verifier = DeepLinkSignInVerifier.CreateVerifier();

            Assert.IsTrue(BASE64_URL_VERIFIER.IsMatch(verifier), verifier);
        }

        [Test]
        public void CreateDistinctVerifiers()
        {
            Assert.AreNotEqual(DeepLinkSignInVerifier.CreateVerifier(), DeepLinkSignInVerifier.CreateVerifier());
        }

        [Test]
        public void DeriveUuidV4()
        {
            string authRequestId = DeepLinkSignInVerifier.DeriveAuthRequestId(DeepLinkSignInVerifier.CreateVerifier());

            Assert.IsTrue(UUID_V4.IsMatch(authRequestId), authRequestId);
        }
    }
}
