using DCL.Web3.Authenticators;
using DCL.WebRequests;
using NUnit.Framework;
using System;

namespace DCL.AuthenticationScreenFlow.Tests
{
    [TestFixture]
    public class DappDeepLinkAuthenticatorShould
    {
        private const string VERIFIER = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        private const string IDENTITY_ID = "f0963d93-f520-4612-9714-8d0d30f7ae6d";

        [Test]
        public void AttachTheVerifierToTheRetrievalHeaders()
        {
            using WebRequestHeadersInfo headers = DappDeepLinkAuthenticator.CreateRetrievalHeaders(VERIFIER);

            Assert.AreEqual(VERIFIER, headers.HeaderOrNull(DeepLinkSignInVerifier.HEADER));
        }

        [TestCase(401, DeeplinkSigninRetrievalException.ErrorReason.VerifierMismatch)]
        [TestCase(403, DeeplinkSigninRetrievalException.ErrorReason.IpMismatch)]
        [TestCase(404, DeeplinkSigninRetrievalException.ErrorReason.NotFound)]
        [TestCase(410, DeeplinkSigninRetrievalException.ErrorReason.Expired)]
        public void MapRetrievalStatusToReason(long responseCode, DeeplinkSigninRetrievalException.ErrorReason expectedReason)
        {
            Exception exception = DappDeepLinkAuthenticator.MapRetrievalException(responseCode, IDENTITY_ID, new Exception());

            Assert.AreEqual(expectedReason, ((DeeplinkSigninRetrievalException)exception).Reason);
        }

        [Test]
        public void KeepTheOriginalExceptionForOtherStatuses()
        {
            var original = new Exception();

            Assert.AreSame(original, DappDeepLinkAuthenticator.MapRetrievalException(500, IDENTITY_ID, original));
        }
    }
}
