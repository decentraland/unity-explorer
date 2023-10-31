using ECS.StreamableLoading.AssetBundles;
using ECS.Unity.GLTFContainer.Asset.Cache;

namespace Global
{
    public class CacheCleaner
    {
        public const float CACHE_EXPIRATION_TIME = 15; // [sec]

        private const int UNLOADING_BUDGET = 10;

        private AssetBundleCache assetBundleCache;
        private GltfContainerAssetsCache gltfContainerAssetsCache;

        public void UnloadCache()
        {
            int unloadedAmount = assetBundleCache.UnloadUnusedCache(UNLOADING_BUDGET);

            // if (unloadedAmount < UNLOADING_BUDGET)
            //     unloadedAmount += gltfContainerAssetsCache.UnloadUnusedCache(UNLOADING_BUDGET - unloadedAmount);
        }

        public void Register(AssetBundleCache assetBundleCache) =>
            this.assetBundleCache = assetBundleCache;

        public void Register(GltfContainerAssetsCache gltfContainerAssetsCache) =>
            this.gltfContainerAssetsCache = gltfContainerAssetsCache;
    }
}
