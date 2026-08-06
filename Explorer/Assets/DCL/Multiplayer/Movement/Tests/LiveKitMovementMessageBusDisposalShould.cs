using Arch.Core;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Multiplayer.Profiles.BroadcastProfiles;
using DCL.Multiplayer.Profiles.Tables;
using NSubstitute;
using NUnit.Framework;

namespace DCL.Multiplayer.Movement.Tests
{
    /// <summary>
    ///     Regression coverage for UNITY-EXPLORER-NQG (Sentry): on every clean app shutdown
    ///     <see cref="LiveKitMovementMessageBus" /> is disposed twice — once by
    ///     <c>MultiplayerMovementPlugin.Dispose()</c> and again by
    ///     <c>LiveKitMultiplayerContainer.Dispose()</c> via <c>DynamicWorldContainer.Dispose()</c> — because
    ///     the container has owned the bus since PR #7291 while the plugin still disposes it too. The second
    ///     <c>Dispose()</c> called <c>cancellationTokenSource.Cancel()</c> on an already-disposed CTS,
    ///     throwing <see cref="System.ObjectDisposedException" /> on every quit.
    /// </summary>
    [TestFixture]
    public class LiveKitMovementMessageBusDisposalShould
    {
        private World world;
        private LiveKitMovementMessageBus bus;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();

            IMessagePipesHub messagePipesHub = Substitute.For<IMessagePipesHub>();
            messagePipesHub.IslandPipe().Returns(Substitute.For<IMessagePipe>());
            messagePipesHub.ScenePipe().Returns(Substitute.For<IMessagePipe>());

            var movementInbox = new MovementInbox(Substitute.For<IReadOnlyEntityParticipantTable>(), world);

            var broadcaster = new LiveKitMessagesBroadcaster(
                Substitute.For<IGateKeeperSceneRoom>(),
                messagePipesHub,
                new PulseActivation(false));

            bus = new LiveKitMovementMessageBus(messagePipesHub, movementInbox, broadcaster);
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void NotThrowOnSecondDispose()
        {
            bus.Dispose();

            // At pin: the second Dispose() re-enters cancellationTokenSource.Cancel() on an already
            // disposed CancellationTokenSource and throws ObjectDisposedException — exactly the shape
            // of the shutdown-time NQG events (DynamicWorldContainer.Dispose -> ... -> CTS.Cancel).
            Assert.DoesNotThrow(() => bus.Dispose());
        }
    }
}
