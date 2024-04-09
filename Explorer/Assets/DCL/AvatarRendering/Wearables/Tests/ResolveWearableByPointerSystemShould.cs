using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using ECS;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Tests;
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
using AssetBundleManifestPromise = ECS.StreamableLoading.Common.AssetPromise<SceneRunner.Scene.SceneAssetBundleManifest, DCL.AvatarRendering.Wearables.Components.GetWearableAssetBundleManifestIntention>;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.AvatarRendering.Wearables.Tests
{

    public class ResolveWearableByPointerSystemShould : UnitySystemTestBase<ResolveWearableByPointerSystem>
    {

        public void Setup()
        {
            mockedABManifest = new StreamableLoadingResult<SceneAssetBundleManifest>(new SceneAssetBundleManifest(URLDomain.EMPTY, new SceneAbDto
                { version = "0" }));

            wearableCatalog = new WearableCatalog();

            mockedAB = new StreamableLoadingResult<WearableAssetBase>(new WearableRegularAsset(null, null, null));

            mockedDefaultAB = new StreamableLoadingResult<WearableAssetBase>(new WearableRegularAsset(null, null, null));

            IWearable mockDefaultWearable = CreateMockWearable(defaultWearableUrn, false, true);

            wearableCatalog.wearablesCache.Add(mockDefaultWearable.GetUrn(), mockDefaultWearable);
            world.Create(new DefaultWearablesComponent
            {
                ResolvedState = DefaultWearablesComponent.State.Success,
            });

            system = new ResolveWearableByPointerSystem(world, wearableCatalog, new RealmData(new TestIpfsRealm()), URLSubdirectory.EMPTY);
        }

        private WearableCatalog wearableCatalog;
        private readonly string testUrn = "urn:decentraland:off-chain:base-avatars:red_hoodie";
        private readonly string unisexTestUrn = "urn:decentraland:off-chain:base-avatars:red_hoodie_unisex";
        private readonly string defaultWearableUrn = "urn:decentraland:off-chain:base-avatars:green_hoodie";

        private StreamableLoadingResult<WearableAssetBase> mockedDefaultAB;
        private StreamableLoadingResult<SceneAssetBundleManifest> mockedABManifest;
        private StreamableLoadingResult<WearableAssetBase> mockedAB;

        private IWearable CreateMockWearable(URN urn, bool isUnisex, bool isDefaultWearable)
        {
            IWearable wearable = Substitute.For<IWearable>();
            wearable.GetUrn().Returns(urn);
            wearable.IsUnisex().Returns(isUnisex);
            wearable.GetCategory().Returns(WearablesConstants.Categories.UPPER_BODY);

            var wearableAssets = new WearableAssets[BodyShape.COUNT];

            if (isDefaultWearable)
                wearableAssets[BodyShape.MALE] = mockedDefaultAB;

            wearable.WearableAssetResults.Returns(wearableAssets);
            wearable.WearableDTO.Returns(new StreamableLoadingResult<WearableDTO>(new WearableDTO { id = urn }));
            wearable.TryGetMainFileHash(Arg.Any<BodyShape>(), out Arg.Any<string>()).Returns(x =>
            {
                x[1] = "mockedHash";
                return true;
            });
            wearable.GetHash().Returns((string)urn);
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

            world.Create(assetBundleManifestPromise, mockWearable, BodyShape.MALE);
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

            world.Create(assetBundlePromise, mockWearable, BodyShape.MALE);
            EntityReference assetBundlePromiseEntity = assetBundlePromise.Entity;
            world.Add(assetBundlePromiseEntity, failed ? new StreamableLoadingResult<AssetBundleData>(new Exception("FAILED")) : mockedAB);
        }


        public void ResolveWearable()
        {
            //Arrange
            IWearable mockWearable = CreateMockWearable(testUrn, false, false);
            wearableCatalog.wearablesCache.Add(mockWearable.GetUrn(), mockWearable);

            var getWearablesByPointersIntention = new GetWearablesByPointersIntention(new List<URN>
                { testUrn }, BodyShape.MALE, Array.Empty<string>());

            Promise.Create(world, getWearablesByPointersIntention, PartitionComponent.TOP_PRIORITY);
            system.Update(0);

            //Act
            MockWearableManifestResult(getWearablesByPointersIntention.CancellationTokenSource, mockWearable, false);
            MockABResult(getWearablesByPointersIntention.CancellationTokenSource, mockWearable, false);

            //Assert
            mockWearable.WearableAssetResults[BodyShape.MALE] = mockedAB;
            mockWearable.Received().ManifestResult = mockedABManifest;
        }


        public void ResolveUnisexWearable()
        {
            //Arrange
            IWearable mockUnisexWearable = CreateMockWearable(unisexTestUrn, false, true);
            wearableCatalog.wearablesCache.Add(mockUnisexWearable.GetUrn(), mockUnisexWearable);

            var getWearablesByPointersIntention = new GetWearablesByPointersIntention(new List<URN>
                { unisexTestUrn }, BodyShape.MALE, Array.Empty<string>());

            Promise.Create(world, getWearablesByPointersIntention, PartitionComponent.TOP_PRIORITY);
            system.Update(0);

            //Act
            MockWearableManifestResult(getWearablesByPointersIntention.CancellationTokenSource, mockUnisexWearable, false);
            MockABResult(getWearablesByPointersIntention.CancellationTokenSource, mockUnisexWearable, false);

            //Assert
            mockUnisexWearable.WearableAssetResults[BodyShape.MALE] = mockedAB;
            mockUnisexWearable.WearableAssetResults[BodyShape.FEMALE] = mockedAB;
            mockUnisexWearable.Received().ManifestResult = mockedABManifest;
        }


        public void ResolveDefaultWearableOnManifestFail()
        {
            //Arrange
            IWearable mockWearable = CreateMockWearable(testUrn, false, false);
            wearableCatalog.wearablesCache.Add(mockWearable.GetUrn(), mockWearable);

            var getWearablesByPointersIntention = new GetWearablesByPointersIntention(new List<URN>
                { testUrn }, BodyShape.MALE, Array.Empty<string>());

            Promise.Create(world, getWearablesByPointersIntention, PartitionComponent.TOP_PRIORITY);

            //Act
            system.Update(0);
            MockWearableManifestResult(getWearablesByPointersIntention.CancellationTokenSource, mockWearable, true);

            //Assert
            mockWearable.WearableAssetResults[BodyShape.MALE] = mockedDefaultAB;
            mockWearable.DidNotReceive().ManifestResult = mockedABManifest;
        }


        public void ResolveDefaultWearableOnABFail()
        {
            //Arrange
            IWearable mockWearable = CreateMockWearable(testUrn, false, false);
            wearableCatalog.wearablesCache.Add(mockWearable.GetUrn(), mockWearable);

            var getWearablesByPointersIntention = new GetWearablesByPointersIntention(new List<URN>
                { testUrn }, BodyShape.MALE, Array.Empty<string>());

            Promise.Create(world, getWearablesByPointersIntention, PartitionComponent.TOP_PRIORITY);
            system.Update(0);

            //Act
            MockWearableManifestResult(getWearablesByPointersIntention.CancellationTokenSource, mockWearable, false);
            MockABResult(getWearablesByPointersIntention.CancellationTokenSource, mockWearable, true);

            //Assert
            mockWearable.WearableAssetResults[BodyShape.MALE] = mockedDefaultAB;
            mockWearable.Received().ManifestResult = mockedABManifest;
        }


        public void CancelIntentionOnManifestStage()
        {
            //Arrange
            IWearable mockWearable = CreateMockWearable(testUrn, false, false);
            wearableCatalog.wearablesCache.Add(mockWearable.GetUrn(), mockWearable);

            var getWearablesByPointersIntention
                = new GetWearablesByPointersIntention(new List<URN>
                    { testUrn }, BodyShape.MALE, Array.Empty<string>());

            var promise = Promise.Create(world, getWearablesByPointersIntention, PartitionComponent.TOP_PRIORITY);
            system.Update(0);

            //Act
            Assert.AreEqual(1, world.CountEntities(in new QueryDescription().WithAll<AssetBundleManifestPromise>()));
            promise.ForgetLoading(world);
            system.Update(0);

            //Assert
            Assert.IsTrue(promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested);
            Assert.IsFalse(promise.Entity.IsAlive(world));

            //No  Manifest promises should be left
            Assert.AreEqual(0, world.CountEntities(in new QueryDescription().WithAll<AssetBundleManifestPromise>()));
        }


        public void CancelIntentionOnABStage()
        {
            //Arrange
            IWearable mockWearable = CreateMockWearable(testUrn, false, false);
            wearableCatalog.wearablesCache.Add(mockWearable.GetUrn(), mockWearable);

            var getWearablesByPointersIntention = new GetWearablesByPointersIntention(new List<URN>
                { testUrn }, BodyShape.MALE, Array.Empty<string>());

            var promise = Promise.Create(world, getWearablesByPointersIntention, PartitionComponent.TOP_PRIORITY);
            system.Update(0);

            //Mock result and start the next promise
            //Act
            MockWearableManifestResult(getWearablesByPointersIntention.CancellationTokenSource, mockWearable, false);
            system.Update(0);
            Assert.AreEqual(1, world.CountEntities(in new QueryDescription().WithAll<AssetBundlePromise>()));
            promise.ForgetLoading(world);
            system.Update(0);

            //Assert
            Assert.IsTrue(promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested);
            Assert.IsFalse(promise.Entity.IsAlive(world));

            //No AB promises should be left
            Assert.AreEqual(0, world.CountEntities(in new QueryDescription().WithAll<AssetBundlePromise>()));
        }
    }
}
