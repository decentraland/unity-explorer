using ECS.StreamableLoading.AssetBundles;
using ECS.Unity.GLTFContainer.Asset.Cache;

namespace Global
{
    public class CacheCleaner
    {
        public const float CACHE_EXPIRATION_TIME = 5 * 60; // [min] * [sec]
        public const float CACHE_MINIMAL_HOLD_TIME = 0.5f * 60; // [min] * [sec]

        private const int UNLOADING_BUDGET = 10;

        private AssetBundleCache assetBundleCache;
        private GltfContainerAssetsCache gltfContainerAssetsCache;

        public void UnloadUnusedCache()
        {
            int unloadedAmount = assetBundleCache.UnloadUnusedCache(UNLOADING_BUDGET);

            // if (unloadedAmount < UNLOADING_BUDGET)
            //     unloadedAmount += gltfContainerAssetsCache.UnloadUnusedCache(UNLOADING_BUDGET - unloadedAmount);
        }

        public void UnloadAllCache()
        {
            int unloadedAmount = assetBundleCache.UnloadAllCache(UNLOADING_BUDGET);

            // if (unloadedAmount < UNLOADING_BUDGET)
            //     unloadedAmount += gltfContainerAssetsCache.UnloadAllCache(UNLOADING_BUDGET - unloadedAmount);
        }

        public void Register(AssetBundleCache assetBundleCache) =>
            this.assetBundleCache = assetBundleCache;

        public void Register(GltfContainerAssetsCache gltfContainerAssetsCache) =>
            this.gltfContainerAssetsCache = gltfContainerAssetsCache;
    }
}
