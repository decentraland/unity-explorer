using Arch.Core;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using Promise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.Wearable[], DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;
using WearableDTOPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Helpers.WearableDTO[], DCL.AvatarRendering.Wearables.Components.Intentions.GetWearableDTOByPointersIntention>;
using AssetBundleManifestPromise = ECS.StreamableLoading.Common.AssetPromise<SceneRunner.Scene.SceneAssetBundleManifest, DCL.AvatarRendering.Wearables.Components.GetWearableAssetBundleManifestIntention>;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.AvatarRendering.Wearables.Tests
{
    [TestFixture]
    public class ResolveWearableByPointerSystemShould : UnitySystemTestBase<ResolveWearableByPointerSystem>
    {
        private Dictionary<string, IWearable> wearableCatalog;
        private readonly string testUrn = "urn:decentraland:off-chain:base-avatars:red_hoodie";
        private readonly string unisexTestUrn = "urn:decentraland:off-chain:base-avatars:red_hoodie_unisex";
        private readonly string defaultWearableUrn = "urn:decentraland:off-chain:base-avatars:green_hoodie";

        private StreamableLoadingResult<AssetBundleData> mockedDefaultAB;
        private StreamableLoadingResult<SceneAssetBundleManifest> mockedABManifest;
        private StreamableLoadingResult<AssetBundleData> mockedAB;

        [SetUp]
        public void Setup()
        {
            wearableCatalog = new Dictionary<string, IWearable>();
            IWearable mockDefaultWearable = CreateMockWearable(defaultWearableUrn, false, true);
            wearableCatalog.Add(mockDefaultWearable.GetUrn(), mockDefaultWearable);

            mockedAB = new StreamableLoadingResult<AssetBundleData>(new AssetBundleData(null, null, null));
            mockedDefaultAB = new StreamableLoadingResult<AssetBundleData>(new AssetBundleData(null, null, null));

            mockedABManifest = new StreamableLoadingResult<SceneAssetBundleManifest>(new SceneAssetBundleManifest("", new SceneAbDto
                { version = "0" }));

            system = new ResolveWearableByPointerSystem(world, wearableCatalog, "");
        }

        private IWearable CreateMockWearable(string urn, bool isUnisex, bool isDefaultWearable)
        {
            IWearable wearable = Substitute.For<IWearable>();
            wearable.GetUrn().Returns(urn);
            wearable.IsUnisex().Returns(isUnisex);
            wearable.GetCategory().Returns(WearablesLiterals.Categories.UPPER_BODY);

            var AssetBundleData
                = new StreamableLoadingResult<AssetBundleData>?[WearablesLiterals.BodyShape.COUNT];

            if (isDefaultWearable)
                AssetBundleData[WearablesLiterals.BodyShape.MALE] = mockedDefaultAB;

            wearable.AssetBundleData.Returns(AssetBundleData);
            return wearable;
        }

        private void MockWearableManifestResult(CancellationTokenSource cts, IWearable mockWearable, bool failed)
        {
            //Mocking the result of the LoadWearableManifestSystem
            var assetBundleManifestPromise
                = AssetBundleManifestPromise.Create(world, new GetWearableAssetBundleManifestIntention
                {
                    CommonArguments = new CommonLoadingArguments("mockURL", cancellationTokenSource: cts),
                }, PartitionComponent.TOP_PRIORITY);

            world.Create(assetBundleManifestPromise, mockWearable, WearablesLiterals.BodyShape.MALE);
            EntityReference assetBundleManifestPromiseEntity = assetBundleManifestPromise.Entity;
            world.Add(assetBundleManifestPromiseEntity, failed ? new StreamableLoadingResult<SceneAssetBundleManifest>(new Exception("FAILED")) : mockedABManifest);
            system.Update(0);
        }

        private void MockABResult(CancellationTokenSource cts, IWearable mockWearable, bool failed)
        {
            //Mocking the result of the LoadAssetBundleSystem
            var assetBundlePromise
                = AssetBundlePromise.Create(world, new GetAssetBundleIntention
                {
                    CommonArguments = new CommonLoadingArguments("mockURL", cancellationTokenSource: cts),
                }, PartitionComponent.TOP_PRIORITY);

            world.Create(assetBundlePromise, mockWearable, WearablesLiterals.BodyShape.MALE);
            EntityReference assetBundlePromiseEntity = assetBundlePromise.Entity;
            world.Add(assetBundlePromiseEntity, failed ? new StreamableLoadingResult<AssetBundleData>(new Exception("FAILED")) : mockedAB);
            system.Update(0);
        }

        [Test]
        public void ResolveWearable()
        {
            IWearable mockWearable = CreateMockWearable(testUrn, false, false);
            wearableCatalog.Add(mockWearable.GetUrn(), mockWearable);

            var getWearablesByPointersIntention = new GetWearablesByPointersIntention(new List<string>
                { testUrn }, new IWearable[1], WearablesLiterals.BodyShape.MALE);

            Promise.Create(world, getWearablesByPointersIntention, PartitionComponent.TOP_PRIORITY);
            system.Update(0);

            MockWearableManifestResult(getWearablesByPointersIntention.CancellationTokenSource, mockWearable, false);
            MockABResult(getWearablesByPointersIntention.CancellationTokenSource, mockWearable, false);

            mockWearable.AssetBundleData[WearablesLiterals.BodyShape.MALE] = mockedAB;
            mockWearable.Received().ManifestResult = mockedABManifest;
        }

        [Test]
        public void ResolveUnisexWearable()
        {
            IWearable mockUnisexWearable = CreateMockWearable(unisexTestUrn, false, true);
            wearableCatalog.Add(mockUnisexWearable.GetUrn(), mockUnisexWearable);

            var getWearablesByPointersIntention = new GetWearablesByPointersIntention(new List<string>
                { unisexTestUrn }, new IWearable[1], WearablesLiterals.BodyShape.MALE);

            Promise.Create(world, getWearablesByPointersIntention, PartitionComponent.TOP_PRIORITY);
            system.Update(0);

            MockWearableManifestResult(getWearablesByPointersIntention.CancellationTokenSource, mockUnisexWearable, false);
            MockABResult(getWearablesByPointersIntention.CancellationTokenSource, mockUnisexWearable, false);

            mockUnisexWearable.AssetBundleData[WearablesLiterals.BodyShape.MALE] = mockedAB;
            mockUnisexWearable.AssetBundleData[WearablesLiterals.BodyShape.FEMALE] = mockedAB;
            mockUnisexWearable.Received().ManifestResult = mockedABManifest;
        }

        [Test]
        public void ResolveDefaultWearableOnManifestFail()
        {
            IWearable mockWearable = CreateMockWearable(testUrn, false, false);
            wearableCatalog.Add(mockWearable.GetUrn(), mockWearable);

            var getWearablesByPointersIntention = new GetWearablesByPointersIntention(new List<string>
                { testUrn }, new IWearable[1], WearablesLiterals.BodyShape.MALE);

            Promise.Create(world, getWearablesByPointersIntention, PartitionComponent.TOP_PRIORITY);
            system.Update(0);

            MockWearableManifestResult(getWearablesByPointersIntention.CancellationTokenSource, mockWearable, true);

            mockWearable.AssetBundleData[WearablesLiterals.BodyShape.MALE] = mockedDefaultAB;
            mockWearable.DidNotReceive().ManifestResult = mockedABManifest;
        }

        [Test]
        public void ResolveDefaultWearableOnABFail()
        {
            IWearable mockWearable = CreateMockWearable(testUrn, false, false);
            wearableCatalog.Add(mockWearable.GetUrn(), mockWearable);

            var getWearablesByPointersIntention = new GetWearablesByPointersIntention(new List<string>
                { testUrn }, new IWearable[1], WearablesLiterals.BodyShape.MALE);

            Promise.Create(world, getWearablesByPointersIntention, PartitionComponent.TOP_PRIORITY);
            system.Update(0);

            MockWearableManifestResult(getWearablesByPointersIntention.CancellationTokenSource, mockWearable, false);
            MockABResult(getWearablesByPointersIntention.CancellationTokenSource, mockWearable, true);

            mockWearable.AssetBundleData[WearablesLiterals.BodyShape.MALE] = mockedDefaultAB;
            mockWearable.Received().ManifestResult = mockedABManifest;
        }

        [Test]
        public void CancelIntention()
        {
            var getWearablesByPointersIntention
                = new GetWearablesByPointersIntention(new List<string>
                    { testUrn }, new IWearable[1], WearablesLiterals.BodyShape.MALE);

            var promise = Promise.Create(world, getWearablesByPointersIntention, PartitionComponent.TOP_PRIORITY);
            system.Update(0);
            promise.ForgetLoading(world);
            system.Update(0);

            //The DTO and WearableByPointer request remain in world with a cancelled exception
            Assert.AreEqual(2, world.CountEntities(new QueryDescription()));
            Assert.IsTrue(getWearablesByPointersIntention.CancellationTokenSource.IsCancellationRequested);
            Assert.IsFalse(promise.Entity.IsAlive(world));

            //TODO: Discuss cancellation path. BY cancelling and add it as a result, wont it be
            //looping forever and be innaccessible?
            //Assert.IsTrue(promise.TryGetResult(world, out StreamableLoadingResult<Wearable[]> result));
            //Assert.IsFalse(result.Succeeded);
        }
    }
}
