using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.RealmNavigation;
using DCL.Utilities;
using DCL.Utility.Types;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Realm;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using UnityEngine;

namespace DCL.Chat.Commands.Tests
{
    [TestFixture]
    public class ChatTeleporterShould
    {
        private static readonly Vector2Int CURRENT_PARCEL = new (5, 7);

        private IRealmNavigator realmNavigator = null!;
        private IDecentralandUrlsSource urlsSource = null!;
        private IScenesCache scenesCache = null!;
        private ChatTeleporter chatTeleporter = null!;

        [SetUp]
        public void SetUp()
        {
            realmNavigator = Substitute.For<IRealmNavigator>();

            realmNavigator.TeleportToParcelAsync(Arg.Any<Vector2Int>(), Arg.Any<CancellationToken>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<string?>())
                          .Returns(UniTask.FromResult(EnumResult<TaskError>.SuccessResult()));

            realmNavigator.TryChangeRealmAsync(Arg.Any<URLDomain>(), Arg.Any<CancellationToken>(), Arg.Any<Vector2Int>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<string?>())
                          .Returns(UniTask.FromResult(EnumResult<ChangeRealmError>.SuccessResult()));

            urlsSource = Substitute.For<IDecentralandUrlsSource>();
            urlsSource.Url(Arg.Any<DecentralandUrl>()).Returns("https://peer.decentraland.org");
            urlsSource.BaseDomain.Returns(IDecentralandUrlsSource.ORG_DOMAIN);

            // No gateway routing: an auto-substituted empty origin would let every realm pass the environment check
            urlsSource.GatewayOrigin.Returns((string?)null);

            IReadonlyReactiveProperty<Vector2Int> currentParcel = Substitute.For<IReadonlyReactiveProperty<Vector2Int>>();
            currentParcel.Value.Returns(CURRENT_PARCEL);

            scenesCache = Substitute.For<IScenesCache>();
            scenesCache.CurrentParcel.Returns(currentParcel);

            // In-world by default: the startup teleport already happened
            var landed = new StartParcel(Vector2Int.zero);
            landed.ConsumeByTeleportOperation();
            chatTeleporter = NewTeleporter(landed);
        }

        [Test]
        public void TakeARealmAndParcelAsTheStartupDestinationBeforeTheWorldLoads()
        {
            // Arrange
            var pending = new StartParcel(Vector2Int.zero);
            var jumpInRequests = 0;
            pending.OnJumpInRequested = () => jumpInRequests++;

            // Act
            string result = NewTeleporter(pending).TeleportToRealmAsync("flutterecho", new Vector2Int(3, 4), CancellationToken.None, "physics").GetAwaiter().GetResult();

            // Assert
            Assert.That(result, Does.StartWith("🟢"));
            Assert.That(pending.Realm!.Value.Value, Does.StartWith("https://peer.decentraland.org").And.EndWith("flutterecho.dcl.eth"));
            Assert.That(pending.Peek(), Is.EqualTo(new Vector2Int(3, 4)));
            Assert.That(pending.IsParcelAssigned, Is.True);
            Assert.That(pending.SpawnPointName, Is.EqualTo("physics"));
            Assert.That(pending.JumpInRequested, Is.True);
            Assert.That(jumpInRequests, Is.EqualTo(1));
            realmNavigator.DidNotReceive().TryChangeRealmAsync(Arg.Any<URLDomain>(), Arg.Any<CancellationToken>(), Arg.Any<Vector2Int>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<string?>());
        }

        [Test]
        public void TakeARealmAsTheStartupDestinationWithoutTouchingTheParcel()
        {
            // Arrange
            var pending = new StartParcel(new Vector2Int(8, 9));

            // Act
            NewTeleporter(pending).TeleportToRealmAsync("flutterecho", CancellationToken.None, "physics").GetAwaiter().GetResult();

            // Assert
            Assert.That(pending.Realm, Is.Not.Null);
            Assert.That(pending.IsParcelAssigned, Is.False);
            Assert.That(pending.Peek(), Is.EqualTo(new Vector2Int(8, 9)));
            Assert.That(pending.SpawnPointName, Is.EqualTo("physics"));
            Assert.That(pending.JumpInRequested, Is.True);
            realmNavigator.DidNotReceive().TryChangeRealmAsync(Arg.Any<URLDomain>(), Arg.Any<CancellationToken>(), Arg.Any<Vector2Int>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<string?>());
        }

        [Test]
        public void TakeAParcelAsTheStartupDestinationBeforeTheWorldLoads()
        {
            // Arrange
            var pending = new StartParcel(Vector2Int.zero);

            // Act
            NewTeleporter(pending).TeleportToParcelAsync(new Vector2Int(1, 2), false, CancellationToken.None).GetAwaiter().GetResult();

            // Assert
            Assert.That(pending.Realm, Is.Null);
            Assert.That(pending.Peek(), Is.EqualTo(new Vector2Int(1, 2)));
            Assert.That(pending.JumpInRequested, Is.True);
            realmNavigator.DidNotReceiveWithAnyArgs().TeleportToParcelAsync(default, default, default);
        }

        [Test]
        public void RejectAForeignRealmBeforeTheWorldLoadsToo()
        {
            // Arrange
            var pending = new StartParcel(Vector2Int.zero);

            // Act
            string result = NewTeleporter(pending).TeleportToRealmAsync("https://evil.example/world/x.dcl.eth", CancellationToken.None).GetAwaiter().GetResult();

            // Assert
            Assert.That(result, Does.Not.StartWith("🟢"));
            Assert.That(pending.Realm, Is.Null);
            Assert.That(pending.JumpInRequested, Is.False);
        }

        private ChatTeleporter NewTeleporter(StartParcel startParcel) =>
            new (realmNavigator, new ChatEnvironmentValidator(urlsSource), urlsSource, scenesCache, startParcel);

        [Test]
        public void TeleportWithinRealmWhenAlreadyThereAndSpawnPointIsGiven()
        {
            // Arrange
            realmNavigator.IsAlreadyOnRealm(Arg.Any<URLDomain>()).Returns(true);

            // Act
            chatTeleporter.TeleportToRealmAsync("flutterecho", CancellationToken.None, "physics").GetAwaiter().GetResult();

            // Assert
            realmNavigator.Received(1).TeleportToParcelAsync(CURRENT_PARCEL, Arg.Any<CancellationToken>(), true, spawnPointName: "physics");
        }

        [Test]
        public void KeepAlreadyInRealmMessageWhenNoSpawnPointIsGiven()
        {
            // Arrange
            realmNavigator.IsAlreadyOnRealm(Arg.Any<URLDomain>()).Returns(true);

            // Act
            string result = chatTeleporter.TeleportToRealmAsync("flutterecho", CancellationToken.None).GetAwaiter().GetResult();

            // Assert
            Assert.That(result, Does.StartWith("🟡"));
            realmNavigator.DidNotReceiveWithAnyArgs().TeleportToParcelAsync(default, default, default);
        }

        [Test]
        public void PassSpawnPointToRealmChangeWhenNotOnRealm()
        {
            // Arrange
            realmNavigator.IsAlreadyOnRealm(Arg.Any<URLDomain>()).Returns(false);

            // Act
            chatTeleporter.TeleportToRealmAsync("flutterecho", CancellationToken.None, "physics").GetAwaiter().GetResult();

            // Assert
            realmNavigator.Received(1).TryChangeRealmAsync(Arg.Any<URLDomain>(), Arg.Any<CancellationToken>(), Arg.Any<Vector2Int>(), Arg.Any<bool>(), Arg.Any<bool>(), spawnPointName: "physics");
        }
    }
}
