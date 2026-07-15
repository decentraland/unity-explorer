using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
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
        private ChatTeleporter chatTeleporter = null!;

        [SetUp]
        public void SetUp()
        {
            realmNavigator = Substitute.For<IRealmNavigator>();

            realmNavigator.TeleportToParcelAsync(Arg.Any<Vector2Int>(), Arg.Any<CancellationToken>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<string?>())
                          .Returns(UniTask.FromResult(EnumResult<TaskError>.SuccessResult()));

            realmNavigator.TryChangeRealmAsync(Arg.Any<URLDomain>(), Arg.Any<CancellationToken>(), Arg.Any<Vector2Int>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<string?>())
                          .Returns(UniTask.FromResult(EnumResult<ChangeRealmError>.SuccessResult()));

            IDecentralandUrlsSource urlsSource = Substitute.For<IDecentralandUrlsSource>();
            urlsSource.Url(Arg.Any<DecentralandUrl>()).Returns("https://peer.decentraland.org");

            IReadonlyReactiveProperty<Vector2Int> currentParcel = Substitute.For<IReadonlyReactiveProperty<Vector2Int>>();
            currentParcel.Value.Returns(CURRENT_PARCEL);

            IScenesCache scenesCache = Substitute.For<IScenesCache>();
            scenesCache.CurrentParcel.Returns(currentParcel);

            chatTeleporter = new ChatTeleporter(realmNavigator, new ChatEnvironmentValidator(DecentralandEnvironment.Org), urlsSource, scenesCache);
        }

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
