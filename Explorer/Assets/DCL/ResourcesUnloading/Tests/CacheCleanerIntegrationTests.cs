using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.PerformanceTesting;
using UnityEngine;

namespace DCL.ResourcesUnloading.Tests
{
    public class CacheCleanerIntegrationTests
    {
        private CacheCleaner cacheCleaner;

        private IConcurrentBudgetProvider concurrentBudgetProvider;

        // Caches
        private WearableCatalog wearableCatalog;
        private WearableAssetsCache wearableAssetsCache;
        private TexturesCache texturesCache;
        private GltfContainerAssetsCache gltfContainerAssetsCache;
        private AssetBundleCache assetBundleCache;

        private IExtendedObjectPool<Material> materialPool;

        [SetUp]
        public void SetUp()
        {
            concurrentBudgetProvider = Substitute.For<IConcurrentBudgetProvider>();

            texturesCache = new TexturesCache();
            assetBundleCache = new AssetBundleCache();
            gltfContainerAssetsCache = new GltfContainerAssetsCache();
            wearableAssetsCache = new WearableAssetsCache(100);
            wearableCatalog = new WearableCatalog();

            cacheCleaner = new CacheCleaner(concurrentBudgetProvider);
            cacheCleaner.Register(texturesCache);
            cacheCleaner.Register(gltfContainerAssetsCache);
            cacheCleaner.Register(assetBundleCache);
            cacheCleaner.Register(wearableAssetsCache);
            cacheCleaner.Register(wearableCatalog);
        }

        [Test]
        public void DisposingShouldProperlyDereferenceDependencyChain()
        {
            // Arrange
            var assetBundleData = new AssetBundleData(null, null, null, null);

            var gltfAsset = GltfContainerAsset.Create(new GameObject(), assetBundleData);
            assetBundleData.AddReference();

            var wearableAsset = new WearableAsset(new GameObject(), new List<WearableAsset.RendererInfo>(5), assetBundleData);
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

        [Test]
        public void ShouldCleanCachesWithRespectToReferencing()
        {
            // Arrange
            concurrentBudgetProvider.TrySpendBudget().Returns(true);
            FillCachesWithElements(amount: 5);

            // Act
            cacheCleaner.UnloadCache();

            // Assert
            Assert.That(texturesCache.cache.Count, Is.EqualTo(0));
            Assert.That(wearableCatalog.WearableAssetsInCatalog, Is.EqualTo(0));
            Assert.That(wearableAssetsCache.cache.Count, Is.EqualTo(0));
            Assert.That(gltfContainerAssetsCache.cache.Count, Is.EqualTo(0));
            Assert.That(assetBundleCache.cache.Count, Is.EqualTo(0));
        }

        [Test] [Performance]
        public void PerformanceMeasureWithEmptyCaches()
        {
            // Arrange
            concurrentBudgetProvider.TrySpendBudget().Returns(true);

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

        [Performance]
        [TestCase(1)]
        [TestCase(10)]
        [TestCase(100)]
        public void PerformanceMeasureWithFullCaches(int cachedElementsAmount)
        {
            // Arrange
            concurrentBudgetProvider.TrySpendBudget().Returns(true);

            FillCachesWithElements(cachedElementsAmount);

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

        private void FillCachesWithElements(int amount)
        {
            for (var i = 0; i < amount; i++)
            {
                var hashID = $"test {i}";

                var textureIntention = new GetTextureIntention { CommonArguments = new CommonLoadingArguments { URL = new URLAddress(hashID) } };
                texturesCache.Add(textureIntention, new Texture2D(1, 1));

                var assetBundleData = new AssetBundleData(null, null, new GameObject(), Array.Empty<AssetBundleData>());
                assetBundleCache.Add(new GetAssetBundleIntention { Hash = hashID }, assetBundleData);

                var gltfContainerAsset = GltfContainerAsset.Create(new GameObject(), assetBundleData);
                assetBundleData.AddReference();
                gltfContainerAssetsCache.Dereference(hashID, gltfContainerAsset);

                var wearableAsset = new WearableAsset(new GameObject(), new List<WearableAsset.RendererInfo>(10), assetBundleData);
                assetBundleData.AddReference();
                var wearable = new Wearable { WearableAssetResults = { [0] = new StreamableLoadingResult<WearableAsset>(wearableAsset) } };
                wearableCatalog.AddWearable(hashID, wearable);

                var cachedWearable = new CachedWearable(wearableAsset, new GameObject());
                wearableAsset.AddReference();
                wearableAssetsCache.Release(cachedWearable);
            }
        }
    }
}
