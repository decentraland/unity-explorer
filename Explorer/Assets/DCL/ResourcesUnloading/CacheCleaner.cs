using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.LOD;
using DCL.Optimization;
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
using System;
using System.Collections.Generic;

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

        private readonly DebugWidgetBuilder? widgetBuilder;
        private readonly List<Action> updateCallbacks = new ();

        private readonly List<IThrottledClearable> extendedObjectPools;

        private IStreamableCache<AssetBundleData, GetAssetBundleIntention>? assetBundleCache;
        private IGltfContainerAssetsCache? gltfContainerAssetsCache;
        private IStreamableCache<Texture2DData, GetTextureIntention>? texturesCache;
        private ILODCache? lodCache;
        private IStreamableCache<AudioClipData, GetAudioClipIntention>? audioClipsCache;
        private IStreamableCache<Texture2DData, GetNFTShapeIntention>? nftShapeCache;

        private IAttachmentsAssetsCache? wearableAssetsCache;
        private IWearableStorage? wearableStorage;
        private IProfileCache? profileCache;
        private IStreamableCache<ProfileData, GetProfileIntention>? profileIntentionCache;
        private IRoadAssetPool? roadCache;

        private IEmoteStorage? emoteCache;
        private IJsSourcesCache? jsSourcesCache;

        private readonly IPerformanceBudget unlimitedFPSBudget;

        public CacheCleaner(IPerformanceBudget fpsCapBudget, DebugWidgetBuilder? widgetBuilder)
        {
            this.fpsCapBudget = fpsCapBudget;
            this.widgetBuilder = widgetBuilder;
            unlimitedFPSBudget = new NullPerformanceBudget();
            extendedObjectPools = new List<IThrottledClearable> { AvatarCustomSkinningComponent.USED_SLOTS_POOL };

            widgetBuilder?.AddSingleButton("Update", () =>
            {
                foreach (var callback in updateCallbacks)
                    callback();
            });
        }

        public void UnloadCache(bool budgeted = true)
        {
            if (budgeted)
                if (!fpsCapBudget.TrySpendBudget())
                    return;

            var budgetToUse = budgeted ? fpsCapBudget : unlimitedFPSBudget;

            nftShapeCache!.Unload(budgetToUse, budgeted ? NFT_SHAPE_UNLOAD_CHUNK : int.MaxValue);
            texturesCache!.Unload(budgetToUse, budgeted ? TEXTURE_UNLOAD_CHUNK : int.MaxValue);
            audioClipsCache!.Unload(budgetToUse, budgeted ? AUDIO_CLIP_UNLOAD_CHUNK : int.MaxValue);
            wearableAssetsCache!.Unload(budgetToUse, budgeted ? WEARABLES_UNLOAD_CHUNK : int.MaxValue);
            wearableStorage!.Unload(budgetToUse);
            emoteCache!.Unload(budgetToUse);
            gltfContainerAssetsCache!.Unload(budgetToUse, budgeted ? GLTF_UNLOAD_CHUNK : int.MaxValue);
            lodCache!.Unload(budgetToUse, budgeted ? GLTF_UNLOAD_CHUNK : int.MaxValue);
            assetBundleCache!.Unload(budgetToUse, budgeted ? AB_UNLOAD_CHUNK : int.MaxValue);
            profileCache!.Unload(budgetToUse, budgeted ? PROFILE_UNLOAD_CHUNK : int.MaxValue);
            profileIntentionCache!.Unload(budgetToUse, budgeted ? PROFILE_UNLOAD_CHUNK : int.MaxValue);
            roadCache!.Unload(budgetToUse, budgeted ? GLTF_UNLOAD_CHUNK : int.MaxValue);
            jsSourcesCache!.Unload(budgetToUse);

            ClearExtendedObjectPools(budgetToUse, budgeted ? POOLS_UNLOAD_CHUNK : int.MaxValue);
        }

        private void ClearExtendedObjectPools(IPerformanceBudget budgetToUse, int maxUnload)
        {
            foreach (IThrottledClearable pool in extendedObjectPools)
                if (budgetToUse.TrySpendBudget())
                    pool.ClearThrottled(maxUnload);
        }

        public void Register(ILODCache lodAssetsPool)
        {
            lodCache = lodAssetsPool;
        }

        public void Register(IRoadAssetPool roadAssetPool) =>
            roadCache = roadAssetPool;

        public void Register(IStreamableCache<AssetBundleData, GetAssetBundleIntention> assetBundleCache) =>
            this.assetBundleCache = assetBundleCache;

        public void Register(IGltfContainerAssetsCache gltfContainerAssetsCache) =>
            this.gltfContainerAssetsCache = gltfContainerAssetsCache;

        public void Register(IAttachmentsAssetsCache wearableAssetsCache) =>
            this.wearableAssetsCache = wearableAssetsCache;

        public void Register(ISizedStreamableCache<Texture2DData, GetTextureIntention> texturesCache)
        {
            this.texturesCache = texturesCache;
            TryAppendToDebug(texturesCache, "Textures");
        }

        public void Register(ISizedStreamableCache<Texture2DData, GetNFTShapeIntention> nftShapeCache)
        {
            this.nftShapeCache = nftShapeCache;
            TryAppendToDebug(nftShapeCache, "NFT Shapes");
        }

        public void Register(IStreamableCache<AudioClipData, GetAudioClipIntention> audioClipsCache) =>
            this.audioClipsCache = audioClipsCache;

        public void Register(IWearableStorage storage) =>
            wearableStorage = storage;

        public void Register<T>(IExtendedObjectPool<T> extendedObjectPool) where T: class =>
            extendedObjectPools.Add(extendedObjectPool);

        public void Register(IProfileCache profileCache) =>
            this.profileCache = profileCache;

        public void Register(IStreamableCache<ProfileData, GetProfileIntention> profileIntentionCache) =>
            this.profileIntentionCache = profileIntentionCache;

        public void Register(IEmoteStorage emoteStorage) =>
            this.emoteCache = emoteStorage;

        public void Register(IJsSourcesCache jsSourcesCache) =>
            this.jsSourcesCache = jsSourcesCache;

        public void UpdateProfilingCounters()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ProfilingCounters.WearablesAssetsInCatalogAmount.Value = ((WearableStorage)wearableStorage).WearableAssetsInCatalog;
            ProfilingCounters.WearablesAssetsInCacheAmount.Value = wearableAssetsCache.AssetsCount;
#endif
        }

        private void TryAppendToDebug<TA, TI>(ISizedStreamableCache<TA, TI> cache, string title)
        {
            if (widgetBuilder == null)
                return;

            ElementBinding<string> totalSize = new (string.Empty);
            ElementBinding<string> totalCount = new (string.Empty);

            widgetBuilder
              ?.AddControl(new DebugConstLabelDef(title), null)
               .AddCustomMarker("Total size", totalSize)
               .AddCustomMarker("Total count", totalCount);

            updateCallbacks.Add(() =>
            {
                totalSize.SetAndUpdate(cache.ToReadableString());
                totalCount.SetAndUpdate(cache.ItemCount.ToString());
            });
        }
    }
}
