using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape.Tests.EditMode;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.Diagnostics;
using ECS;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Tests;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.TestTools;
using Promise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.Wearable[], DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;
using AssetBundleManifestPromise = ECS.StreamableLoading.Common.AssetPromise<SceneRunner.Scene.SceneAssetBundleManifest, DCL.AvatarRendering.Wearables.Components.GetWearableAssetBundleManifestIntention>;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.AvatarRendering.Wearables.Tests
{
    [TestFixture]
    public class ResolveWearableByPointerSystemShould : UnitySystemTestBase<FinalizeWearableLoadingSystem>
    {
        [SetUp]
        public void Setup()
        {
            mockedABManifest = new StreamableLoadingResult<SceneAssetBundleManifest>(new SceneAssetBundleManifest(URLDomain.EMPTY, "0", Array.Empty<string>()));

            wearableStorage = new WearableStorage();

            mockedAB = new StreamableLoadingResult<AttachmentAssetBase>(new AttachmentRegularAsset(null, null, null));

            mockedDefaultAB = new StreamableLoadingResult<AttachmentAssetBase>(new AttachmentRegularAsset(null, null, null));

            IWearable mockDefaultWearable = CreateMockWearable(defaultWearableUrn, false, true);

            wearableStorage.wearablesCache.Add(mockDefaultWearable.GetUrn(), mockDefaultWearable);

            world.Create(new DefaultWearablesComponent
            {
                ResolvedState = DefaultWearablesComponent.State.Success,
            });

            system = new FinalizeWearableLoadingSystem(world, wearableStorage, new RealmData(new TestIpfsRealm()), URLSubdirectory.EMPTY);
            system.Initialize();
        }

        private WearableStorage wearableStorage;
        private readonly string testUrn = "urn:decentraland:off-chain:base-avatars:red_hoodie";
        private readonly string unisexTestUrn = "urn:decentraland:off-chain:base-avatars:red_hoodie_unisex";
        private readonly string defaultWearableUrn = "urn:decentraland:off-chain:base-avatars:green_hoodie";

        private StreamableLoadingResult<AttachmentAssetBase> mockedDefaultAB;
        private StreamableLoadingResult<SceneAssetBundleManifest> mockedABManifest;
        private StreamableLoadingResult<AttachmentAssetBase> mockedAB;

        private IWearable CreateMockWearable(URN urn, bool isUnisex, bool isDefaultWearable)
        {
            var wearableAssets = new WearableAssets[BodyShape.COUNT];

            if (isDefaultWearable)
                wearableAssets[BodyShape.MALE] = mockedDefaultAB;

            return new FakeWearable(
                new WearableDTO
                {
                    id = urn,
                    metadata = new WearableDTO.WearableMetadataDto
                    {
                        id = urn,
                        data =
                        {
                            representations = isUnisex
                                ? new[] { AvatarAttachmentDTO.Representation.NewFakeRepresentation(), AvatarAttachmentDTO.Representation.NewFakeRepresentation() }
                                : new[] { AvatarAttachmentDTO.Representation.NewFakeRepresentation() },
                            category = WearablesConstants.Categories.UPPER_BODY,
                        },
                    }
                },
                model: new StreamableLoadingResult<WearableDTO>(new WearableDTO { id = urn }),
                mainHash: "mockedHash",
                wearableAssetResults: wearableAssets
            );
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
            world.Add(assetBundleManifestPromiseEntity, failed ? new StreamableLoadingResult<SceneAssetBundleManifest>(ReportData.UNSPECIFIED, new Exception("FAILED")) : mockedABManifest);
            system!.Update(0);
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
            world.Add(assetBundlePromiseEntity, failed ? new StreamableLoadingResult<SceneAssetBundleManifest>(ReportData.UNSPECIFIED, new Exception("FAILED")) : mockedAB);
        }

        [Test]
        public void ResolveWearable()
        {
            LogAssert.ignoreFailingMessages = true;

            //Arrange
            IWearable mockWearable = CreateMockWearable(testUrn, false, false);
            wearableStorage.wearablesCache.Add(mockWearable.GetUrn(), mockWearable);

            var getWearablesByPointersIntention = new GetWearablesByPointersIntention(new List<URN>
                { testUrn }, BodyShape.MALE, Array.Empty<string>());

            Promise.Create(world, getWearablesByPointersIntention, PartitionComponent.TOP_PRIORITY);
            system!.Update(0);

            //Act
            MockWearableManifestResult(getWearablesByPointersIntention.CancellationTokenSource, mockWearable, false);
            MockABResult(getWearablesByPointersIntention.CancellationTokenSource, mockWearable, false);

            //Assert
            mockWearable.WearableAssetResults[BodyShape.MALE] = mockedAB;
            mockWearable.ManifestResult = mockedABManifest;
        }

        [Test]
        public void ResolveUnisexWearable()
        {
            LogAssert.ignoreFailingMessages = true;

            //Arrange
            IWearable mockUnisexWearable = CreateMockWearable(unisexTestUrn, false, true);
            wearableStorage.wearablesCache.Add(mockUnisexWearable.GetUrn(), mockUnisexWearable);

            var getWearablesByPointersIntention = new GetWearablesByPointersIntention(new List<URN>
                { unisexTestUrn }, BodyShape.MALE, Array.Empty<string>());

            Promise.Create(world, getWearablesByPointersIntention, PartitionComponent.TOP_PRIORITY);
            system!.Update(0);

            //Act
            MockWearableManifestResult(getWearablesByPointersIntention.CancellationTokenSource, mockUnisexWearable, false);
            MockABResult(getWearablesByPointersIntention.CancellationTokenSource, mockUnisexWearable, false);

            //Assert
            mockUnisexWearable.WearableAssetResults[BodyShape.MALE] = mockedAB;
            mockUnisexWearable.WearableAssetResults[BodyShape.FEMALE] = mockedAB;
            mockUnisexWearable.ManifestResult = mockedABManifest;
        }

        [Test]
        public void ResolveDefaultWearableOnManifestFail()
        {
            LogAssert.ignoreFailingMessages = true;

            //Arrange
            IWearable mockWearable = CreateMockWearable(testUrn, false, false);
            wearableStorage.wearablesCache.Add(mockWearable.GetUrn(), mockWearable);

            var getWearablesByPointersIntention = new GetWearablesByPointersIntention(new List<URN>
                { testUrn }, BodyShape.MALE, Array.Empty<string>());

            Promise.Create(world, getWearablesByPointersIntention, PartitionComponent.TOP_PRIORITY);

            //Act
            system!.Update(0);
            MockWearableManifestResult(getWearablesByPointersIntention.CancellationTokenSource, mockWearable, true);

            //Assert
            mockWearable.WearableAssetResults[BodyShape.MALE] = mockedDefaultAB;
            mockWearable.ManifestResult = mockedABManifest;
        }

        [Test]
        public void ResolveDefaultWearableOnABFail()
        {
            LogAssert.ignoreFailingMessages = true;

            //Arrange
            IWearable mockWearable = CreateMockWearable(testUrn, false, false);
            wearableStorage.wearablesCache.Add(mockWearable.GetUrn(), mockWearable);

            var getWearablesByPointersIntention = new GetWearablesByPointersIntention(new List<URN>
                { testUrn }, BodyShape.MALE, Array.Empty<string>());

            Promise.Create(world, getWearablesByPointersIntention, PartitionComponent.TOP_PRIORITY);
            system!.Update(0);

            //Act
            MockWearableManifestResult(getWearablesByPointersIntention.CancellationTokenSource, mockWearable, false);
            MockABResult(getWearablesByPointersIntention.CancellationTokenSource, mockWearable, true);

            //Assert
            mockWearable.WearableAssetResults[BodyShape.MALE] = mockedDefaultAB;
            mockWearable.ManifestResult = mockedABManifest;
        }

        [Test]
        public void CancelIntentionOnManifestStage()
        {
            LogAssert.ignoreFailingMessages = true;

            //Arrange
            IWearable mockWearable = CreateMockWearable(testUrn, false, false);
            wearableStorage.wearablesCache.Add(mockWearable.GetUrn(), mockWearable);

            var getWearablesByPointersIntention
                = new GetWearablesByPointersIntention(new List<URN>
                    { testUrn }, BodyShape.MALE, Array.Empty<string>());

            var promise = Promise.Create(world, getWearablesByPointersIntention, PartitionComponent.TOP_PRIORITY);
            system!.Update(0);

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

        [Test]
        public void CancelIntentionOnABStage()
        {
            LogAssert.ignoreFailingMessages = true;

            //Arrange
            IWearable mockWearable = CreateMockWearable(testUrn, false, false);
            wearableStorage.AddToInternalCache(mockWearable);

            var getWearablesByPointersIntention = new GetWearablesByPointersIntention(new List<URN>
                { testUrn }, BodyShape.MALE, Array.Empty<string>());

            var promise = Promise.Create(world, getWearablesByPointersIntention, PartitionComponent.TOP_PRIORITY);
            system!.Update(0);
            Assert.AreEqual(0, world.CountEntities(in new QueryDescription().WithAll<AssetBundlePromise>()));

            //Mock result and start the next promise
            //Act
            MockWearableManifestResult(getWearablesByPointersIntention.CancellationTokenSource, mockWearable, false);
            Assert.AreEqual(0, world.CountEntities(in new QueryDescription().WithAll<AssetBundlePromise>()));
            system.Update(0);
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
