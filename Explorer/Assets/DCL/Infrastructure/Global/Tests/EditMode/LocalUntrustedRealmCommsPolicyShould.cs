using Global.Dynamic;
using NUnit.Framework;

namespace Global.Dynamic.Tests
{
    public class LocalUntrustedRealmCommsPolicyShould
    {
        [TestCase(true, "https://localhost:443", true)]
        [TestCase(true, "http://127.0.0.1:8080", true)]
        [TestCase(true, "https://[::1]:443", true)]
        [TestCase(false, "https://localhost:443", false)]
        [TestCase(true, "https://seed.dcl.eth", false)]
        [TestCase(true, "https://127.0.0.1.example", false)]
        public void EnableDirectIceOnlyForOptedInLoopbackRealms(bool acceptUntrustedRealm, string realmUrl, bool expected)
        {
            Assert.AreEqual(expected, LocalUntrustedRealmCommsPolicy.ShouldUseTransportAll(acceptUntrustedRealm, realmUrl));
        }
    }
}
