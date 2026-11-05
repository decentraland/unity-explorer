using NUnit.Framework;

namespace DCL.WebRequests.Tests
{
    // Pins the ADR-44 payload format. The signature has to cover the metadata bytes exactly as the
    // request delivers them in `x-identity-metadata`: folding the whole payload, as this did before,
    // left the metadata's casing outside the signature, so a key or value could be re-cased between
    // signing and delivery and still verify while services read the delivered header.
    public class WebRequestSignInfoShould
    {
        private const ulong TIMESTAMP = 1700000000000;

        [Test]
        public void LowercaseTheMethodAndPath()
        {
            WebRequestSignInfo info = WebRequestSignInfo.NewFromRaw("{}", "https://example.com/Api/Quote", TIMESTAMP, "POST");

            Assert.That(info.StringToSign, Is.EqualTo($"post:/api/quote:{TIMESTAMP}:{{}}"));
        }

        [Test]
        public void LeaveTheMetadataVerbatim()
        {
            const string METADATA = "{\"realmName\":\"main\",\"sceneId\":\"QmAbC\",\"isGuest\":false}";

            WebRequestSignInfo info = WebRequestSignInfo.NewFromRaw(METADATA, "https://example.com/", TIMESTAMP, "get");

            Assert.That(info.StringToSign, Does.EndWith(METADATA));
        }

        [Test]
        public void DistinguishMetadataDifferingOnlyInCase()
        {
            // Under the previous fold these collapsed to one string, which is what let a re-spelled
            // field ride an otherwise valid signature.
            WebRequestSignInfo folded = WebRequestSignInfo.NewFromRaw("{\"realmname\":\"main\"}", "https://example.com/", TIMESTAMP, "get");
            WebRequestSignInfo camel = WebRequestSignInfo.NewFromRaw("{\"realmName\":\"main\"}", "https://example.com/", TIMESTAMP, "get");

            Assert.That(folded.StringToSign, Is.Not.EqualTo(camel.StringToSign));
        }

        [Test]
        public void DefaultToAnEmptyObject_WhenThereIsNothingToSign()
        {
            // What `NewFromUrl` relies on: a request with no metadata still signs "{}", which is the
            // literal the header carries, so both formats agree for those requests.
            WebRequestSignInfo info = WebRequestSignInfo.NewFromUrl("https://example.com/status", TIMESTAMP, "GET");

            Assert.That(info.StringToSign, Is.EqualTo($"get:/status:{TIMESTAMP}:{{}}"));
        }
    }
}
