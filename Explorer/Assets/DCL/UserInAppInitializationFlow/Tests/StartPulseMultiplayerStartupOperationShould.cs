using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Utilities;
using DCL.Utility.Types;
using ECS;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DCL.UserInAppInitializationFlow.Tests
{
    [TestFixture]
    public class StartPulseMultiplayerStartupOperationShould
    {
        private const string REALM = "main";
        private const string ENTITY_ID = "b64-L2hvbWUvZGV2L215LXNjZW5l";

        private World world = null!;
        private IPulseMultiplayerService service = null!;
        private IProfilePropagation profilePropagation = null!;
        private ISelfProfile selfProfile = null!;
        private IRealmData realmData = null!;
        private ILocalSceneEntityIdSource entityIdSource = null!;
        private CancellationTokenSource cts = null!;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            service = Substitute.For<IPulseMultiplayerService>();
            profilePropagation = Substitute.For<IProfilePropagation>();
            selfProfile = Substitute.For<ISelfProfile>();
            realmData = Substitute.For<IRealmData>();
            realmData.RealmName.Returns(REALM);
            entityIdSource = Substitute.For<ILocalSceneEntityIdSource>();
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
            StartPulseMultiplayerStartupOperation operation = Operation(activation, new PulseRealm(realmData));

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
            StartPulseMultiplayerStartupOperation operation = Operation(activation, new PulseRealm(realmData));

            // Act
            await operation.ExecuteAsync(MakeParams(), cts.Token);

            // Assert
            Assert.IsFalse(activation.IsActive);
            profilePropagation.DidNotReceive().Propagate(Arg.Any<Profile>());
        }

        [Test]
        public async Task ResolveTheLocalSceneRealmBeforeConnecting()
        {
            // Arrange
            var activation = new PulseActivation(true);
            service.ConnectAsync(Arg.Any<CancellationToken>(), Arg.Any<int>()).Returns(UniTask.FromResult(true));
            entityIdSource.EntityAsync(Arg.Any<CancellationToken>())
                          .Returns(UniTask.FromResult(Result<LocalSceneEntity>.SuccessResult(new LocalSceneEntity(ENTITY_ID, Vector2Int.zero))));

            var pulseRealm = new PulseRealm(realmData, entityIdSource);
            StartPulseMultiplayerStartupOperation operation = Operation(activation, pulseRealm);

            // Act
            await operation.ExecuteAsync(MakeParams(), cts.Token);

            // Assert — the realm is only non-empty once resolved, and the connection is gated on that,
            // so a connected session proves resolution ran first
            Assert.That(pulseRealm.Value, Is.EqualTo("lsd:" + ENTITY_ID));
            _ = service.Received(1).ConnectAsync(Arg.Any<CancellationToken>(), Arg.Any<int>());
            Assert.IsTrue(activation.IsActive);
        }

        [Test]
        public async Task FallBackToLiveKitWhenTheLocalSceneRealmIsUnresolved()
        {
            // Arrange — the local dev server is unreachable
            var activation = new PulseActivation(true);
            entityIdSource.EntityAsync(Arg.Any<CancellationToken>())
                          .Returns(UniTask.FromResult(Result<LocalSceneEntity>.ErrorResult("Local scene server unreachable")));

            var pulseRealm = new PulseRealm(realmData, entityIdSource);
            StartPulseMultiplayerStartupOperation operation = Operation(activation, pulseRealm);

            // Act
            await operation.ExecuteAsync(MakeParams(), cts.Token);

            // Assert — an empty realm is rejected server-side, so it must not connect at all
            Assert.That(pulseRealm.Value, Is.Empty);
            Assert.IsFalse(activation.IsActive);
            _ = service.DidNotReceive().ConnectAsync(Arg.Any<CancellationToken>(), Arg.Any<int>());
            profilePropagation.DidNotReceive().Propagate(Arg.Any<Profile>());
        }

        private StartPulseMultiplayerStartupOperation Operation(PulseActivation activation, PulseRealm pulseRealm) =>
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
