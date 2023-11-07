using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.AssetBundles;
using ECS.Unity.GLTFContainer.Asset.Cache;

namespace DCL.CacheCleanUp
{
    public class CacheCleaner
    {
        private GltfContainerAssetsCache gltfContainerAssetsCache;
        private AssetBundleCache assetBundleCache;
        private IWearableAssetsCache wearableAssetsCache;
        private WearableCatalog catalog;

        public void Register(AssetBundleCache assetBundleCache) =>
            this.assetBundleCache = assetBundleCache;

        public void Register(GltfContainerAssetsCache gltfContainerAssetsCache) =>
            this.gltfContainerAssetsCache = gltfContainerAssetsCache;

        public void Register(IWearableAssetsCache wearableAssetsCache) =>
            this.wearableAssetsCache = wearableAssetsCache;

        public void Register(WearableCatalog catalog) =>
            this.catalog = catalog;
    }
}
