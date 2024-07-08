using DCL.AvatarRendering.Wearables.Helpers;
using DCL.LOD;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.Profiles;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Textures;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace DCL.ResourcesUnloading.Tests
{
    public class CacheCleanerUnitTests
    {
        private CacheCleaner cacheCleaner;

        // Mocks
        private IReleasablePerformanceBudget releasablePerformanceBudget;
        private IWearableCatalog wearableCatalog;
        private IWearableAssetsCache wearableAssetsCache;
        private IStreamableCache<Texture2D, GetTextureIntention> texturesCache;
        private IStreamableCache<AudioClip, GetAudioClipIntention> audioClipsCache;
        private IGltfContainerAssetsCache gltfContainerAssetsCache;
        private IStreamableCache<AssetBundleData, GetAssetBundleIntention> assetBundleCache;
        private IExtendedObjectPool<Material> materialPool;
        private IProfileCache profileCache;
        private ILODAssetsPool lodAssetsPool;
        private IRoadAssetPool roadAssetPool;



        [SetUp]
        public void SetUp()
        {
            releasablePerformanceBudget = Substitute.For<IReleasablePerformanceBudget>();

            materialPool = Substitute.For<IExtendedObjectPool<Material>>();

            wearableCatalog = Substitute.For<IWearableCatalog>();
            wearableAssetsCache = Substitute.For<IWearableAssetsCache>();

            texturesCache = Substitute.For<IStreamableCache<Texture2D, GetTextureIntention>>();
            audioClipsCache = Substitute.For<IStreamableCache<AudioClip, GetAudioClipIntention>>();
            assetBundleCache = Substitute.For<IStreamableCache<AssetBundleData, GetAssetBundleIntention>>();
            gltfContainerAssetsCache = Substitute.For<IGltfContainerAssetsCache>();
            profileCache = Substitute.For<IProfileCache>();
            lodAssetsPool = Substitute.For<ILODAssetsPool>();
            roadAssetPool = Substitute.For<IRoadAssetPool>();


            cacheCleaner = new CacheCleaner(releasablePerformanceBudget);

            cacheCleaner.Register(wearableCatalog);
            cacheCleaner.Register(texturesCache);
            cacheCleaner.Register(audioClipsCache);
            cacheCleaner.Register(gltfContainerAssetsCache);
            cacheCleaner.Register(assetBundleCache);
            cacheCleaner.Register(wearableAssetsCache);
            cacheCleaner.Register(materialPool);
            cacheCleaner.Register(profileCache);
            cacheCleaner.Register(lodAssetsPool);
            cacheCleaner.Register(roadAssetPool);
        }

        [TestCase(true, 1)]
        [TestCase(false, 0)]
        public void ShouldUnloadOnlyWhenHasFrameBudget(bool hasBudget, int callsAmount)
        {
            // Arrange
            releasablePerformanceBudget.TrySpendBudget().Returns(hasBudget);

            // Act
            cacheCleaner.UnloadCache();

            // Assert
            texturesCache.Received(callsAmount).Unload(releasablePerformanceBudget, Arg.Any<int>());
            audioClipsCache.Received(callsAmount).Unload(releasablePerformanceBudget, Arg.Any<int>());
            wearableAssetsCache.Received(callsAmount).Unload(releasablePerformanceBudget, Arg.Any<int>());
            wearableCatalog.Received(callsAmount).Unload(Arg.Any<IReleasablePerformanceBudget>());
            gltfContainerAssetsCache.Received(callsAmount).Unload(releasablePerformanceBudget, Arg.Any<int>());
            assetBundleCache.Received(callsAmount).Unload(releasablePerformanceBudget, Arg.Any<int>());
            materialPool.Received(callsAmount).ClearThrottled(Arg.Any<int>());
            profileCache.Received(callsAmount).Unload(releasablePerformanceBudget, Arg.Any<int>());
        }
    }
}
