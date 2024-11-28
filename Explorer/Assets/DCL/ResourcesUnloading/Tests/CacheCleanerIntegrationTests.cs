using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using DCL.LOD;
using DCL.Profiles;
using ECS.StreamableLoading.NFTShapes;
using SceneRuntime.Factory.WebSceneSource.Cache;
using Unity.PerformanceTesting;
using UnityEngine;
using static Utility.Tests.TestsCategories;

namespace DCL.ResourcesUnloading.Tests
{
    public class CacheCleanerIntegrationTests
    {
        private CacheCleaner cacheCleaner;

        private IReleasablePerformanceBudget releasablePerformanceBudget;

        // Caches
        private WearableStorage wearableStorage;
        private AttachmentsAssetsCache attachmentsAssetsCache;
        private TexturesCache texturesCache;
        private AudioClipsCache audioClipsCache;
        private GltfContainerAssetsCache gltfContainerAssetsCache;
        private LODCache lodAssets;
        private RoadAssetsPool roadAssets;
        private NftShapeCache nftShapeCache;
        private IEmoteStorage emoteStorage;
        private IProfileCache profileCache;
        private ProfileIntentionCache profileIntentionCache;
        private IComponentPoolsRegistry poolsRegistry;
        private MemoryJsSourcesCache jsSourcesCache;

        private AssetBundleCache assetBundleCache;

        private IExtendedObjectPool<Material> materialPool;

        [SetUp]
        public void SetUp()
        {
            releasablePerformanceBudget = Substitute.For<IReleasablePerformanceBudget>();
            poolsRegistry = Substitute.For<IComponentPoolsRegistry>();

            texturesCache = new TexturesCache();
            audioClipsCache = new AudioClipsCache();
            assetBundleCache = new AssetBundleCache();
            gltfContainerAssetsCache = new GltfContainerAssetsCache(poolsRegistry);
            attachmentsAssetsCache = new AttachmentsAssetsCache(100, poolsRegistry);
            wearableStorage = new WearableStorage();
            lodAssets = new LODCache(new GameObjectPool<LODGroup>(new GameObject().transform));
            roadAssets = new RoadAssetsPool(new List<GameObject>());
            nftShapeCache = new NftShapeCache();
            emoteStorage = new MemoryEmotesStorage();
            profileCache = new DefaultProfileCache();
            profileIntentionCache = new ProfileIntentionCache();
            jsSourcesCache = new MemoryJsSourcesCache();

            cacheCleaner = new CacheCleaner(releasablePerformanceBudget);
            cacheCleaner.Register(texturesCache);
            cacheCleaner.Register(audioClipsCache);
            cacheCleaner.Register(gltfContainerAssetsCache);
            cacheCleaner.Register(assetBundleCache);
            cacheCleaner.Register(attachmentsAssetsCache);
            cacheCleaner.Register(wearableStorage);
            cacheCleaner.Register(lodAssets);
            cacheCleaner.Register(roadAssets);
            cacheCleaner.Register(nftShapeCache);
            cacheCleaner.Register(emoteStorage);
            cacheCleaner.Register(profileCache);
            cacheCleaner.Register(profileIntentionCache);
            cacheCleaner.Register(jsSourcesCache);
        }

        [TearDown]
        public void TearDown()
        {
            cacheCleaner.UnloadCache();

            texturesCache.Dispose();
            audioClipsCache.Dispose();
            assetBundleCache.Dispose();
            gltfContainerAssetsCache.Dispose();
            attachmentsAssetsCache.Dispose();
            wearableStorage.Unload(releasablePerformanceBudget);
            lodAssets.Unload(releasablePerformanceBudget, 3);
            roadAssets.Unload(releasablePerformanceBudget, 3);
        }

        [Performance]
        [TestCase(1)]
        [TestCase(10)]
        [TestCase(100)]
        public void CacheCleaningPerformance(int cachedElementsAmount)
        {
            // Arrange
            releasablePerformanceBudget.TrySpendBudget().Returns(true);

            for (var i = 0; i < cachedElementsAmount; i++)
                FillCachesWithElements(hashID: $"test{i}");

            // Measure
            Measure.Method(() =>
                    {
                        cacheCleaner.UnloadCache(); // Act
                    })
                   .WarmupCount(5)
                   .IterationsPerMeasurement(10)
                   .MeasurementCount(20)
                   .GC()
                   .Run();
        }

