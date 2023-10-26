using ECS.StreamableLoading.AssetBundles;
using ECS.Unity.GLTFContainer.Asset.Cache;
using UnityEngine;

namespace Global
{
    public class CacheCleaner
    {
        public const float CACHE_EXPIRATION_TIME = 2 * 60; // minutes in seconds
        public const float CACHE_MINIMAL_HOLD_TIME = 10f; // * 60; // minutes in seconds

        private const int UNLOADING_BUDGET = 3;

        private AssetBundleCache assetBundleCache;
        private GltfContainerAssetsCache gltfContainerAssetsCache;

        public void UnloadUnusedCache()
        {
            int unloadedAmount = assetBundleCache.UnloadUnusedCache(UNLOADING_BUDGET);

            if (unloadedAmount < UNLOADING_BUDGET)
                unloadedAmount += gltfContainerAssetsCache.UnloadUnusedCache(UNLOADING_BUDGET - unloadedAmount);

            Debug.Log($"VV AB:: cleared {unloadedAmount}");
        }

        public void UnloadAllCache()
        {
            int unloadedAmount = assetBundleCache.UnloadAllCache(UNLOADING_BUDGET);

            if (unloadedAmount < UNLOADING_BUDGET)
                unloadedAmount += gltfContainerAssetsCache.UnloadAllCache(UNLOADING_BUDGET - unloadedAmount);
        }

        public void Register(AssetBundleCache assetBundleCache) =>
            this.assetBundleCache = assetBundleCache;

        public void Register(GltfContainerAssetsCache gltfContainerAssetsCache) =>
            this.gltfContainerAssetsCache = gltfContainerAssetsCache;
    }
}
