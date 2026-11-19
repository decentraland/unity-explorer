using Arch.Core;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.Diagnostics;
using DCL.Ipfs;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using Runtime.Wearables;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;
using RawGltfPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.GLTF.GLTFData, ECS.StreamableLoading.GLTF.GetGLTFIntention>;

namespace DCL.AvatarRendering.Wearables.Tests
{
    public class FinalizeAssetBundleWearableLoadingSystemShould : UnitySystemTestBase<FinalizeAssetBundleWearableLoadingSystem>
    {
        private const string HAT_PATH = "assets/wearables/cap_hat.glb";
        private const string HAT_HASH = "bafkreihathash";

        private static readonly QueryDescription AB_PROMISES = new QueryDescription().WithAll<AssetBundlePromise>();
        private static readonly QueryDescription RAW_GLTF_PROMISES = new QueryDescription().WithAll<RawGltfPromise, IWearable, BodyShape, int>();

        private AbgenConversionMetrics metrics = null!;

        [SetUp]
        public void SetUp()
        {
            metrics = new AbgenConversionMetrics();
            system = new FinalizeAssetBundleWearableLoadingSystem(world, new WearableStorage(), Substitute.For<IRealmData>(), metrics);
        }

        [Test]
        public void FallBackToRawGltfWhenSceneWearableBundleFails()
        {
            // Arrange
            LogAssert.ignoreFailingMessages = true; // A failed loading result logs its exception on construction.
            IWearable wearable = CreateWearable(new LocalSceneWearableDTO(null, true));
            Entity promiseEntity = CreateAssetBundlePromise(wearable, new StreamableLoadingResult<AssetBundleData>(ReportData.UNSPECIFIED, new Exception("bundle missing")));

            // Act
            system.Update(0);

            // Assert
            Assert.IsFalse(world.IsAlive(promiseEntity));
            Assert.AreEqual(0, world.CountEntities(in AB_PROMISES));
            AssertRawGltfRequested(wearable);
            Assert.IsNull(wearable.WearableAssetResults[BodyShape.MALE].Results![WearablePolymorphicBehaviour.MAIN_ASSET_INDEX]);
            Assert.AreEqual(1, metrics.Failed);
        }

        [Test]
        public void ReleaseBundleAndFallBackToRawGltfWhenSceneWearableBundleHasNoGameObject()
        {
            // Arrange
            LogAssert.ignoreFailingMessages = true;
            IWearable wearable = CreateWearable(new LocalSceneWearableDTO(null, true));
            var bundle = new AssetBundleData(null!, Array.Empty<UnityEngine.Object>(), typeof(GameObject), Array.Empty<AssetBundleData>());
            bundle.AddReference();
            CreateAssetBundlePromise(wearable, new StreamableLoadingResult<AssetBundleData>(bundle));

            // Act
            system.Update(0);

            // Assert
            Assert.AreEqual(0, bundle.referenceCount);
            Assert.AreEqual(0, world.CountEntities(in AB_PROMISES));
            AssertRawGltfRequested(wearable);
        }

        [Test]
        public void FailWearableWithoutRawGltfFallback()
        {
            // Arrange
            LogAssert.ignoreFailingMessages = true;
            IWearable wearable = CreateWearable(new WearableDTO());
            CreateAssetBundlePromise(wearable, new StreamableLoadingResult<AssetBundleData>(ReportData.UNSPECIFIED, new Exception("bundle missing")));

            // Act
            system.Update(0);

            // Assert
            Assert.AreEqual(0, world.CountEntities(in AB_PROMISES));
            Assert.AreEqual(0, world.CountEntities(in RAW_GLTF_PROMISES));
            Assert.IsFalse(wearable.WearableAssetResults[BodyShape.MALE].Results![WearablePolymorphicBehaviour.MAIN_ASSET_INDEX]!.Value.Succeeded);
            Assert.AreEqual(0, metrics.Failed);
        }

        private static IWearable CreateWearable(WearableDTO dto)
        {
            dto.id = "bafkreisceneid";
            dto.content = new[] { new ContentDefinition { file = HAT_PATH, hash = HAT_HASH } };

            dto.metadata = new WearableDTO.WearableMetadataDto
            {
                id = "urn:decentraland:off-chain:scene-wearable:test",
                data = new WearableDTO.WearableMetadataDto.DataDto
                {
                    category = WearableCategories.Categories.HAT,
                    representations = new[]
                    {
                        new AvatarAttachmentDTO.Representation
                        {
                            bodyShapes = new[] { BodyShape.MALE.Value, BodyShape.FEMALE.Value },
                            mainFile = HAT_PATH,
                            contents = new[] { HAT_PATH },
                            overrideHides = Array.Empty<string>(),
                            overrideReplaces = Array.Empty<string>(),
                        },
                    },
                },
            };

            IWearable wearable = new Wearable(new StreamableLoadingResult<WearableDTO>(dto));
            wearable.WearableAssetResults[BodyShape.MALE].Results = new StreamableLoadingResult<AttachmentAssetBase>?[1];
            wearable.WearableAssetResults[BodyShape.FEMALE].Results = new StreamableLoadingResult<AttachmentAssetBase>?[1];
            wearable.UpdateLoadingStatus(true);
            return wearable;
        }

        private Entity CreateAssetBundlePromise(IWearable wearable, StreamableLoadingResult<AssetBundleData> result)
        {
            var promise = AssetBundlePromise.Create(world,
                GetAssetBundleIntention.FromHash(HAT_HASH, AssetBundleManifestVersion.CreateManualManifest(), typeof(GameObject), cancellationTokenSource: new CancellationTokenSource()),
                PartitionComponent.TOP_PRIORITY);

            world.Add(promise.Entity, result);
            world.Create(promise, wearable, BodyShape.MALE, WearablePolymorphicBehaviour.MAIN_ASSET_INDEX);
            return promise.Entity;
        }

        private void AssertRawGltfRequested(IWearable wearable)
        {
            Assert.AreEqual(1, world.CountEntities(in RAW_GLTF_PROMISES));

            world.Query(in RAW_GLTF_PROMISES, (ref RawGltfPromise promise, ref IWearable requested, ref int index) =>
            {
                Assert.AreSame(wearable, requested);
                Assert.AreEqual(WearablePolymorphicBehaviour.MAIN_ASSET_INDEX, index);
                Assert.AreEqual(HAT_PATH, promise.LoadingIntention.Name);
                Assert.AreEqual(HAT_HASH, promise.LoadingIntention.Hash);
            });

            Assert.IsTrue(wearable.IsLoading);
        }
    }
}
