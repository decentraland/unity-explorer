using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
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
        private StartParcel startParcel = null!;

        [SetUp]
        public void SetUp()
        {
            IRealmData realmData = Substitute.For<IRealmData>();
            realmData.RealmType.Returns(new ReactiveProperty<RealmKind>(RealmKind.GenesisCity));

            realmController = Substitute.For<IRealmController>();
            realmController.RealmData.Returns(realmData);
            realmController.CurrentDomain.Returns(GENESIS);
            realmController.IsReachableAsync(Arg.Any<URLDomain>(), Arg.Any<CancellationToken>()).Returns(UniTask.FromResult(true));

            startParcel = new StartParcel(Vector2Int.zero);
        }

        [Test]
        public void SwitchToAReachableRealm()
        {
            // Arrange
            startParcel.AssignRealm(WORLD);

            // Act
            RealUserInAppInitializationFlow.ApplyStartRealmAsync(startParcel, realmController, GENESIS, CancellationToken.None).GetAwaiter().GetResult();

            // Assert
            realmController.Received(1).SetRealmAsync(WORLD, Arg.Any<CancellationToken>());
            Assert.That(startParcel.IsRealmApplied, Is.True);
        }

        [Test]
        public void KeepTheCurrentRealmWhenTheRequestedOneIsNotReachable()
        {
            // Arrange
            startParcel.AssignRealm(WORLD);
            realmController.IsReachableAsync(WORLD, Arg.Any<CancellationToken>()).Returns(UniTask.FromResult(false));

            // Act
            RealUserInAppInitializationFlow.ApplyStartRealmAsync(startParcel, realmController, GENESIS, CancellationToken.None).GetAwaiter().GetResult();

            // Assert
            realmController.DidNotReceiveWithAnyArgs().SetRealmAsync(default, default);
            Assert.That(startParcel.IsRealmApplied, Is.True);
        }

        [Test]
        public void TreatAnyGenesisRealmAsAGenesisPick()
        {
            // Arrange
            realmController.CurrentDomain.Returns(URLDomain.FromString("https://peer-ec1.decentraland.org"));
            startParcel.AssignRealm(GENESIS);

            // Act
            RealUserInAppInitializationFlow.ApplyStartRealmAsync(startParcel, realmController, GENESIS, CancellationToken.None).GetAwaiter().GetResult();

            // Assert
            realmController.DidNotReceiveWithAnyArgs().SetRealmAsync(default, default);
            realmController.DidNotReceiveWithAnyArgs().IsReachableAsync(default, default);
        }

        [Test]
        public void LeaveTheRealmAloneWhenNoneWasPicked()
        {
            // Act
            RealUserInAppInitializationFlow.ApplyStartRealmAsync(startParcel, realmController, GENESIS, CancellationToken.None).GetAwaiter().GetResult();

            // Assert
            realmController.DidNotReceiveWithAnyArgs().SetRealmAsync(default, default);
            Assert.That(startParcel.IsRealmApplied, Is.True);
        }
    }
}
