using ECS.StreamableLoading.AssetBundles;
using ECS.Unity.GLTFContainer.Asset.Cache;

namespace Global
{
    public class CacheCleaner
    {
        private AssetBundleCache assetBundleCache;
        private GltfContainerAssetsCache gltfContainerAssetsCache;

        public void UnloadCache()
        {
            assetBundleCache.UnloadCache();
            gltfContainerAssetsCache.UnloadCache();
        }

        public void Register(AssetBundleCache assetBundleCache)
        {
            this.assetBundleCache = assetBundleCache;
        }

        public void Register(GltfContainerAssetsCache gltfContainerAssetsCache)
        {
            this.gltfContainerAssetsCache = gltfContainerAssetsCache;
        }
    }
}
