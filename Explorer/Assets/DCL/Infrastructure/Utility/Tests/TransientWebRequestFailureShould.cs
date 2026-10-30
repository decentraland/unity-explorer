using NUnit.Framework;
using Utility.Networking;

namespace Utility.Tests
{
    public class TransientWebRequestFailureShould
    {
        [TestCase(408)]
        [TestCase(429)]
        [TestCase(500)]
        [TestCase(502)]
        [TestCase(503)]
        [TestCase(504)]
        [TestCase(521)]
        [TestCase(524)]
        [TestCase(525)]
        public void TreatRetryableResponseCodesAsTransient(long responseCode)
        {
            Assert.IsTrue(TransientWebRequestFailure.IsTransient(responseCode, "HTTP/1.1 " + responseCode));
        }

        [TestCase(400)]
        [TestCase(401)]
        [TestCase(403)]
        [TestCase(404)]
        [TestCase(501)]
        [TestCase(507)]
        public void TreatDefinitiveResponseCodesAsPermanent(long responseCode)
        {
            Assert.IsFalse(TransientWebRequestFailure.IsTransient(responseCode, "HTTP/1.1 " + responseCode));
        }

        [TestCase("Cannot connect to destination host")]
        [TestCase("Unable to complete SSL connection")]
        [TestCase("SSL CA certificate error")]
        public void TreatConnectionFailuresWithoutResponseAsTransient(string message)
        {
            Assert.IsTrue(TransientWebRequestFailure.IsTransient(0, message));
        }

        [Test]
        public void TreatSslFailureAsTransientWhateverTheCode()
        {
            Assert.IsTrue(TransientWebRequestFailure.IsTransient(404, "Unable to complete SSL connection"));
        }

        [TestCase(0, null)]
        [TestCase(0, "Request timeout")]
        [TestCase(404, "Cannot connect to destination host")]
        public void KeepOtherFailuresPermanent(long responseCode, string? message)
        {
            Assert.IsFalse(TransientWebRequestFailure.IsTransient(responseCode, message));
        }
    }
}
