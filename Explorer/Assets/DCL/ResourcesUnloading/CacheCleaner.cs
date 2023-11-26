using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.PerformanceAndDiagnostics.Optimization.PerformanceBudgeting;
using DCL.PerformanceAndDiagnostics.Optimization.Pools;
using DCL.Profiling;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Textures;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using UnityEngine;

namespace DCL.ResourcesUnloading
{
    public class CacheCleaner
    {
        private const int UNLOAD_CHUNK_SIZE = 10;
        private readonly IConcurrentBudgetProvider fpsCapBudgetProvider;

        private GltfContainerAssetsCache gltfContainerAssetsCache;
        private AssetBundleCache assetBundleCache;
        private IWearableAssetsCache wearableAssetsCache;
        private WearableCatalog wearableCatalog;
        private TexturesCache texturesCache;

        private IObjectPoolDCL<Material> materialPool;
        private IObjectPoolDCL<ComputeShader> computeShaderPool;
        private IComponentPoolDCL<AvatarBase> avatarPoolRegistry;

        public CacheCleaner(IConcurrentBudgetProvider fpsCapBudgetProvider)
        {
            this.fpsCapBudgetProvider = fpsCapBudgetProvider;
        }

        public void UnloadCache()
        {
            if (!fpsCapBudgetProvider.TrySpendBudget()) return;

            texturesCache.Unload(fpsCapBudgetProvider);

            assetBundleCache.Unload(fpsCapBudgetProvider);

            gltfContainerAssetsCache.Unload(fpsCapBudgetProvider);
            GltfContainerAsset.COLLIDERS_POOL.Clear(UNLOAD_CHUNK_SIZE);
            GltfContainerAsset.RENDERERS_POOL.Clear(UNLOAD_CHUNK_SIZE);
            GltfContainerAsset.MESH_FILTERS_POOL.Clear(UNLOAD_CHUNK_SIZE);

            wearableCatalog.Unload(fpsCapBudgetProvider);
            wearableAssetsCache.Unload(fpsCapBudgetProvider);

            avatarPoolRegistry.Clear(UNLOAD_CHUNK_SIZE);
            computeShaderPool.Clear(UNLOAD_CHUNK_SIZE);
            AvatarCustomSkinningComponent.USED_SLOTS_POOL.Clear(UNLOAD_CHUNK_SIZE);
            materialPool.Clear(UNLOAD_CHUNK_SIZE);
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

        public void Register(IComponentPoolDCL<AvatarBase> avatarPoolRegistry) =>
            this.avatarPoolRegistry = avatarPoolRegistry;

        public void Register(IObjectPoolDCL<Material> celShadingMaterialPool) =>
            materialPool = celShadingMaterialPool;

        public void Register(IObjectPoolDCL<ComputeShader> computeShaderPool) =>
            this.computeShaderPool = computeShaderPool;

        public void UpdateProfilingCounters()
        {
            ProfilingCounters.WearablesAssetsInCatalogAmount.Value = wearableCatalog.WearableAssetsInCatalog;
            ProfilingCounters.WearablesAssetsInCacheAmount.Value = wearableAssetsCache.Cache.Keys.Count;
        }
    }
}
