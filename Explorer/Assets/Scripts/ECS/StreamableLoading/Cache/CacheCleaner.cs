using ECS.StreamableLoading.AssetBundles;
using ECS.Unity.GLTFContainer.Asset.Cache;
using System;

namespace Global
{
    public class CacheCleaner
    {
        private AssetBundleCache assetBundleCache;
        private GltfContainerAssetsCache gltfContainerAssetsCache;

        public void UnloadUnusedCache(int requestedMemoryCounter)
        {
            (Type, int) unloaded = assetBundleCache.UnloadUnusedCache();
        }

        public void UnloadAllCache()
        {
            assetBundleCache.UnloadAllCache();

            // gltfContainerAssetsCache.UnloadAllCache();
        }

        public void Register(AssetBundleCache assetBundleCache) =>
            this.assetBundleCache = assetBundleCache;

        public void Register(GltfContainerAssetsCache gltfContainerAssetsCache) =>
            this.gltfContainerAssetsCache = gltfContainerAssetsCache;
    }
}
