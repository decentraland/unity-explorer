using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.Profiling;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Textures;
using ECS.Unity.GLTFContainer.Asset.Cache;
using System.Collections.Generic;
using Unity.Profiling;

namespace DCL.ResourcesUnloading
{
    public class CacheCleaner
    {
        private const int POOLS_UNLOAD_CHUNK = 10;
        private const int WEARABLES_UNLOAD_CHUNK = 10;
        private const int GLTF_UNLOAD_CHUNK = 3;
        private const int AB_UNLOAD_CHUNK = 1;
        private const int TEXTURE_UNLOAD_CHUNK = 1;

        // TODO: remove markers before merge to main
        private static readonly ProfilerMarker texturesCacheMarker = new ("CacheCleanup.texturesCache");
        private static readonly ProfilerMarker assetBundleCacheMarker = new ("CacheCleanup.assetBundleCache");
        private static readonly ProfilerMarker gltfContainerAssetsCacheMarker = new ("CacheCleanup.gltfContainerAssetsCache");
        private static readonly ProfilerMarker wearableCatalogMarker = new ("CacheCleanup.wearableCatalog");
        private static readonly ProfilerMarker wearableAssetsCacheMarker = new ("CacheCleanup.wearableAssetsCache");
        private static readonly ProfilerMarker avatarPoolsMarker = new ("CacheCleanup.avatarPools");

        private readonly IConcurrentBudgetProvider fpsCapBudgetProvider;
        private readonly List<IThrottledClearable> avatarPools;

        private GltfContainerAssetsCache gltfContainerAssetsCache;
        private AssetBundleCache assetBundleCache;
        private IWearableAssetsCache wearableAssetsCache;
        private WearableCatalog wearableCatalog;
        private TexturesCache texturesCache;

        public CacheCleaner(IConcurrentBudgetProvider fpsCapBudgetProvider)
        {
            this.fpsCapBudgetProvider = fpsCapBudgetProvider;

            avatarPools = new List<IThrottledClearable> { AvatarCustomSkinningComponent.USED_SLOTS_POOL };
        }

        public void UnloadCache()
        {
            if (!fpsCapBudgetProvider.TrySpendBudget()) return;

            using (texturesCacheMarker.Auto())
                texturesCache.Unload(fpsCapBudgetProvider, TEXTURE_UNLOAD_CHUNK);

            using (assetBundleCacheMarker.Auto())
                assetBundleCache.Unload(fpsCapBudgetProvider, AB_UNLOAD_CHUNK);

            using (gltfContainerAssetsCacheMarker.Auto())
                gltfContainerAssetsCache.Unload(fpsCapBudgetProvider, GLTF_UNLOAD_CHUNK);

            using (wearableCatalogMarker.Auto())
                wearableCatalog.Unload(fpsCapBudgetProvider);

            using (wearableAssetsCacheMarker.Auto())
                wearableAssetsCache.Unload(fpsCapBudgetProvider, WEARABLES_UNLOAD_CHUNK);

            ClearAvatarsRelatedPools();
        }

        private void ClearAvatarsRelatedPools()
        {
            foreach (IThrottledClearable pool in avatarPools)
                using (avatarPoolsMarker.Auto())
                    if (fpsCapBudgetProvider.TrySpendBudget())
                        pool.ClearThrottled(POOLS_UNLOAD_CHUNK);
        }

        public void Register(AssetBundleCache assetBundleCache) =>
            this.assetBundleCache = assetBundleCache;

        public void Register(GltfContainerAssetsCache gltfContainerAssetsCache) =>
            this.gltfContainerAssetsCache = gltfContainerAssetsCache;

        public void Register(IWearableAssetsCache wearableAssetsCache) =>
            this.wearableAssetsCache = wearableAssetsCache;

        public void Register(WearableCatalog catalog) =>
            wearableCatalog = catalog;

        public void Register(TexturesCache texturesCache) =>
            this.texturesCache = texturesCache;

        public void Register<T>(IExtendedObjectPool<T> extendedObjectPool) where T: class =>
            avatarPools.Add(extendedObjectPool);

        public void UpdateProfilingCounters()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ProfilingCounters.WearablesAssetsInCatalogAmount.Value = wearableCatalog.WearableAssetsInCatalog;
            ProfilingCounters.WearablesAssetsInCacheAmount.Value = wearableAssetsCache.WearablesAssesCount;
#endif
        }
    }
}
