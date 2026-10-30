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
    public class ConnectPulseTeleportOperationShould
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
            pulseMultiplayerService.ConnectAsync(Arg.Any<CancellationToken>(), Arg.Any<int>()).Returns(UniTask.FromResult(true));

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
        public async Task ConnectWhenRealmCommsAreOnline()
        {
            // Arrange
            realmData.CommsAdapter.Returns(ONLINE_ADAPTER);
            var operation = new ConnectPulseTeleportOperation(pulseMultiplayerService, new PulseActivation(true), realmData);

            // Act
            EnumResult<TaskError> result = await operation.ExecuteAsync(MakeParams(), cts.Token);

            // Assert
            Assert.IsTrue(result.Success);
            _ = pulseMultiplayerService.Received(1).ConnectAsync(Arg.Any<CancellationToken>(), Arg.Any<int>());
        }

        [Test]
        public async Task NotConnectWhenRealmCommsAreOffline()
        {
            // Arrange
            realmData.CommsAdapter.Returns(OFFLINE_ADAPTER);
            var operation = new ConnectPulseTeleportOperation(pulseMultiplayerService, new PulseActivation(true), realmData);

            // Act
            EnumResult<TaskError> result = await operation.ExecuteAsync(MakeParams(), cts.Token);

            // Assert
            Assert.IsTrue(result.Success);
            _ = pulseMultiplayerService.DidNotReceive().ConnectAsync(Arg.Any<CancellationToken>(), Arg.Any<int>());
        }

        [Test]
        public async Task NotConnectWhenPulseIsInactive()
        {
            // Arrange
            realmData.CommsAdapter.Returns(ONLINE_ADAPTER);
            var operation = new ConnectPulseTeleportOperation(pulseMultiplayerService, new PulseActivation(false), realmData);

            // Act
            EnumResult<TaskError> result = await operation.ExecuteAsync(MakeParams(), cts.Token);

            // Assert
            Assert.IsTrue(result.Success);
            _ = pulseMultiplayerService.DidNotReceive().ConnectAsync(Arg.Any<CancellationToken>(), Arg.Any<int>());
        }

        [Test]
        public async Task FallBackToLiveKitWhenConnectionFailsTerminally()
        {
            // Arrange
            realmData.CommsAdapter.Returns(ONLINE_ADAPTER);
            pulseMultiplayerService.ConnectAsync(Arg.Any<CancellationToken>(), Arg.Any<int>()).Returns(UniTask.FromResult(false));
            var activation = new PulseActivation(true);
            var operation = new ConnectPulseTeleportOperation(pulseMultiplayerService, activation, realmData);

            // Act
            EnumResult<TaskError> result = await operation.ExecuteAsync(MakeParams(), cts.Token);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsFalse(activation.IsActive);
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
