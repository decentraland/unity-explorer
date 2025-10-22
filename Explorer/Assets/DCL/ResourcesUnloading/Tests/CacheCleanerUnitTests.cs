﻿using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.LOD;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.Profiles;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Cache.InMemory;
using ECS.StreamableLoading.Textures;
using ECS.Unity.GLTFContainer.Asset.Cache;
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
        private IWearableStorage wearableStorage;
        private IAttachmentsAssetsCache attachmentsAssetsCache;
        private ISizedStreamableCache<TextureData, GetTextureIntention> texturesCache;
        private IStreamableCache<AudioClipData, GetAudioClipIntention> audioClipsCache;
        private IGltfContainerAssetsCache gltfContainerAssetsCache;
        private IStreamableCache<AssetBundleData, GetAssetBundleIntention> assetBundleCache;
        private IExtendedObjectPool<Material> materialPool;
        private IProfileCache profileCache;
        private ILODCache lodAssetsPool;
        private IRoadAssetPool roadAssetPool;
        private IEmoteStorage emoteStorage;
        private IMemoryCache<string, string> jsSourcesCache;

        [SetUp]
        public void SetUp()
        {
            releasablePerformanceBudget = Substitute.For<IReleasablePerformanceBudget>();

            materialPool = Substitute.For<IExtendedObjectPool<Material>>();

            wearableStorage = Substitute.For<IWearableStorage>();
            attachmentsAssetsCache = Substitute.For<IAttachmentsAssetsCache>();

            texturesCache = Substitute.For<ISizedStreamableCache<TextureData, GetTextureIntention>>();
            audioClipsCache = Substitute.For<IStreamableCache<AudioClipData, GetAudioClipIntention>>();
            assetBundleCache = Substitute.For<IStreamableCache<AssetBundleData, GetAssetBundleIntention>>();
            gltfContainerAssetsCache = Substitute.For<IGltfContainerAssetsCache>();
            profileCache = Substitute.For<IProfileCache>();
            lodAssetsPool = Substitute.For<ILODCache>();
            roadAssetPool = Substitute.For<IRoadAssetPool>();
            emoteStorage = Substitute.For<IEmoteStorage>();
            jsSourcesCache = Substitute.For<IMemoryCache<string, string>>();

            cacheCleaner = new CacheCleaner(releasablePerformanceBudget, null);

            cacheCleaner.Register(wearableStorage);
            cacheCleaner.Register(texturesCache);
            cacheCleaner.Register(audioClipsCache);
            cacheCleaner.Register(gltfContainerAssetsCache);
            cacheCleaner.Register(assetBundleCache);
            cacheCleaner.Register(attachmentsAssetsCache);
            cacheCleaner.Register(materialPool);
            cacheCleaner.Register(profileCache);
            cacheCleaner.Register(lodAssetsPool);
            cacheCleaner.Register(roadAssetPool);
            cacheCleaner.Register(emoteStorage);
            cacheCleaner.Register(jsSourcesCache);
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
            attachmentsAssetsCache.Received(callsAmount).Unload(releasablePerformanceBudget, Arg.Any<int>());
            wearableStorage.Received(callsAmount).Unload(Arg.Any<IReleasablePerformanceBudget>());
            gltfContainerAssetsCache.Received(callsAmount).Unload(releasablePerformanceBudget, Arg.Any<int>());
            assetBundleCache.Received(callsAmount).Unload(releasablePerformanceBudget, Arg.Any<int>());
            materialPool.Received(callsAmount).ClearThrottled(Arg.Any<int>());
            profileCache.Received(callsAmount).Unload(releasablePerformanceBudget, Arg.Any<int>());
            jsSourcesCache.Received(callsAmount).Unload(releasablePerformanceBudget);
        }
    }
}
