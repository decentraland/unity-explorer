using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.LOD;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.Profiles;
using DCL.Profiling;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.NFTShapes;
using ECS.StreamableLoading.Textures;
using ECS.Unity.GLTFContainer.Asset.Cache;
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
        private const int NFT_SHAPE_UNLOAD_CHUNK = 1;
        private const int AUDIO_CLIP_UNLOAD_CHUNK = 100;
        private const int PROFILE_UNLOAD_CHUNK = 10;

        private readonly IPerformanceBudget fpsCapBudget;
        private readonly List<IThrottledClearable> avatarPools;

        private IStreamableCache<AssetBundleData, GetAssetBundleIntention> assetBundleCache;
        private IGltfContainerAssetsCache gltfContainerAssetsCache;
        private IStreamableCache<Texture2D, GetTextureIntention> texturesCache;
        private ILODAssetsPool lodCache;
        private IStreamableCache<AudioClip, GetAudioClipIntention> audioClipsCache;
        private IStreamableCache<Texture2D, GetNFTShapeIntention> nftShapeCache = new IStreamableCache<Texture2D, GetNFTShapeIntention>.Fake();

        private IWearableAssetsCache wearableAssetsCache;
        private IWearableCatalog wearableCatalog;
        private IProfileCache? profileCache;
        private IStreamableCache<Profile, GetProfileIntention>? profileIntentionCache;
        private IRoadAssetPool roadCache;

        private IEmoteCache? emoteCache;

        public CacheCleaner(IPerformanceBudget fpsCapBudget)
        {
            this.fpsCapBudget = fpsCapBudget;

            avatarPools = new List<IThrottledClearable> { AvatarCustomSkinningComponent.USED_SLOTS_POOL };
        }

        public void UnloadCache()
        {
            if (!fpsCapBudget.TrySpendBudget()) return;

            nftShapeCache.Unload(fpsCapBudget, NFT_SHAPE_UNLOAD_CHUNK);
            texturesCache.Unload(fpsCapBudget, TEXTURE_UNLOAD_CHUNK);
            audioClipsCache.Unload(fpsCapBudget, AUDIO_CLIP_UNLOAD_CHUNK);
            wearableAssetsCache.Unload(fpsCapBudget, WEARABLES_UNLOAD_CHUNK);
            wearableCatalog.Unload(fpsCapBudget);
            emoteCache?.Unload(fpsCapBudget);
            gltfContainerAssetsCache.Unload(fpsCapBudget, GLTF_UNLOAD_CHUNK);
            assetBundleCache.Unload(fpsCapBudget, AB_UNLOAD_CHUNK);
            profileCache?.Unload(fpsCapBudget, PROFILE_UNLOAD_CHUNK);
            profileIntentionCache?.Unload(fpsCapBudget, PROFILE_UNLOAD_CHUNK);
            lodCache.Unload(fpsCapBudget, GLTF_UNLOAD_CHUNK);
            roadCache.Unload(fpsCapBudget, GLTF_UNLOAD_CHUNK);

            ClearAvatarsRelatedPools();
        }

        private void ClearAvatarsRelatedPools()
        {
            foreach (IThrottledClearable pool in avatarPools)
                if (fpsCapBudget.TrySpendBudget())
                    pool.ClearThrottled(POOLS_UNLOAD_CHUNK);
        }

        public void Register(ILODAssetsPool lodAssetsPool) =>
            lodCache = lodAssetsPool;

        public void Register(IRoadAssetPool roadAssetPool) =>
            roadCache = roadAssetPool;

        public void Register(IStreamableCache<AssetBundleData, GetAssetBundleIntention> assetBundleCache) =>
            this.assetBundleCache = assetBundleCache;

        public void Register(IGltfContainerAssetsCache gltfContainerAssetsCache) =>
            this.gltfContainerAssetsCache = gltfContainerAssetsCache;

        public void Register(IWearableAssetsCache wearableAssetsCache) =>
            this.wearableAssetsCache = wearableAssetsCache;

        public void Register(IStreamableCache<Texture2D, GetTextureIntention> texturesCache) =>
            this.texturesCache = texturesCache;

        public void Register(IStreamableCache<Texture2D, GetNFTShapeIntention> nftShapeCache) =>
            this.nftShapeCache = nftShapeCache;

        public void Register(IStreamableCache<AudioClip, GetAudioClipIntention> audioClipsCache) =>
            this.audioClipsCache = audioClipsCache;

        public void Register(IWearableCatalog catalog) =>
            wearableCatalog = catalog;

        public void Register<T>(IExtendedObjectPool<T> extendedObjectPool) where T: class =>
            avatarPools.Add(extendedObjectPool);

        public void Register(IProfileCache profileCache) =>
            this.profileCache = profileCache;

        public void Register(IStreamableCache<Profile, GetProfileIntention> profileIntentionCache) =>
            this.profileIntentionCache = profileIntentionCache;

        public void Register(IEmoteCache emoteCache) =>
            this.emoteCache = emoteCache;

        public void UpdateProfilingCounters()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ProfilingCounters.WearablesAssetsInCatalogAmount.Value = ((WearableCatalog)wearableCatalog).WearableAssetsInCatalog;
            ProfilingCounters.WearablesAssetsInCacheAmount.Value = wearableAssetsCache.WearablesAssesCount;
#endif
        }
    }
}
