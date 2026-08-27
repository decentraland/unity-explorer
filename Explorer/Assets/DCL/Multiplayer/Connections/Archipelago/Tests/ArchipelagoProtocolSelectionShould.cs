using DCL.Multiplayer.Connections.Archipelago.AdapterAddress;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using NUnit.Framework;
using System;

namespace DCL.Multiplayer.Connections.Archipelago.Tests
{
    /// <summary>
    ///     Pins which room a realm's comms adapter is served by, refiner included, since the two are only
    ///     correct together: the refiner decides where the url starts and the fork reads the scheme it starts
    ///     with. The cleartext tier is the one an operator can widen, so its boundary is held here in full.
    /// </summary>
    public class ArchipelagoProtocolSelectionShould
    {
        private const bool ACCEPTED = true;
        private const bool NOT_ACCEPTED = false;

        private static readonly RefinedAdapterAddresses REFINER = new ();

        // One case each rather than a parameterised expectation: AdapterProtocol is internal, so it cannot
        // appear in a public test signature.
        [Test]
        public void ServeAWssAdapterAsAnArchipelagoRoom() =>
            Assert.AreEqual(ForkGlobalRealmRoom.AdapterProtocol.Archipelago,
                ProtocolFor("archipelago:archipelago:wss://archipelago-ea-stats.decentraland.org", NOT_ACCEPTED));

        [Test]
        public void ServeAnHttpsAdapterAsAFixedRoom() =>
            Assert.AreEqual(ForkGlobalRealmRoom.AdapterProtocol.Fixed,
                ProtocolFor("fixed-adapter:signed-login:https://comms-gatekeeper.decentraland.org/get-scene-adapter", NOT_ACCEPTED));

        [Test]
        public void ServeAnOfflineAdapterAsNoRoom() =>
            Assert.AreEqual(ForkGlobalRealmRoom.AdapterProtocol.Offline,
                ProtocolFor("offline:offline", NOT_ACCEPTED));

        [TestCase("fixed-adapter:signed-login:http://127.0.0.1:8080/comms")]
        [TestCase("fixed-adapter:signed-login:http://localhost:8080/comms")]
        [TestCase("fixed-adapter:signed-login:http://[::1]:8080/comms")]
        public void ServeAnOptedInLoopbackFixtureAsAFixedRoom(string commsAdapter) =>
            Assert.AreEqual(ForkGlobalRealmRoom.AdapterProtocol.Fixed, ProtocolFor(commsAdapter, ACCEPTED));

        [Test]
        public void RefuseALoopbackFixtureWithoutTheOptIn() =>
            Assert.Throws<InvalidOperationException>(
                () => ProtocolFor("fixed-adapter:signed-login:http://127.0.0.1:8080/comms", NOT_ACCEPTED));

        // The opt-in widens cleartext for this machine only: a remote host — including one named to read as
        // loopback — still has no tier to fall into, so the connection fails instead of going out in the clear.
        [TestCase("fixed-adapter:signed-login:http://fixture.attacker.example/comms")]
        [TestCase("fixed-adapter:signed-login:http://127.0.0.1.attacker.example/comms")]
        [TestCase("fixed-adapter:signed-login:http://127.0.0.1@attacker.example/comms")]
        [TestCase("fixed-adapter:signed-login:http://attacker.example/?next=http://127.0.0.1")]
        [TestCase("nonsense")]
        public void RefuseEveryOtherAdapterEvenWithTheOptIn(string commsAdapter) =>
            Assert.Throws<InvalidOperationException>(() => ProtocolFor(commsAdapter, ACCEPTED));

        private static ForkGlobalRealmRoom.AdapterProtocol ProtocolFor(string commsAdapter, bool allowInsecureLocalHttp) =>
            ForkGlobalRealmRoom.ProtocolFor(REFINER.AdapterUrlAsync(commsAdapter), allowInsecureLocalHttp);
    }
}
