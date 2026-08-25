using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Utilities;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.UserInAppInitializationFlow.Tests
{
    [TestFixture]
    public class StartPulseMultiplayerStartupOperationShould
    {
        private const string REALM = "main";

        private World world = null!;
        private IPulseMultiplayerService service = null!;
        private IProfilePropagation profilePropagation = null!;
        private ISelfProfile selfProfile = null!;
        private IPulseRealm pulseRealm = null!;
        private CancellationTokenSource cts = null!;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            service = Substitute.For<IPulseMultiplayerService>();
            profilePropagation = Substitute.For<IProfilePropagation>();
            selfProfile = Substitute.For<ISelfProfile>();
            pulseRealm = Substitute.For<IPulseRealm>();
            pulseRealm.Value.Returns(REALM);
            cts = new CancellationTokenSource();
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
            cts.Dispose();
        }

        [Test]
        public async Task SkipConnectionWhenPulseInactive()
        {
            // Arrange
            var activation = new PulseActivation(false);
            StartPulseMultiplayerStartupOperation operation = Operation(activation);

            // Act
            await operation.ExecuteAsync(MakeParams(), cts.Token);

            // Assert
            _ = service.DidNotReceive().ConnectAsync(Arg.Any<CancellationToken>(), Arg.Any<int>());
            Assert.IsFalse(activation.IsActive);
        }

        [Test]
        public async Task FallBackToLiveKitWhenUnreachable()
        {
            // Arrange
            var activation = new PulseActivation(true);
            service.ConnectAsync(Arg.Any<CancellationToken>(), Arg.Any<int>()).Returns(UniTask.FromResult(false));
            StartPulseMultiplayerStartupOperation operation = Operation(activation);

            // Act
            await operation.ExecuteAsync(MakeParams(), cts.Token);

            // Assert
            Assert.IsFalse(activation.IsActive);
            profilePropagation.DidNotReceive().Propagate(Arg.Any<Profile>());
        }

        [Test]
        public async Task ResolveRealmBeforeConnecting()
        {
            // Arrange
            var activation = new PulseActivation(true);
            service.ConnectAsync(Arg.Any<CancellationToken>(), Arg.Any<int>()).Returns(UniTask.FromResult(true));
            StartPulseMultiplayerStartupOperation operation = Operation(activation);

            // Act
            await operation.ExecuteAsync(MakeParams(), cts.Token);

            // Assert
            Received.InOrder(() =>
            {
                pulseRealm.EnsureResolvedAsync(Arg.Any<CancellationToken>());
                service.ConnectAsync(Arg.Any<CancellationToken>(), Arg.Any<int>());
            });

            Assert.IsTrue(activation.IsActive);
        }

        [Test]
        public async Task FallBackToLiveKitWhenRealmUnresolved()
        {
            // Arrange
            var activation = new PulseActivation(true);
            pulseRealm.Value.Returns(string.Empty);
            StartPulseMultiplayerStartupOperation operation = Operation(activation);

            // Act
            await operation.ExecuteAsync(MakeParams(), cts.Token);

            // Assert
            Assert.IsFalse(activation.IsActive);
            _ = service.DidNotReceive().ConnectAsync(Arg.Any<CancellationToken>(), Arg.Any<int>());
            profilePropagation.DidNotReceive().Propagate(Arg.Any<Profile>());
        }

        private StartPulseMultiplayerStartupOperation Operation(PulseActivation activation) =>
            new (service, profilePropagation, selfProfile, activation, pulseRealm);

        private IStartupOperation.Params MakeParams()
        {
            var flowParams = new UserInAppInitializationFlowParameters(
                showAuthentication: false,
                showLoading: false,
                loadSource: IUserInAppInitializationFlow.LoadSource.StartUp,
                world: world,
                playerEntity: default);

            return new IStartupOperation.Params(AsyncLoadProcessReport.Create(cts.Token), flowParams);
        }
    }
}
