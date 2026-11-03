using DCL.Multiplayer.Connections.Pulse.ENet;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;

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

        [Test]
        public async Task ResolveHostFromAThreadWithoutSynchronizationContext()
        {
            // The reconnect loop resolves the host on a thread-pool thread, which carries no SynchronizationContext
            var transport = new ENetTransport(new ENetTransportOptions(), new MessagePipe());

            string resolvedIp = await Task.Run(async () =>
            {
                SynchronizationContext.SetSynchronizationContext(null);
                return await transport.ResolveIPv4Async("localhost", CancellationToken.None);
            });

            Assert.That(resolvedIp, Is.EqualTo("127.0.0.1"));
        }
    }
}
