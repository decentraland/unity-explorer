using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Textures;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using Unity.PerformanceTesting;
using UnityEngine;

namespace DCL.ResourcesUnloading.Tests
{
    public class CacheCleanerIntegrationTests
    {
        private CacheCleaner cacheCleaner;

        // Subs
        private IConcurrentBudgetProvider concurrentBudgetProvider;
        private IWearableCatalog wearableCatalog;
        private IWearableAssetsCache wearableAssetsCache;
        private IStreamableCache<Texture2D, GetTextureIntention> texturesCache;
        private IStreamableCache<GltfContainerAsset, string> gltfContainerAssetsCache;
        private IStreamableCache<AssetBundleData, GetAssetBundleIntention> assetBundleCache;

        private IExtendedObjectPool<Material> materialPool;

        [SetUp]
        public void SetUp()
        {
            concurrentBudgetProvider = Substitute.For<IConcurrentBudgetProvider>();
            cacheCleaner = new CacheCleaner(concurrentBudgetProvider);

            texturesCache = new TexturesCache();
            assetBundleCache = new AssetBundleCache();
            gltfContainerAssetsCache = new GltfContainerAssetsCache();
            wearableAssetsCache = new WearableAssetsCache(100);
            wearableCatalog = new WearableCatalog();

            cacheCleaner.Register(texturesCache);
            cacheCleaner.Register(gltfContainerAssetsCache);
            cacheCleaner.Register(assetBundleCache);
            cacheCleaner.Register(wearableAssetsCache);
            cacheCleaner.Register(wearableCatalog);
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

        [Test] [Performance]
        public void PerformanceMeasureWithFullCaches()
        {
            // Arrange
            concurrentBudgetProvider.TrySpendBudget().Returns(true);

            for (var i = 0; i < 5; i++)
            {
                texturesCache.Add(new GetTextureIntention(), new Texture2D(1, 1));
                wearableCatalog.AddEmptyWearable("test" + i);

                var abIntention = new GetAssetBundleIntention
                {
                    Hash = "test" + i,
                };

                var assetBundleData = new AssetBundleData(null, null, null, null);
                assetBundleData.AddReference();

                assetBundleCache.Add(abIntention, assetBundleData);
                gltfContainerAssetsCache.Add("test" + i, GltfContainerAsset.Create(new GameObject(), assetBundleData));

                var wearableAsset = new WearableAsset(new GameObject(), new List<WearableAsset.RendererInfo>(5), assetBundleData);
                assetBundleData.AddReference();

                var cachedWearable = new CachedWearable(wearableAsset, new GameObject());
                wearableAsset.AddReference();
                wearableAssetsCache.Release(cachedWearable);
            }

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
    }
}
