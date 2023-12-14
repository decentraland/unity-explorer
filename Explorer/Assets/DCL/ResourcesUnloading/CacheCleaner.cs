using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.Profiling;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Textures;
using ECS.Unity.GLTFContainer.Asset.Components;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.ResourcesUnloading
{
    public class CacheCleaner : ICacheCleaner
    {
        private const int POOLS_UNLOAD_CHUNK = 10;
        private const int WEARABLES_UNLOAD_CHUNK = 10;
        private const int GLTF_UNLOAD_CHUNK = 3;
        private const int AB_UNLOAD_CHUNK = 1;
        private const int TEXTURE_UNLOAD_CHUNK = 1;
        private const int AUDIO_CLIP_UNLOAD_CHUNK = 100;

        private readonly IConcurrentBudgetProvider fpsCapBudgetProvider;
        private readonly List<IThrottledClearable> avatarPools;

        private IStreamableCache<AssetBundleData, GetAssetBundleIntention> assetBundleCache;
        private IStreamableCache<GltfContainerAsset, string> gltfContainerAssetsCache;
        private IStreamableCache<Texture2D, GetTextureIntention> texturesCache;
        private IStreamableCache<AudioClip, GetAudioClipIntention> audioClipsCache;

        private IWearableAssetsCache wearableAssetsCache;
        private IWearableCatalog wearableCatalog;

        public CacheCleaner(IConcurrentBudgetProvider fpsCapBudgetProvider)
        {
            this.fpsCapBudgetProvider = fpsCapBudgetProvider;

            avatarPools = new List<IThrottledClearable> { AvatarCustomSkinningComponent.USED_SLOTS_POOL };
        }

        public void UnloadCache()
        {
            if (!fpsCapBudgetProvider.TrySpendBudget()) return;

            texturesCache.Unload(fpsCapBudgetProvider, TEXTURE_UNLOAD_CHUNK);
            audioClipsCache.Unload(fpsCapBudgetProvider, AUDIO_CLIP_UNLOAD_CHUNK);
            wearableAssetsCache.Unload(fpsCapBudgetProvider, WEARABLES_UNLOAD_CHUNK);
            wearableCatalog.Unload(fpsCapBudgetProvider);
            gltfContainerAssetsCache.Unload(fpsCapBudgetProvider, GLTF_UNLOAD_CHUNK);
            assetBundleCache.Unload(fpsCapBudgetProvider, AB_UNLOAD_CHUNK);

            ClearAvatarsRelatedPools();
        }

        private void ClearAvatarsRelatedPools()
        {
            foreach (IThrottledClearable pool in avatarPools)
                if (fpsCapBudgetProvider.TrySpendBudget())
                    pool.ClearThrottled(POOLS_UNLOAD_CHUNK);
        }

        public void Register(IStreamableCache<AssetBundleData, GetAssetBundleIntention> assetBundleCache) =>
            this.assetBundleCache = assetBundleCache;

        public void Register(IStreamableCache<GltfContainerAsset, string> gltfContainerAssetsCache) =>
            this.gltfContainerAssetsCache = gltfContainerAssetsCache;

        public void Register(IWearableAssetsCache wearableAssetsCache) =>
            this.wearableAssetsCache = wearableAssetsCache;

        public void Register(IStreamableCache<Texture2D, GetTextureIntention> texturesCache) =>
            this.texturesCache = texturesCache;

        public void Register(IStreamableCache<AudioClip, GetAudioClipIntention> audioClipsCache) =>
            this.audioClipsCache = audioClipsCache;

        public void Register(IWearableCatalog catalog) =>
            wearableCatalog = catalog;

        public void Register<T>(IExtendedObjectPool<T> extendedObjectPool) where T: class =>
            avatarPools.Add(extendedObjectPool);

        public void UpdateProfilingCounters()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ProfilingCounters.WearablesAssetsInCatalogAmount.Value = ((WearableCatalog)wearableCatalog).WearableAssetsInCatalog;
            ProfilingCounters.WearablesAssetsInCacheAmount.Value = wearableAssetsCache.WearablesAssesCount;
#endif
        }
    }
}
