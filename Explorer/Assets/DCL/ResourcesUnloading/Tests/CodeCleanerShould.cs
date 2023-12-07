using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Textures;
using ECS.Unity.GLTFContainer.Asset.Components;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace DCL.ResourcesUnloading.Tests
{
    public class CacheCleanerShould
    {
        private CacheCleaner cacheCleaner;

        // Subs
        private IConcurrentBudgetProvider concurrentBudgetProvider;
        private IWearableCatalog wearableCatalog;
        private IWearableAssetsCache wearableAssetsCache;
        private IStreamableCache<Texture2D, GetTextureIntention> texturesCache;
        private IStreamableCache<GltfContainerAsset, string> gltfContainerAssetsCache;
        private IStreamableCache<AssetBundleData, GetAssetBundleIntention> assetBundleCache;

        [SetUp]
        public void SetUp()
        {
            concurrentBudgetProvider = Substitute.For<IConcurrentBudgetProvider>();

            wearableCatalog = Substitute.For<IWearableCatalog>();
            wearableAssetsCache = Substitute.For<IWearableAssetsCache>();

            texturesCache = Substitute.For<IStreamableCache<Texture2D, GetTextureIntention>>();
            assetBundleCache = Substitute.For<IStreamableCache<AssetBundleData, GetAssetBundleIntention>>();
            gltfContainerAssetsCache = Substitute.For<IStreamableCache<GltfContainerAsset, string>>();

            cacheCleaner = new CacheCleaner(concurrentBudgetProvider);

            cacheCleaner.Register(wearableCatalog);
            cacheCleaner.Register(texturesCache);
            cacheCleaner.Register(gltfContainerAssetsCache);
            cacheCleaner.Register(assetBundleCache);
            cacheCleaner.Register(wearableAssetsCache);
        }

        [TestCase(true, 1)]
        [TestCase(false, 0)]
        public void RespectFrameBudget(bool hasBudget, int callsAmount)
        {
            // Arrange
            concurrentBudgetProvider.TrySpendBudget().Returns(hasBudget);

            // Act
            cacheCleaner.UnloadCache();

            // Assert

            texturesCache.Received(callsAmount).Unload(concurrentBudgetProvider, Arg.Any<int>());
            wearableAssetsCache.Received(callsAmount).Unload(concurrentBudgetProvider, Arg.Any<int>());
            wearableCatalog.Received(callsAmount).Unload(Arg.Any<IConcurrentBudgetProvider>());
            gltfContainerAssetsCache.Received(callsAmount).Unload(concurrentBudgetProvider, Arg.Any<int>());
            assetBundleCache.Received(callsAmount).Unload(concurrentBudgetProvider, Arg.Any<int>());
        }
    }
}
