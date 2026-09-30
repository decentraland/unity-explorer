using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.FeatureFlags;
using DCL.RealmNavigation;
using DCL.Utilities;
using ECS;
using ECS.SceneLifeCycle.Realm;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using UnityEngine;

namespace DCL.UserInAppInitializationFlow.Tests
{
    [TestFixture]
    public class StartupRealmShould
    {
        private static readonly URLDomain GENESIS = URLDomain.FromString("https://peer.decentraland.org");
        private static readonly URLDomain WORLD = URLDomain.FromString("https://worlds.example.com/myworld.dcl.eth");

        private IRealmController realmController = null!;
        private IChatHistory chatHistory = null!;
        private StartParcel startParcel = null!;

        [SetUp]
        public void SetUp()
        {
            // A system chat message resolves its sender through the wallets helper, which reads the feature flags
            FeatureFlagsConfiguration.Reset();
            OfficialWalletsHelper.Reset();
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));
            OfficialWalletsHelper.Initialize(new OfficialWalletsHelper());

            IRealmData realmData = Substitute.For<IRealmData>();
            realmData.RealmType.Returns(new ReactiveProperty<RealmKind>(RealmKind.GenesisCity));

            realmController = Substitute.For<IRealmController>();
            realmController.RealmData.Returns(realmData);
            realmController.CurrentDomain.Returns(GENESIS);
            realmController.IsReachableAsync(Arg.Any<URLDomain>(), Arg.Any<CancellationToken>()).Returns(UniTask.FromResult(true));

            chatHistory = Substitute.For<IChatHistory>();
            startParcel = new StartParcel(Vector2Int.zero);
        }

        [TearDown]
        public void TearDown()
        {
            OfficialWalletsHelper.Reset();
            FeatureFlagsConfiguration.Reset();
        }

        [Test]
        public void SwitchToAReachableRealm()
        {
            // Arrange
            startParcel.AssignRealm(WORLD);

            // Act
            RealUserInAppInitializationFlow.ApplyStartRealmAsync(startParcel, realmController, chatHistory, GENESIS, CancellationToken.None).GetAwaiter().GetResult();

            // Assert
            realmController.Received(1).SetRealmAsync(WORLD, Arg.Any<CancellationToken>());
            chatHistory.DidNotReceiveWithAnyArgs().AddMessage(default, default, default);
            Assert.That(startParcel.IsRealmApplied, Is.True);
        }

        [Test]
        public void KeepTheCurrentRealmWhenTheRequestedOneIsNotReachable()
        {
            // Arrange
            startParcel.AssignRealm(WORLD);
            startParcel.Assign(new Vector2Int(10, 10));
            realmController.IsReachableAsync(WORLD, Arg.Any<CancellationToken>()).Returns(UniTask.FromResult(false));

            // Act
            RealUserInAppInitializationFlow.ApplyStartRealmAsync(startParcel, realmController, chatHistory, GENESIS, CancellationToken.None).GetAwaiter().GetResult();

            // Assert
            realmController.DidNotReceiveWithAnyArgs().SetRealmAsync(default, default);
            Assert.That(startParcel.IsRealmApplied, Is.True);
            Assert.That(startParcel.Realm, Is.EqualTo(GENESIS));
            Assert.That(startParcel.IsParcelAssigned, Is.False, "a parcel that belonged to the unreachable realm must not be used in the kept one");
            chatHistory.Received(1).AddMessage(ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY, Arg.Any<ChatMessage>());
        }

        [Test]
        public void TreatAnyGenesisRealmAsAGenesisPick()
        {
            // Arrange
            realmController.CurrentDomain.Returns(URLDomain.FromString("https://peer-ec1.decentraland.org"));
            startParcel.AssignRealm(GENESIS);

            // Act
            RealUserInAppInitializationFlow.ApplyStartRealmAsync(startParcel, realmController, chatHistory, GENESIS, CancellationToken.None).GetAwaiter().GetResult();

            // Assert
            realmController.DidNotReceiveWithAnyArgs().SetRealmAsync(default, default);
            realmController.DidNotReceiveWithAnyArgs().IsReachableAsync(default, default);
        }

        [Test]
        public void LeaveTheRealmAloneWhenNoneWasPicked()
        {
            // Act
            RealUserInAppInitializationFlow.ApplyStartRealmAsync(startParcel, realmController, chatHistory, GENESIS, CancellationToken.None).GetAwaiter().GetResult();

            // Assert
            realmController.DidNotReceiveWithAnyArgs().SetRealmAsync(default, default);
            Assert.That(startParcel.IsRealmApplied, Is.True);
        }
    }
}
