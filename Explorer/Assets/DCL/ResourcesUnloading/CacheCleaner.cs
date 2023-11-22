using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.PerformanceBudgeting;
using DCL.Profiling;
using ECS.ComponentsPooling;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Textures;
using ECS.Unity.GLTFContainer.Asset.Cache;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.ResourcesUnloading
{
    public class CacheCleaner
    {
        private readonly IConcurrentBudgetProvider fpsCapBudgetProvider;

        private GltfContainerAssetsCache gltfContainerAssetsCache;
        private AssetBundleCache assetBundleCache;
        private IWearableAssetsCache wearableAssetsCache;
        private WearableCatalog wearableCatalog;
        private TexturesCache texturesCache;

        private IObjectPool<Material> materialPool;
        private IObjectPool<ComputeShader> computeShaderPool;
        private IComponentPool<AvatarBase> avatarPoolRegistry;

        public CacheCleaner(IConcurrentBudgetProvider fpsCapBudgetProvider)
        {
            this.fpsCapBudgetProvider = fpsCapBudgetProvider;
        }

        public void UnloadCache()
        {
            if (fpsCapBudgetProvider.TrySpendBudget())
                assetBundleCache.Unload();

            if (fpsCapBudgetProvider.TrySpendBudget())
                gltfContainerAssetsCache.Unload();

            if (fpsCapBudgetProvider.TrySpendBudget())
                texturesCache.Unload();

            if (fpsCapBudgetProvider.TrySpendBudget())
                avatarPoolRegistry.Clear();

            if (fpsCapBudgetProvider.TrySpendBudget())
                computeShaderPool.Clear();

            if (fpsCapBudgetProvider.TrySpendBudget())
                AvatarCustomSkinningComponent.USED_SLOTS_POOL.Clear();

            if (fpsCapBudgetProvider.TrySpendBudget())
                materialPool.Clear();

            if (fpsCapBudgetProvider.TrySpendBudget())
                wearableCatalog.UnloadWearableAssets();

            if (fpsCapBudgetProvider.TrySpendBudget())
                wearableAssetsCache.Unload();
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

        public void Register(IObjectPool<Material> celShadingMaterialPool) =>
            materialPool = celShadingMaterialPool;

        public void Register(IObjectPool<ComputeShader> computeShaderPool) =>
            this.computeShaderPool = computeShaderPool;

        public void UpdateProfilingCounters()
        {
            ProfilingCounters.WearablesAssetsInCatalogAmount.Value = wearableCatalog.WearableAssetsInCatalog;
            ProfilingCounters.WearablesAssetsInCacheAmount.Value = wearableAssetsCache.Cache.Keys.Count;
        }
    }
}
