using DCL.Multiplayer.Connections.Archipelago.AdapterAddress;
using NUnit.Framework;

namespace DCL.Multiplayer.Connections.Archipelago.Tests
{
    /// <summary>
    ///     The refined address is what the rooms fetch, so it has to be the url alone — the handshake pre-info
    ///     in front of it is not part of any endpoint.
    /// </summary>
    public class RefinedAdapterAddressesShould
    {
        private static readonly RefinedAdapterAddresses REFINER = new ();

        [TestCase("archipelago:archipelago:wss://archipelago-ea-stats.decentraland.org", "wss://archipelago-ea-stats.decentraland.org")]
        [TestCase("fixed-adapter:signed-login:https://comms-gatekeeper.decentraland.org/get-scene-adapter", "https://comms-gatekeeper.decentraland.org/get-scene-adapter")]
        [TestCase("fixed-adapter:signed-login:http://127.0.0.1:8080/comms", "http://127.0.0.1:8080/comms")]
        // Already bare, or carrying no url at all.
        [TestCase("wss://archipelago-ea-stats.decentraland.org", "wss://archipelago-ea-stats.decentraland.org")]
        [TestCase("offline:offline", "offline:offline")]
        // The url's own scheme comes first; a scheme inside its query is part of the url, not the start of it.
        [TestCase("fixed-adapter:signed-login:http://127.0.0.1:8080/comms?next=https://other.example", "http://127.0.0.1:8080/comms?next=https://other.example")]
        public void CutAnAddressDownToItsUrl(string commsAdapter, string expected) =>
            Assert.AreEqual(expected, REFINER.AdapterUrlAsync(commsAdapter));
    }
}
