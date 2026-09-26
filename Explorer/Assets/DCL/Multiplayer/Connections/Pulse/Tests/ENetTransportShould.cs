using DCL.Multiplayer.Connections.Pulse.ENet;
using NUnit.Framework;

namespace DCL.Multiplayer.Connections.Pulse.Tests
{
    [TestFixture]
    public class ENetTransportShould
    {
        [Test]
        public void NotThrowWhenTimeoutTeardownRunsAfterPeerWasCleared()
        {
            // A fresh transport matches the state FinalizeHost() leaves behind: no peer, no host, loop inactive
            var transport = new ENetTransport(new ENetTransportOptions(), new MessagePipe());

            Assert.DoesNotThrow(() => transport.ForceDisconnectAsync().GetAwaiter().GetResult());
        }
    }
}
