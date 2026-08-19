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
    ///     Regression (Sentry UNITY-EXPLORER-NQG): the bus has two owners that both dispose it on shutdown,
    ///     and the second call cancelled an already-disposed CTS.
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

            Assert.DoesNotThrow(() => bus.Dispose());
        }
    }
}
