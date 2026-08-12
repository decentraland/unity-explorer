using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Pulse.ENet;
using NUnit.Framework;
using System.Reflection;
using System.Threading;

namespace DCL.Multiplayer.Connections.Pulse.Tests
{
    [TestFixture]
    public class ENetTransportShould
    {
        [Test]
        public void NotThrowWhenTimeoutTeardownRunsAfterPeerWasCleared()
        {
            var transport = new ENetTransport(new ENetTransportOptions(), new MessagePipe());

            // Simulate the listen loop having already run FinalizeHost() on a Disconnect/Timeout
            // event: serverPeer and host are null while the connect timeout teardown fires.
            SetField(transport, "lifeCycleCts", new CancellationTokenSource());
            SetField(transport, "serverPeer", null!);
            SetField(transport, "host", null!);
            SetField(transport, "listenLoopIsActive", false);

            MethodInfo forceDisconnect = typeof(ENetTransport)
               .GetMethod("ForceDisconnectAsync", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.DoesNotThrow(() =>
            {
                var task = (UniTask)forceDisconnect.Invoke(transport, null);
                task.GetAwaiter().GetResult();
            });
        }

        private static void SetField(object target, string name, object value) =>
            typeof(ENetTransport)
               .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
               .SetValue(target, value);
    }
}
