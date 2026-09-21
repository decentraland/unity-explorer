using CommunicationData.URLHelpers;
using DCL.Ipfs;
using DCL.LOD;
using DCL.Utilities;
using DCL.Utility.Types;
using ECS;
using ECS.SceneLifeCycle.Realm;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using UnityEngine;

namespace DCL.RealmNavigation.TeleportOperations.Tests
{
    /// <summary>
    ///     Regression coverage for https://github.com/decentraland/unity-explorer/issues/10031.
    ///     A competing realm change can invalidate <see cref="RealmData" /> between the realm-change step and
    ///     this one, and <see cref="IRealmData.ScenesAreFixed" /> throws while the realm is unconfigured.
    ///     Prewarming is optional, so that window must not fail the whole teleport chain.
    /// </summary>
    [TestFixture]
    public class PrewarmRoadAssetPoolsTeleportOperationShould
    {
        private IRealmController realmController = null!;
        private IRoadAssetPool roadAssetPool = null!;
        private ILoadingStatus loadingStatus = null!;
        private CancellationTokenSource cts = null!;

        [SetUp]
        public void SetUp()
        {
            realmController = Substitute.For<IRealmController>();
            roadAssetPool = Substitute.For<IRoadAssetPool>();
            loadingStatus = Substitute.For<ILoadingStatus>();
            cts = new CancellationTokenSource();
        }

        [TearDown]
        public void TearDown()
        {
            cts.Dispose();
        }

        [Test]
        public void SucceedWithoutPrewarmingWhenRealmIsNotConfigured()
        {
            // The real RealmData is used on purpose: its ScenesAreFixed getter is the one that throws
            realmController.RealmData.Returns(new RealmData());

            EnumResult<TaskError> result = Execute();

            Assert.IsTrue(result.Success, result.Error?.Message);
            roadAssetPool.DidNotReceive().Prewarm();
        }

        [Test]
        public void PrewarmWhenRealmIsGenesisCity()
        {
            realmController.RealmData.Returns(MakeConfiguredRealm(scenesAreFixed: false));

            EnumResult<TaskError> result = Execute();

            Assert.IsTrue(result.Success, result.Error?.Message);
            roadAssetPool.Received(1).Prewarm();
        }

        [Test]
        public void SkipPrewarmWhenRealmScenesAreFixed()
        {
            realmController.RealmData.Returns(MakeConfiguredRealm(scenesAreFixed: true));

            EnumResult<TaskError> result = Execute();

            Assert.IsTrue(result.Success, result.Error?.Message);
            roadAssetPool.DidNotReceive().Prewarm();
        }

        private EnumResult<TaskError> Execute()
        {
            var operation = new PrewarmRoadAssetPoolsTeleportOperation(realmController, roadAssetPool);

            var teleportParams = new TeleportParams(
                URLDomain.EMPTY,
                Vector2Int.zero,
                AsyncLoadProcessReport.Create(cts.Token),
                loadingStatus,
                allowsWorldPositionOverride: false);

            return operation.ExecuteAsync(teleportParams, cts.Token).GetAwaiter().GetResult();
        }

        private static IRealmData MakeConfiguredRealm(bool scenesAreFixed) =>
            new IRealmData.Fake(new LocalIpfsRealm(URLDomain.EMPTY), scenesAreFixed, "realm", configured: true, 1, string.Empty, "v3", "localhost");
    }
}
