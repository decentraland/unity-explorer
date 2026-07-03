using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using DCL.Utilities;
using NSubstitute;
using NUnit.Framework;
using System.Threading;

namespace DCL.UserInAppInitializationFlow.Tests
{
    [TestFixture]
    public class StartPulseMultiplayerStartupOperationShould
    {
        private World world;
        private IPulseMultiplayerService service;
        private IProfilePropagation profilePropagation;
        private ISelfProfile selfProfile;
        private CancellationTokenSource cts;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            service = Substitute.For<IPulseMultiplayerService>();
            profilePropagation = Substitute.For<IProfilePropagation>();
            selfProfile = Substitute.For<ISelfProfile>();
            cts = new CancellationTokenSource();
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
            cts.Dispose();
        }

        [Test]
        public void SkipConnectionWhenPulseInactive()
        {
            // Arrange
            var activation = new PulseActivation(false);
            var operation = new StartPulseMultiplayerStartupOperation(service, profilePropagation, selfProfile, activation);

            // Act
            operation.ExecuteAsync(MakeParams(), cts.Token).GetAwaiter().GetResult();

            // Assert
            service.DidNotReceive().ConnectAsync(Arg.Any<CancellationToken>(), Arg.Any<int>());
            Assert.IsFalse(activation.IsActive);
        }

        [Test]
        public void FallBackToLiveKitWhenUnreachable()
        {
            // Arrange
            var activation = new PulseActivation(true);
            service.ConnectAsync(Arg.Any<CancellationToken>(), Arg.Any<int>()).Returns(UniTask.FromResult(false));
            var operation = new StartPulseMultiplayerStartupOperation(service, profilePropagation, selfProfile, activation);

            // Act
            operation.ExecuteAsync(MakeParams(), cts.Token).GetAwaiter().GetResult();

            // Assert
            Assert.IsFalse(activation.IsActive);
            profilePropagation.DidNotReceive().Propagate(Arg.Any<Profile>());
        }

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
