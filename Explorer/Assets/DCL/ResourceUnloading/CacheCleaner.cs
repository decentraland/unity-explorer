using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Profiling;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Textures;
using ECS.Unity.GLTFContainer.Asset.Cache;

namespace DCL.ResourceUnloading
{
    public class CacheCleaner
    {
        private GltfContainerAssetsCache gltfContainerAssetsCache;
        private AssetBundleCache assetBundleCache;
        private IWearableAssetsCache wearableAssetsCache;
        private WearableCatalog wearableCatalog;
        private TexturesCache texturesCache;

        public void UnloadCache()
        {
            gltfContainerAssetsCache.Unload();
            wearableAssetsCache.Unload();
            wearableCatalog.UnloadWearableAssets();
            texturesCache.Unload();
            assetBundleCache.Unload();
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

        public void UpdateProfilingCounters()
        {
            ProfilingCounters.WearablesAssetsInCatalogAmount.Value = wearableCatalog.WearableAssetsInCatalog;
            ProfilingCounters.WearablesAssetsInCacheAmount.Value = wearableAssetsCache.Cache.Keys.Count;
        }
    }
}
