using ECS.StreamableLoading.AssetBundles;
using ECS.Unity.GLTFContainer.Asset.Cache;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Global
{
    public class CacheCleaner
    {
        public const float CACHE_EXPIRATION_TIME = 150; // * 60; // minutes in seconds
        public const float CACHE_MINIMAL_HOLD_TIME = 10f; // * 60; // minutes in seconds

        private const int UNLOADING_BUDGET = 50;

        private readonly Dictionary<Type, int> estimationMap;

        private AssetBundleCache assetBundleCache;
        private GltfContainerAssetsCache gltfContainerAssetsCache;

        public CacheCleaner(Dictionary<Type, int> memoryEstimationMap)
        {
            estimationMap = memoryEstimationMap;
        }

        public void UnloadUnusedCache(float memoryOverusage)
        {
            (Type type, int amount) unloaded = assetBundleCache.UnloadUnusedCache(UNLOADING_BUDGET);

            Debug.Log($"VV AB:: cleared {unloaded.amount}");

            if (unloaded.amount < UNLOADING_BUDGET && estimationMap.TryGetValue(unloaded.type, out int size) && size * unloaded.amount < memoryOverusage)
            {
                // gltfContainerAssetsCache.UnloadUnusedCache(UNLOADING_BUDGET - unloaded.amount);
            }
        }

        public void UnloadAllCache()
        {
            assetBundleCache.UnloadAllCache(UNLOADING_BUDGET);
            gltfContainerAssetsCache.UnloadAllCache();
        }

        public void Register(AssetBundleCache assetBundleCache) =>
            this.assetBundleCache = assetBundleCache;

        public void Register(GltfContainerAssetsCache gltfContainerAssetsCache) =>
            this.gltfContainerAssetsCache = gltfContainerAssetsCache;
    }
}
