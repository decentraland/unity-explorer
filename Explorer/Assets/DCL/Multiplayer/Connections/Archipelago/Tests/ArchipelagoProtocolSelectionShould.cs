using NUnit.Framework;

namespace DCL.Multiplayer.Connections.Archipelago.Tests
{
    public class ArchipelagoProtocolSelectionShould
    {
        [TestCase("fixed-adapter:signed-login:http://127.0.0.1:8080/comms", true, true)]
        [TestCase("fixed-adapter:signed-login:http://localhost:8080/comms", true, true)]
        [TestCase("fixed-adapter:signed-login:http://[::1]:8080/comms", true, true)]
        [TestCase("fixed-adapter:signed-login:http://127.0.0.1:8080/comms", false, false)]
        [TestCase("fixed-adapter:signed-login:http://127.0.0.1.attacker.example/comms", true, false)]
        [TestCase("fixed-adapter:signed-login:http://127.0.0.1@attacker.example/comms", true, false)]
        [TestCase("fixed-adapter:signed-login:https://127.0.0.1:8080/comms", true, false)]
        public void AllowInsecureHttpOnlyForExplicitLoopbackOptIn(string adapterUrl, bool allowInsecureLocalHttp, bool expected)
        {
            Assert.AreEqual(expected, ForkGlobalRealmRoom.IsLoopbackHttpAdapter(adapterUrl, allowInsecureLocalHttp));
        }
    }
}
