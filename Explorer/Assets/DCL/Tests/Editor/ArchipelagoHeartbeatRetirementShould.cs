using Cysharp.Threading.Tasks;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.Archipelago.AdapterAddress.Current;
using DCL.Multiplayer.Connections.Archipelago.LiveConnections;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.Archipelago.SignFlow;
using DCL.Multiplayer.Connections.Pools;
using Global.AppArgs;
using LiveKit.Internal.FFIClients.Pools.Memory;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.Tests.Editor
{
    public class ArchipelagoHeartbeatRetirementShould
    {
        [SetUp]
        public void SetUp()
        {
            FeaturesRegistry.Reset();
            FeatureFlagsConfiguration.Reset();
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));
            FeaturesRegistry.Initialize(new FeaturesRegistry(Substitute.For<IAppArgs>(), localSceneDevelopment: false));
        }

        [TearDown]
        public void TearDown()
        {
            FeaturesRegistry.Reset();
            FeatureFlagsConfiguration.Reset();
        }

        [Test]
        public async Task NeverSendApplicationHeartbeatFromRoomCycles()
        {
            // Arrange: real sign flow and room cycle, observed at the socket boundary.
            var socket = Substitute.For<IArchipelagoLiveConnection>();
            var flow = new LiveConnectionArchipelagoSignFlow(socket, new DCLMultiPool());
            var room = new CycleProbe(flow);

            // Act
            for (int i = 0; i < 3; i++) await room.TickAsync();

            // Assert
            _ = socket.DidNotReceive().SendAsync(Arg.Any<MemoryWrap>(), Arg.Any<CancellationToken>());
        }

        private sealed class CycleProbe : ArchipelagoIslandRoom
        {
            public CycleProbe(IArchipelagoSignFlow flow) : base(flow, Substitute.For<ICurrentAdapterAddress>()) { }

            public UniTask TickAsync() => CycleStepAsync(CancellationToken.None);
        }
    }
}
