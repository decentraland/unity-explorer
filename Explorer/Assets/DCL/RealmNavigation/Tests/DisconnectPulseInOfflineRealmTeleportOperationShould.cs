using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Utilities;
using DCL.Utility.Types;
using ECS;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DCL.RealmNavigation.TeleportOperations.Tests
{
    [TestFixture]
    public class DisconnectPulseInOfflineRealmTeleportOperationShould
    {
        private const string OFFLINE_ADAPTER = "fixed-adapter:offline:offline";
        private const string ONLINE_ADAPTER = "fixed-adapter:pulse:wss://pulse.example.com";

        private ILoadingStatus loadingStatus = null!;
        private IPulseMultiplayerService pulseMultiplayerService = null!;
        private IRealmData realmData = null!;
        private CancellationTokenSource cts = null!;

        [SetUp]
        public void SetUp()
        {
            loadingStatus = Substitute.For<ILoadingStatus>();

            pulseMultiplayerService = Substitute.For<IPulseMultiplayerService>();
            pulseMultiplayerService.DisconnectAsync().Returns(UniTask.CompletedTask);

            realmData = Substitute.For<IRealmData>();
            realmData.RealmName.Returns("test-realm");

            cts = new CancellationTokenSource();
        }

        [TearDown]
        public void TearDown()
        {
            cts.Dispose();
        }

        [Test]
        public async Task DisconnectWhenRealmCommsAreOffline()
        {
            // Arrange
            realmData.CommsAdapter.Returns(OFFLINE_ADAPTER);
            var operation = new DisconnectPulseInOfflineRealmTeleportOperation(pulseMultiplayerService, new PulseActivation(true), realmData);

            // Act
            EnumResult<TaskError> result = await operation.ExecuteAsync(MakeParams(), cts.Token);

            // Assert
            Assert.IsTrue(result.Success);
            _ = pulseMultiplayerService.Received(1).DisconnectAsync();
        }

        [Test]
        public async Task NotDisconnectWhenRealmCommsAreOnline()
        {
            // Arrange
            realmData.CommsAdapter.Returns(ONLINE_ADAPTER);
            var operation = new DisconnectPulseInOfflineRealmTeleportOperation(pulseMultiplayerService, new PulseActivation(true), realmData);

            // Act
            EnumResult<TaskError> result = await operation.ExecuteAsync(MakeParams(), cts.Token);

            // Assert
            Assert.IsTrue(result.Success);
            _ = pulseMultiplayerService.DidNotReceive().DisconnectAsync();
        }

        [Test]
        public async Task NotDisconnectWhenPulseIsInactive()
        {
            // Arrange
            realmData.CommsAdapter.Returns(OFFLINE_ADAPTER);
            var operation = new DisconnectPulseInOfflineRealmTeleportOperation(pulseMultiplayerService, new PulseActivation(false), realmData);

            // Act
            EnumResult<TaskError> result = await operation.ExecuteAsync(MakeParams(), cts.Token);

            // Assert
            Assert.IsTrue(result.Success);
            _ = pulseMultiplayerService.DidNotReceive().DisconnectAsync();
        }

        private TeleportParams MakeParams() =>
            new (
                URLDomain.EMPTY,
                Vector2Int.zero,
                AsyncLoadProcessReport.Create(cts.Token),
                loadingStatus,
                allowsWorldPositionOverride: false);
    }
}
