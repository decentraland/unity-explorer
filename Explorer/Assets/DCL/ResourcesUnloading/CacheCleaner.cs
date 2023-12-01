using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.PerformanceAndDiagnostics.Optimization.PerformanceBudgeting;
using DCL.PerformanceAndDiagnostics.Optimization.Pools;
using DCL.PerformanceAndDiagnostics.Profiling;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Textures;
using ECS.Unity.GLTFContainer.Asset.Cache;
using Unity.Profiling;
using UnityEngine;

namespace DCL.ResourcesUnloading
{
    public class CacheCleaner
    {
        // TODO: remove markers before merge to main
        private static readonly ProfilerMarker texturesCacheMarker = new ("CacheCleanup.texturesCache");
        private static readonly ProfilerMarker assetBundleCacheMarker = new ("CacheCleanup.assetBundleCache");
        private static readonly ProfilerMarker gltfContainerAssetsCacheMarker = new ("CacheCleanup.gltfContainerAssetsCache");
        private static readonly ProfilerMarker wearableCatalogMarker = new ("CacheCleanup.wearableCatalog");
        private static readonly ProfilerMarker wearableAssetsCacheMarker = new ("CacheCleanup.wearableAssetsCache");

        private static readonly ProfilerMarker avatarPoolRegistryMarker = new ("CacheCleanup.avatarPoolRegistry");
        private static readonly ProfilerMarker computeShaderPoolMarker = new ("CacheCleanup.computeShaderPool");
        private static readonly ProfilerMarker USED_SLOTS_POOLCacheMarker = new ("CacheCleanup.USED_SLOTS_POOL");
        private static readonly ProfilerMarker materialPoolCacheMarker = new ("CacheCleanup.materialPool");

        private readonly IConcurrentBudgetProvider fpsCapBudgetProvider;
        private readonly IProfilingProvider profilingProvider;
        private readonly IThrottledClearable avatarMaterialSetupPool;

        private GltfContainerAssetsCache gltfContainerAssetsCache;
        private AssetBundleCache assetBundleCache;
        private IWearableAssetsCache wearableAssetsCache;
        private WearableCatalog wearableCatalog;
        private TexturesCache texturesCache;

        private IThrottledClearable materialPool;
        private IThrottledClearable computeShaderPool;
        private IThrottledClearable avatarPoolRegistry;

        public CacheCleaner(IConcurrentBudgetProvider fpsCapBudgetProvider, IProfilingProvider profilingProvider)
        {
            this.fpsCapBudgetProvider = fpsCapBudgetProvider;
            this.profilingProvider = profilingProvider;

            avatarMaterialSetupPool = AvatarCustomSkinningComponent.USED_SLOTS_POOL;
        }

        public void UnloadCache()
        {
            if (!fpsCapBudgetProvider.TrySpendBudget()) return;

            // Debug.Log(profilingProvider.CurrentFrameTimeValueInNS);

            using (texturesCacheMarker.Auto())
                texturesCache.Unload(fpsCapBudgetProvider, 1);

            using (assetBundleCacheMarker.Auto())
                assetBundleCache.Unload(fpsCapBudgetProvider, 1);

            using (gltfContainerAssetsCacheMarker.Auto())
                gltfContainerAssetsCache.Unload(fpsCapBudgetProvider, 3);

            using (wearableCatalogMarker.Auto())
                wearableCatalog.Unload(fpsCapBudgetProvider);

            using (wearableAssetsCacheMarker.Auto())
                wearableAssetsCache.Unload(fpsCapBudgetProvider, 10);

            ClearAvatarsRelatedPools();
        }

        private void ClearAvatarsRelatedPools()
        {
            if (fpsCapBudgetProvider.TrySpendBudget())
                using (avatarPoolRegistryMarker.Auto())
                    avatarPoolRegistry.ClearThrottled(10);

            if (fpsCapBudgetProvider.TrySpendBudget())
                using (computeShaderPoolMarker.Auto())
                    computeShaderPool.ClearThrottled(10);

            if (fpsCapBudgetProvider.TrySpendBudget())
                using (USED_SLOTS_POOLCacheMarker.Auto())
                    avatarMaterialSetupPool.ClearThrottled(10);

            if (fpsCapBudgetProvider.TrySpendBudget())
                using (materialPoolCacheMarker.Auto())
                    materialPool.ClearThrottled(10);
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

        public void Register(IComponentPool<AvatarBase> avatarPoolRegistry) =>
            this.avatarPoolRegistry = avatarPoolRegistry;

        public void Register(IExtendedObjectPool<Material> celShadingMaterialPool) =>
            materialPool = celShadingMaterialPool;

        public void Register(IExtendedObjectPool<ComputeShader> computeShaderPool) =>
            this.computeShaderPool = computeShaderPool;

        public void UpdateProfilingCounters()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ProfilingCounters.WearablesAssetsInCatalogAmount.Value = wearableCatalog.WearableAssetsInCatalog;
            ProfilingCounters.WearablesAssetsInCacheAmount.Value = wearableAssetsCache.Cache.Keys.Count;
#endif
        }
    }
}