        [Test, Performance]
        [TestCase(1)]
        [TestCase(10)]
        [TestCase(100)]
        public void CacheCleaningAllocations(int cachedElementsAmount)
        {
            // Arrange
            releasablePerformanceBudget.TrySpendBudget().Returns(true);

            for (var i = 0; i < cachedElementsAmount; i++)
                FillCachesWithElements(hashID: $"test{i}");

            SampleGroup totalAllocatedMemory = new SampleGroup("TotalAllocatedMemory", SampleUnit.Kilobyte, increaseIsBetter: false);

            // Act
            long memoryBefore = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            cacheCleaner.UnloadCache();
            long memoryAfter = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();

            Measure.Custom(totalAllocatedMemory, (memoryAfter - memoryBefore) / 1024f);
        }

        [Category(INTEGRATION)]
        [Test]
        public void DisposingShouldProperlyDereferenceDependencyChain()
        {
            // Arrange
            var assetBundleData = new AssetBundleData(null, null, null, typeof(GameObject), null);

            var gltfAsset = GltfContainerAsset.Create(new GameObject(), assetBundleData);
            assetBundleData.AddReference();

            var wearableAsset = new AttachmentRegularAsset(new GameObject(), new List<AttachmentRegularAsset.RendererInfo>(5), assetBundleData);
            assetBundleData.AddReference();

            var cachedWearable = new CachedAttachment(wearableAsset, new GameObject());
            wearableAsset.AddReference();

            // Act
            cachedWearable.Dispose();
            wearableAsset.Dispose();
            gltfAsset.Dispose();

            // Assert
            Assert.That(wearableAsset.ReferenceCount, Is.EqualTo(0));
            Assert.That(assetBundleData.referenceCount, Is.EqualTo(0));
        }

        [Category(INTEGRATION)]
        [Test]
        public void ShouldCleanCachesWithRespectToReferencing()
        {
            // Arrange
            releasablePerformanceBudget.TrySpendBudget().Returns(true);
            FillCachesWithElements(hashID: "test");

            // Act
            cacheCleaner.UnloadCache();

            // Assert
            Assert.That(texturesCache.cache.Count, Is.EqualTo(0));
            Assert.That(audioClipsCache.cache.Count, Is.EqualTo(0));
            Assert.That(wearableStorage.WearableAssetsInCatalog, Is.EqualTo(0));
            Assert.That(attachmentsAssetsCache.cache.Count, Is.EqualTo(0));
            Assert.That(gltfContainerAssetsCache.cache.Count, Is.EqualTo(0));
            Assert.That(assetBundleCache.cache.Count, Is.EqualTo(0));
            Assert.That(jsSourcesCache.Count, Is.EqualTo(0));
        }

        private void FillCachesWithElements(string hashID)
        {
            var textureIntention = new GetTextureIntention { CommonArguments = new CommonLoadingArguments { URL = URLAddress.FromString(hashID) } };
            texturesCache.Add(textureIntention, new Texture2DData(new Texture2D(1, 1)));

            var audioClipIntention = new GetAudioClipIntention { CommonArguments = new CommonLoadingArguments { URL = URLAddress.FromString(hashID) } };
            var audioClip = new AudioClipData(AudioClip.Create(hashID, 1, 1, 2000, false));
            audioClipsCache.Add(audioClipIntention, audioClip);
            audioClipsCache.AddReference(in audioClipIntention, audioClip);
            audioClip.Dereference();

            var assetBundleData = new AssetBundleData(null, null, new GameObject(), typeof(GameObject), Array.Empty<AssetBundleData>());
            assetBundleCache.Add(new GetAssetBundleIntention { Hash = hashID }, assetBundleData);

            var gltfContainerAsset = GltfContainerAsset.Create(new GameObject(), assetBundleData);
            assetBundleData.AddReference();
            gltfContainerAssetsCache.Dereference(hashID, gltfContainerAsset); // add to cache

            var wearableAsset = new AttachmentRegularAsset(new GameObject(), new List<AttachmentRegularAsset.RendererInfo>(10), assetBundleData);
            assetBundleData.AddReference();
            var wearable = new Wearable { WearableAssetResults = { [0] = new StreamableLoadingResult<AttachmentAssetBase>(wearableAsset) } };
            wearableStorage.AddWearable(hashID, wearable, true); // add to cache

            var cachedWearable = new CachedAttachment(wearableAsset, new GameObject());
            wearableAsset.AddReference();
            attachmentsAssetsCache.Release(cachedWearable); // add to cache

            jsSourcesCache.Cache("a", new string('a', 1024 * 1024));
        }
    }
}
