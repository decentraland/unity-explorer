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
using System;
using System.Collections;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;

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
            int jumpInRequests = 0;
            pending.JumpInRequestRaised += () => jumpInRequests++;

            // Act
            string result = NewTeleporter(pending).TeleportToRealmAsync("flutterecho", new Vector2Int(3, 4), CancellationToken.None, "physics").GetAwaiter().GetResult();

            // Assert
            AssertLeadsWith(result, "🟢");
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
            Assert.That(pending.Realm, Is.EqualTo(URLDomain.FromString("https://peer.decentraland.org")));
            Assert.That(pending.Peek(), Is.EqualTo(new Vector2Int(1, 2)));
            Assert.That(pending.JumpInRequested, Is.True);
            realmNavigator.DidNotReceiveWithAnyArgs().TeleportToParcelAsync(default, default, default);
        }

        [Test]
        public void KeepTheCurrentRealmForALocalParcelBeforeTheWorldLoads()
        {
            // Arrange
            var pending = new StartParcel(Vector2Int.zero);

            // Act
            NewTeleporter(pending).TeleportToParcelAsync(new Vector2Int(1, 2), true, CancellationToken.None).GetAwaiter().GetResult();

            // Assert
            Assert.That(pending.Realm, Is.Null);
            Assert.That(pending.Peek(), Is.EqualTo(new Vector2Int(1, 2)));
            Assert.That(pending.JumpInRequested, Is.True);
        }

        [UnityTest]
        public IEnumerator NavigateToAnotherRealmOnlyAfterTheStartupTeleport() =>
            UniTask.ToCoroutine(async () =>
            {
                // Arrange
                var pending = new StartParcel(Vector2Int.zero);
                pending.MarkRealmApplied();
                realmNavigator.IsAlreadyOnRealm(Arg.Any<URLDomain>()).Returns(false);

                // Act
                UniTask<string> teleport = NewTeleporter(pending).TeleportToRealmAsync("flutterecho", new Vector2Int(3, 4), CancellationToken.None);
                await UniTask.Yield();
                bool navigatedDuringStartup = teleport.Status.IsCompleted();
                pending.ConsumeByTeleportOperation();
                string result = await teleport;

                // Assert
                Assert.That(navigatedDuringStartup, Is.False, "the request must wait for the startup teleport");
                AssertLeadsWith(result, "🟢");
                Assert.That(pending.Realm, Is.Null);
                Assert.That(pending.IsParcelAssigned, Is.False);
                Assert.That(pending.JumpInRequested, Is.False);
                realmNavigator.Received(1).TryChangeRealmAsync(Arg.Any<URLDomain>(), Arg.Any<CancellationToken>(), new Vector2Int(3, 4), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<string?>());
            });

        [UnityTest]
        public IEnumerator GiveUpWaitingForTheStartupTeleportWhenCancelled() =>
            UniTask.ToCoroutine(async () =>
            {
                // Arrange
                var pending = new StartParcel(Vector2Int.zero);
                pending.MarkRealmApplied();
                realmNavigator.IsAlreadyOnRealm(Arg.Any<URLDomain>()).Returns(false);
                using var cts = new CancellationTokenSource();

                // Act
                UniTask<string> teleport = NewTeleporter(pending).TeleportToParcelAsync(new Vector2Int(3, 4), false, cts.Token);
                await UniTask.Yield();
                cts.Cancel();
                string result = await teleport;

                // Assert
                AssertLeadsWith(result, "🔴");
                realmNavigator.DidNotReceiveWithAnyArgs().TeleportToParcelAsync(default, default, default);
            });

        [Test]
        public void TakeAParcelInTheAppliedRealmAsTheStartupDestination()
        {
            // Arrange
            var pending = new StartParcel(Vector2Int.zero);
            pending.MarkRealmApplied();
            realmNavigator.IsAlreadyOnRealm(Arg.Any<URLDomain>()).Returns(true);

            // Act
            string result = NewTeleporter(pending).TeleportToRealmAsync("flutterecho", new Vector2Int(3, 4), CancellationToken.None).GetAwaiter().GetResult();

            // Assert
            AssertLeadsWith(result, "🟢");
            Assert.That(pending.IsParcelAssigned, Is.True);
            Assert.That(pending.Peek(), Is.EqualTo(new Vector2Int(3, 4)));
            Assert.That(pending.JumpInRequested, Is.True);
            realmNavigator.DidNotReceiveWithAnyArgs().TeleportToParcelAsync(default, default, default);
        }

        [UnityTest]
        public IEnumerator KeepThePickedRealmWhenAParcelLinkArrivesDuringTheStartupSwitch() =>
            UniTask.ToCoroutine(async () =>
            {
                // Arrange
                URLDomain picked = URLDomain.FromString("https://peer.decentraland.org/world/flutterecho.dcl.eth");
                var pending = new StartParcel(Vector2Int.zero);
                pending.AssignRealm(picked);
                pending.MarkRealmApplied();
                realmNavigator.IsAlreadyOnRealm(Arg.Any<URLDomain>()).Returns(true);

                // Act
                UniTask<string> teleport = NewTeleporter(pending).TeleportToParcelAsync(new Vector2Int(1, 2), false, CancellationToken.None);
                await UniTask.Yield();
                bool startedDuringSwitch = teleport.Status.IsCompleted();
                pending.ConsumeByTeleportOperation();
                await teleport;

                // Assert
                Assert.That(startedDuringSwitch, Is.False);
                Assert.That(pending.Realm, Is.EqualTo(picked));
                Assert.That(pending.IsParcelAssigned, Is.False);
                realmNavigator.Received(1).TeleportToParcelAsync(new Vector2Int(1, 2), Arg.Any<CancellationToken>(), false, spawnPointName: null);
            });

        [Test]
        public void JoinThePickedRealmWithAParcelAfterTheStartupSwitchStarted()
        {
            // Arrange
            var pending = new StartParcel(Vector2Int.zero);
            NewTeleporter(pending).TeleportToRealmAsync("flutterecho", CancellationToken.None).GetAwaiter().GetResult();
            URLDomain picked = pending.Realm!.Value;
            pending.MarkRealmApplied();
            realmNavigator.IsAlreadyOnRealm(picked).Returns(true);

            // Act
            string result = NewTeleporter(pending).TeleportToRealmAsync("flutterecho", new Vector2Int(3, 4), CancellationToken.None, "physics").GetAwaiter().GetResult();

            // Assert
            AssertLeadsWith(result, "🟢");
            Assert.That(pending.Realm, Is.EqualTo(picked));
            Assert.That(pending.Peek(), Is.EqualTo(new Vector2Int(3, 4)));
            Assert.That(pending.SpawnPointName, Is.EqualTo("physics"));
            Assert.That(pending.JumpInRequested, Is.True);
            realmNavigator.DidNotReceive().TryChangeRealmAsync(Arg.Any<URLDomain>(), Arg.Any<CancellationToken>(), Arg.Any<Vector2Int>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<string?>());
        }

        [Test]
        public void TakeARealmAsTheStartupDestinationAgainOnceTheAppliedOneIsCleared()
        {
            // Arrange
            var pending = new StartParcel(Vector2Int.zero);
            pending.MarkRealmApplied();
            pending.ClearRealmApplied();

            // Act
            NewTeleporter(pending).TeleportToRealmAsync("otherworld", CancellationToken.None).GetAwaiter().GetResult();

            // Assert
            Assert.That(pending.Realm, Is.Not.Null);
            Assert.That(pending.JumpInRequested, Is.True);
            realmNavigator.DidNotReceive().TryChangeRealmAsync(Arg.Any<URLDomain>(), Arg.Any<CancellationToken>(), Arg.Any<Vector2Int>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<string?>());
        }

        [Test]
        public void RejectAForeignRealmBeforeTheWorldLoadsToo()
        {
            // Arrange
            var pending = new StartParcel(Vector2Int.zero);

            // Act
            string result = NewTeleporter(pending).TeleportToRealmAsync("https://evil.example/world/x.dcl.eth", CancellationToken.None).GetAwaiter().GetResult();

            // Assert
            AssertLeadsWith(result, "🟢", expected: false);
            Assert.That(pending.Realm, Is.Null);
            Assert.That(pending.JumpInRequested, Is.False);
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
            AssertLeadsWith(result, "🟡");
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

        private ChatTeleporter NewTeleporter(StartParcel startParcel) =>
            new (realmNavigator, new ChatEnvironmentValidator(urlsSource), urlsSource, scenesCache, startParcel);

        // Ordinal: the culture-aware StartsWith behind Does.StartWith treats emoji as ignorable on the Linux runner
        private static void AssertLeadsWith(string result, string marker, bool expected = true) =>
            Assert.That(result.StartsWith(marker, StringComparison.Ordinal), Is.EqualTo(expected), result);
    }
}
