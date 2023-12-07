using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Textures;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace DCL.ResourcesUnloading.Tests
{
    public class CacheCleanerTest
    {
        private CacheCleaner cacheCleaner;

        private IConcurrentBudgetProvider concurrentBudgetProvider;

        private IWearableCatalog wearableCatalog;
        private IStreamableCache<Texture2D, GetTextureIntention> texturesCache;
        private IStreamableCache<GltfContainerAsset, string> gltfContainerAssetsCache;
        private IStreamableCache<AssetBundleData, GetAssetBundleIntention> assetBundleCache;
        private IWearableAssetsCache wearableAssetsCache;

        [SetUp]
        public void SetUp()
        {
            concurrentBudgetProvider = Substitute.For<IConcurrentBudgetProvider>();

            wearableAssetsCache = Substitute.For<IWearableAssetsCache>();
            wearableCatalog = Substitute.For<IWearableCatalog>();

            texturesCache = Substitute.For<TexturesCache>();
            assetBundleCache = Substitute.For<AssetBundleCache>();
            gltfContainerAssetsCache = Substitute.For<GltfContainerAssetsCache>();

            cacheCleaner = new CacheCleaner(concurrentBudgetProvider);

            cacheCleaner.Register(wearableCatalog);
            cacheCleaner.Register(texturesCache);
            cacheCleaner.Register(gltfContainerAssetsCache);
            cacheCleaner.Register(assetBundleCache);
            cacheCleaner.Register(wearableAssetsCache);
        }

        [TestCase(true, 1)]
        [TestCase(false, 0)]
        public void UnloadCallShouldBeBudgeted(bool hasBudget, int unloadCallsAmount)
        {
            // Arrange
            concurrentBudgetProvider.TrySpendBudget().Returns(hasBudget);

            // Act
            cacheCleaner.UnloadCache();

            // Assert

            // texturesCache.Received(callsAmount).Unload(concurrentBudgetProvider, Arg.Any<int>());
            // wearableAssetsCache.Received(callsAmount).Unload(concurrentBudgetProvider, Arg.Any<int>());
            wearableCatalog.Received(unloadCallsAmount).Unload(Arg.Any<IConcurrentBudgetProvider>());

            // gltfContainerAssetsCache.Received(callsAmount).Unload(concurrentBudgetProvider, Arg.Any<int>());
            // assetBundleCache.Received(callsAmount).Unload(concurrentBudgetProvider, Arg.Any<int>());
        }
    }
}
