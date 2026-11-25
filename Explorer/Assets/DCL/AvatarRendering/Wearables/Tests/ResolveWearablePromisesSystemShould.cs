using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utility;
using DCL.Web3.Identities;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using Runtime.Wearables;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Tests
{
    public class ResolveWearablePromisesSystemShould : UnitySystemTestBase<ResolveWearablePromisesSystem>
    {
        private const string WEARABLE_URN = "urn:decentraland:off-chain:base-avatars:red_hoodie";

        private WearableStorage wearableStorage = null!;
        private IWearable shirt = null!;

        [SetUp]
        public void SetUp()
        {
            wearableStorage = new WearableStorage();
            shirt = wearableStorage.GetOrAddByDTO(CreateDTO());

            system = new ResolveWearablePromisesSystem(world, wearableStorage, Substitute.For<IDecentralandUrlsSource>(), URLSubdirectory.EMPTY);
        }

        [Test]
        public void ResolveADtoOnlyRequestWithoutWaitingForAssets()
        {
            // Arrange
            Entity entity = CreateIntention(dtoOnly: true);

            // Act
            system.Update(0);

            // Assert
            Assert.IsTrue(world.Has<StreamableLoadingResult<WearablesResolution>>(entity));
            StreamableLoadingResult<WearablesResolution> result = world.Get<StreamableLoadingResult<WearablesResolution>>(entity);
            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(1, result.Asset.Wearables.Count);
        }

        [Test]
        public void WaitForTheAssetsOfARegularRequest()
        {
            // Arrange
            Entity entity = CreateIntention(dtoOnly: false);

            // Act
            system.Update(0);

            // Assert
            Assert.IsFalse(world.Has<StreamableLoadingResult<WearablesResolution>>(entity));
        }

        [Test]
        public void FinishWhenTheManifestFailedWithoutAModel()
        {
            shirt.DTO.assetBundleManifestVersion = AssetBundleManifestVersion.FAILED;
            Entity entity = CreateIntention(dtoOnly: false);

            system.Update(0);

            Assert.That(world.TryGet(entity, out StreamableLoadingResult<WearablesResolution> result), Is.True);
            Assert.That(result.Asset.Wearables, Is.Empty);
            Assert.That(world.Get<GetWearablesByPointersIntention>(entity).ResolvedWearablesIndices, Is.Zero);
            result.Asset.Dispose();
        }

        [Test]
        public void KeepTheHealthyModelInABatchWithAFailedManifest()
        {
            const string HAT_URN = "urn:decentraland:off-chain:base-avatars:hat";
            shirt.DTO.assetBundleManifestVersion = AssetBundleManifestVersion.FAILED;
            IWearable hat = wearableStorage.GetOrAddByDTO(CreateDTO(HAT_URN, WearableCategories.Categories.HAT));
            hat.DTO.assetBundleManifestVersion = AssetBundleManifestVersion.CreateManualManifest();
            var gameObject = new GameObject("hat");
            var model = new AttachmentRegularAsset(gameObject, AttachmentRegularAsset.RENDERER_INFO_POOL.Get(), Substitute.For<IStreamableRefCountData>());
            hat.WearableAssetResults[BodyShape.MALE] = new StreamableLoadingResult<AttachmentAssetBase>(model);
            Entity entity = world.Create(new GetWearablesByPointersIntention(new List<URN> { WEARABLE_URN, HAT_URN }, BodyShape.MALE, Array.Empty<string>()),
                (IPartitionComponent)PartitionComponent.TOP_PRIORITY);

            try
            {
                system.Update(0);
                system.Update(0);

                Assert.That(world.TryGet(entity, out StreamableLoadingResult<WearablesResolution> result), Is.True);
                CollectionAssert.AreEqual(new[] { hat }, result.Asset.Wearables);
                Assert.That(model.ReferenceCount, Is.EqualTo(1));
                result.Asset.Dispose();
            }
            finally
            {
                if (model.ReferenceCount > 0) model.Dereference();
                model.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public async Task EquipTheSameWearableAfterItsManifestBecomesAvailable()
        {
            var provider = new ECSWearablesProvider(Substitute.For<IWeb3IdentityCache>(), world, wearableStorage);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            UniTask<IWearable?> first = provider.GetForEquipAsync(WEARABLE_URN, BodyShape.MALE, timeout.Token);
            shirt.DTO.assetBundleManifestVersion = AssetBundleManifestVersion.FAILED;
            system.Update(0);
            Assert.That(await first, Is.Null);

            UniTask<IWearable?> retry = provider.GetForEquipAsync(WEARABLE_URN, BodyShape.MALE, timeout.Token);
            Entity manifest = SingleRequest<GetAssetBundleManifestIntention>();
            Assert.That(world.Get<GetAssetBundleManifestIntention>(manifest).CommonArguments.RetryCachedFailures, Is.True);
            world.Add(manifest, new StreamableLoadingResult<SceneAssetBundleManifest>(new SceneAssetBundleManifest("v49", "1")));

            for (var frame = 0; frame < 10 && shirt.DTO.assetBundleManifestVersion.assetBundleManifestRequestFailed; frame++)
                await UniTask.Yield();

            system.Update(0);
            Entity model = SingleRequest<GetAssetBundleIntention>();
            Assert.That(world.Get<GetAssetBundleIntention>(model).Hash, Is.EqualTo("shirt-hash" + PlatformUtils.GetCurrentPlatform()));
            var gameObject = new GameObject("shirt");
            var bundle = new AssetBundleData(null, new UnityEngine.Object[] { gameObject }, typeof(GameObject), Array.Empty<AssetBundleData>());
            bundle.AddReference();
            using var finalize = new FinalizeAssetBundleWearableLoadingSystem(world, wearableStorage, Substitute.For<IRealmData>(), new AbgenConversionMetrics());
            try
            {
                world.Add(model, new StreamableLoadingResult<AssetBundleData>(bundle));
                finalize.Update(0);
                system.Update(0);

                Assert.That(await retry, Is.SameAs(shirt));
                Assert.That(await provider.GetForEquipAsync(WEARABLE_URN, BodyShape.MALE, timeout.Token), Is.SameAs(shirt));
                var asset = (AttachmentRegularAsset)shirt.WearableAssetResults[BodyShape.MALE].Results![0]!.Value.Asset!;
                Assert.That(asset.MainAsset, Is.SameAs(gameObject));
                Assert.That(asset.ReferenceCount, Is.Zero);
            }
            finally
            {
                shirt.WearableAssetResults[BodyShape.MALE].Results?[0]?.Asset?.Dispose();
                bundle.Dispose();
            }
        }

        [Test]
        public async Task UseTheSelectedBodyShapeWhenSwitchingFromAnotherOne()
        {
            IWearable female = wearableStorage.GetOrAddByDTO(CreateDTO(BodyShape.FEMALE, WearableCategories.Categories.BODY_SHAPE, BodyShape.FEMALE));
            var gameObject = new GameObject("female");
            var asset = new AttachmentRegularAsset(gameObject, AttachmentRegularAsset.RENDERER_INFO_POOL.Get(), Substitute.For<IStreamableRefCountData>());
            female.WearableAssetResults[BodyShape.FEMALE] = new StreamableLoadingResult<AttachmentAssetBase>(asset);
            var provider = new ECSWearablesProvider(Substitute.For<IWeb3IdentityCache>(), world, wearableStorage);

            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                Assert.That(await provider.GetForEquipAsync(BodyShape.FEMALE, BodyShape.MALE, timeout.Token), Is.SameAs(female));
                Assert.That(asset.ReferenceCount, Is.Zero);
            }
            finally
            {
                asset.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private Entity SingleRequest<T>()
        {
            QueryDescription query = new QueryDescription().WithAll<T>();
            Assert.That(world.CountEntities(query), Is.EqualTo(1), typeof(T).Name);
            var entities = new Entity[1];
            world.GetEntities(query, entities);
            return entities[0];
        }

        private Entity CreateIntention(bool dtoOnly) =>
            world.Create(new GetWearablesByPointersIntention(new List<URN> { new (WEARABLE_URN) }, BodyShape.MALE, Array.Empty<string>(), dtoOnly: dtoOnly),
                (IPartitionComponent)PartitionComponent.TOP_PRIORITY);

        private static WearableDTO CreateDTO(string urn = WEARABLE_URN, string category = WearableCategories.Categories.UPPER_BODY, string? bodyShape = null) =>
            new ()
            {
                id = urn,
                content = new[] { new ContentDefinition { file = "shirt.glb", hash = "shirt-hash" } },
                metadata = new WearableDTO.WearableMetadataDto
                {
                    id = urn,
                    data = new WearableDTO.WearableMetadataDto.DataDto
                    {
                        category = category,
                        hides = Array.Empty<string>(),
                        replaces = Array.Empty<string>(),
                        representations = new[]
                        {
                            new AvatarAttachmentDTO.Representation
                            {
                                mainFile = "shirt.glb",
                                contents = new[] { "shirt.glb" },
                                bodyShapes = new[] { bodyShape ?? BodyShape.MALE.Value },
                                overrideHides = Array.Empty<string>(),
                                overrideReplaces = Array.Empty<string>(),
                            },
                        },
                    },
                },
            };
    }
}
