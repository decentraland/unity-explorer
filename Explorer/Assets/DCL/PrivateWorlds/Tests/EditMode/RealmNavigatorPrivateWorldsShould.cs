using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.NotificationsBus;
using DCL.RealmNavigation;
using DCL.RealmNavigation.LoadingOperation;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.Utilities;
using DCL.Utility.Types;
using ECS;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Realm;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DCL.RealmNavigation.TeleportOperations;

namespace DCL.PrivateWorlds.Tests.EditMode
{
    [TestFixture]
    public class RealmNavigatorPrivateWorldsShould
    {
        private const string WORLD_NAME = "test.dcl.eth";
        private RealmNavigator navigator = null!;
        private IWorldAccessGate worldAccessGate = null!;
        private ObjectProxy<IWorldAccessGate> worldAccessGateProxy = null!;
        private IRealmController realmController = null!;
        private IRealmData realmData = null!;
        private URLDomain testRealm;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Ensure the singleton is available for tests that hit the CheckFailed path.
            try { NotificationsBusController.Initialize(new NotificationsBusController()); }
            catch (InvalidOperationException) { /* already initialized by another fixture */ }
        }

        [SetUp]
        public void SetUp()
        {
            worldAccessGate = Substitute.For<IWorldAccessGate>();
            worldAccessGateProxy = new ObjectProxy<IWorldAccessGate>();
            worldAccessGateProxy.SetObject(worldAccessGate);

            realmData = Substitute.For<IRealmData>();
            realmData.Configured.Returns(true);
            realmData.IsLocalSceneDevelopment.Returns(false);
            var ipfs = Substitute.For<DCL.Ipfs.IIpfsRealm>();
            ipfs.CatalystBaseUrl.Returns(URLDomain.EMPTY);
            realmData.Ipfs.Returns(ipfs);

            realmController = Substitute.For<IRealmController>();
            realmController.RealmData.Returns(realmData);
            realmController.CurrentDomain.Returns((URLDomain?)URLDomain.FromString("https://other.realm"));
            realmController.IsReachableAsync(Arg.Any<URLDomain>(), Arg.Any<CancellationToken>()).Returns(UniTask.FromResult(true));

            testRealm = URLDomain.FromString("https://test-realm.decentraland.org");

            var loadingStatus = Substitute.For<ILoadingStatus>();
            var reportData = new ReportData(ReportCategory.REALM);
            var realmChangeOps = new SequentialLoadingOperation<TeleportParams>(loadingStatus, new List<ILoadingOperation<TeleportParams>>(), reportData);
            var teleportInRealmOps = new SequentialLoadingOperation<TeleportParams>(loadingStatus, new List<ILoadingOperation<TeleportParams>>(), reportData);

            var loadingScreen = Substitute.For<ILoadingScreen>();
            loadingScreen.ShowWhileExecuteTaskAsync(Arg.Any<Func<AsyncLoadProcessReport, CancellationToken, UniTask<EnumResult<TaskError>>>>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var op = callInfo.Arg<Func<AsyncLoadProcessReport, CancellationToken, UniTask<EnumResult<TaskError>>>>();
                    var ct = callInfo.Arg<CancellationToken>();
                    var report = AsyncLoadProcessReport.Create(ct);
                    return op(report, ct);
                });

            navigator = new RealmNavigator(
                loadingScreen,
                realmController,
                Substitute.For<IDecentralandUrlsSource>(),
                World.Create(),
                new ObjectProxy<Entity>(),
                new CameraSamplingData(),
                loadingStatus,
                Substitute.For<ILandscape>(),
                Substitute.For<DCL.PerformanceAndDiagnostics.Analytics.IAnalyticsController>(),
                realmChangeOps,
                teleportInRealmOps,
                worldAccessGateProxy
            );
        }

        [Test]
        public async Task ShowsToastAndBlocks_WhenCheckFailed()
        {
            // Arrange
            worldAccessGate.CheckAccessAsync(WORLD_NAME, Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(WorldAccessResult.CheckFailed));

            // Act
            var result = await navigator.TryChangeRealmAsync(testRealm, CancellationToken.None, default, WORLD_NAME)
                .Timeout(TimeSpan.FromSeconds(5));

            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual(ChangeRealmError.UnauthorizedWorldAccess, result.Error.Value.State);
        }

        [Test]
        public async Task LetsThrough_WhenAllowed()
        {
            // Arrange
            worldAccessGate.CheckAccessAsync(WORLD_NAME, Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(WorldAccessResult.Allowed));

            // Act
            var result = await navigator.TryChangeRealmAsync(testRealm, CancellationToken.None, default, WORLD_NAME)
                .Timeout(TimeSpan.FromSeconds(5));

            // Assert
            Assert.IsTrue(result.Success);
        }

        [Test]
        public async Task BlocksWithRecoverableError_WhenDenied()
        {
            // Arrange
            worldAccessGate.CheckAccessAsync(WORLD_NAME, Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(WorldAccessResult.Denied));

            // Act
            var result = await navigator.TryChangeRealmAsync(testRealm, CancellationToken.None, default, WORLD_NAME)
                .Timeout(TimeSpan.FromSeconds(5));

            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual(ChangeRealmError.WhitelistAccessDenied, result.Error.Value.State);
        }

        [Test]
        public async Task BlocksWithRecoverableError_WhenPasswordCancelled()
        {
            // Arrange
            worldAccessGate.CheckAccessAsync(WORLD_NAME, Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(WorldAccessResult.PasswordCancelled));

            // Act
            var result = await navigator.TryChangeRealmAsync(testRealm, CancellationToken.None, default, WORLD_NAME)
                .Timeout(TimeSpan.FromSeconds(5));

            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual(ChangeRealmError.PasswordCancelled, result.Error.Value.State);
        }

        [Test]
        public async Task SkipsPermissionCheck_WhenWorldNameIsNull()
        {
            // Arrange — gate should never be called
            worldAccessGate.CheckAccessAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(_ => { Assert.Fail("Permission check should not run when worldName is null"); return UniTask.FromResult(WorldAccessResult.Allowed); });

            // Act
            var result = await navigator.TryChangeRealmAsync(testRealm, CancellationToken.None, default, worldName: null)
                .Timeout(TimeSpan.FromSeconds(5));

            // Assert
            Assert.IsTrue(result.Success);
        }

        [Test]
        [Explicit("Runs ~15s due to permission check timeout; gate delays indefinitely so navigator returns CheckFailed.")]
        [Timeout(20000)]
        public async Task ReturnsCheckFailed_WhenTimeoutFires()
        {
            // Arrange — gate hangs forever, navigator's 15s timeout kicks in
            worldAccessGate.CheckAccessAsync(WORLD_NAME, Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(callInfo => UniTask.Create(async () =>
                {
                    await UniTask.Delay(60000, cancellationToken: callInfo.ArgAt<CancellationToken>(2));
                    return WorldAccessResult.Allowed;
                }));

            // Act
            var result = await navigator.TryChangeRealmAsync(testRealm, CancellationToken.None, default, WORLD_NAME)
                .Timeout(TimeSpan.FromSeconds(18));

            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual(ChangeRealmError.UnauthorizedWorldAccess, result.Error.Value.State);
        }
    }
}
