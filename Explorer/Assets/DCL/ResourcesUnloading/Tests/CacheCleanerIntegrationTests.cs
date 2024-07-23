using CommunicationData.URLHelpers;
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
        private WearableCatalog wearableCatalog;
        private WearableAssetsCache wearableAssetsCache;
        private TexturesCache texturesCache;
        private AudioClipsCache audioClipsCache;
        private GltfContainerAssetsCache gltfContainerAssetsCache;
        private LODAssetsPool lodAssets;
        private RoadAssetsPool roadAssets;


        private AssetBundleCache assetBundleCache;

        private IExtendedObjectPool<Material> materialPool;

        [SetUp]
        public void SetUp()
        {
            releasablePerformanceBudget = Substitute.For<IReleasablePerformanceBudget>();

            texturesCache = new TexturesCache();
            audioClipsCache = new AudioClipsCache();
            assetBundleCache = new AssetBundleCache();
            gltfContainerAssetsCache = new GltfContainerAssetsCache();
            wearableAssetsCache = new WearableAssetsCache(100);
            wearableCatalog = new WearableCatalog();
            lodAssets = new LODAssetsPool();
            roadAssets = new RoadAssetsPool(new List<GameObject>());


            cacheCleaner = new CacheCleaner(releasablePerformanceBudget);
            cacheCleaner.Register(texturesCache);
            cacheCleaner.Register(audioClipsCache);
            cacheCleaner.Register(gltfContainerAssetsCache);
            cacheCleaner.Register(assetBundleCache);
            cacheCleaner.Register(wearableAssetsCache);
            cacheCleaner.Register(wearableCatalog);
            cacheCleaner.Register(lodAssets);
            cacheCleaner.Register(roadAssets);
        }

        [TearDown]
        public void TearDown()
        {
            cacheCleaner.UnloadCache();

            texturesCache.Dispose();
            audioClipsCache.Dispose();
            assetBundleCache.Dispose();
            gltfContainerAssetsCache.Dispose();
            wearableAssetsCache.Dispose();
            wearableCatalog.Unload(releasablePerformanceBudget);
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

        [Category(INTEGRATION)]
        [Test]
        public void DisposingShouldProperlyDereferenceDependencyChain()
        {
            // Arrange
            var assetBundleData = new AssetBundleData(null, null, null, typeof(GameObject), null);

            var gltfAsset = GltfContainerAsset.Create(new GameObject(), assetBundleData);
            assetBundleData.AddReference();

            var wearableAsset = new WearableRegularAsset(new GameObject(), new List<WearableRegularAsset.RendererInfo>(5), assetBundleData);
            assetBundleData.AddReference();

            var cachedWearable = new CachedWearable(wearableAsset, new GameObject());
            wearableAsset.AddReference();

            // Act
            cachedWearable.Dispose();
            wearableAsset.Dispose();
            gltfAsset.Dispose();

            // Assert
            Assert.That(wearableAsset.ReferenceCount, Is.EqualTo(0));
            Assert.That(assetBundleData.referencesCount, Is.EqualTo(0));
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
            Assert.That(wearableCatalog.WearableAssetsInCatalog, Is.EqualTo(0));
            Assert.That(wearableAssetsCache.cache.Count, Is.EqualTo(0));
            Assert.That(gltfContainerAssetsCache.cache.Count, Is.EqualTo(0));
            Assert.That(assetBundleCache.cache.Count, Is.EqualTo(0));
        }

        private void FillCachesWithElements(string hashID)
        {
            var textureIntention = new GetTextureIntention { CommonArguments = new CommonLoadingArguments { URL = URLAddress.FromString(hashID) } };
            texturesCache.Add(textureIntention, new Texture2D(1, 1));

            var audioClipIntention = new GetAudioClipIntention { CommonArguments = new CommonLoadingArguments { URL = URLAddress.FromString(hashID) } };
            var audioClip = AudioClip.Create(hashID, 1, 1, 2000, false);
            audioClipsCache.Add(audioClipIntention, audioClip);
            audioClipsCache.AddReference(in audioClipIntention, audioClip);
            audioClipsCache.Dereference(audioClipIntention, audioClip);

            var assetBundleData = new AssetBundleData(null, null, new GameObject(), typeof(GameObject), Array.Empty<AssetBundleData>());
            assetBundleCache.Add(new GetAssetBundleIntention { Hash = hashID }, assetBundleData);

            var gltfContainerAsset = GltfContainerAsset.Create(new GameObject(), assetBundleData);
            assetBundleData.AddReference();
            gltfContainerAssetsCache.Dereference(hashID, gltfContainerAsset); // add to cache

            var wearableAsset = new WearableRegularAsset(new GameObject(), new List<WearableRegularAsset.RendererInfo>(10), assetBundleData);
            assetBundleData.AddReference();
            var wearable = new Wearable { WearableAssetResults = { [0] = new StreamableLoadingResult<WearableAssetBase>(wearableAsset) } };
            wearableCatalog.AddWearable(hashID, wearable, true); // add to cache

            var cachedWearable = new CachedWearable(wearableAsset, new GameObject());
            wearableAsset.AddReference();
            wearableAssetsCache.Release(cachedWearable); // add to cache
        }
    }
}
