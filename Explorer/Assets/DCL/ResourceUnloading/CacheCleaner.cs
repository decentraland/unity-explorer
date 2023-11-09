using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.AssetBundles;
using ECS.Unity.GLTFContainer.Asset.Cache;

namespace DCL.CacheCleanUp
{
    public class CacheCleaner
    {
        private GltfContainerAssetsCache gltfContainerAssetsCache;
        private AssetBundleCache assetBundleCache;
        public IWearableAssetsCache WearableAssetsCache { get; private set; }
        public WearableCatalog WearableCatalog { get; private set; }

        public void UnloadCache()
        {
            gltfContainerAssetsCache.Unload();
            WearableAssetsCache.UnloadCachedWearables();

            foreach (IWearable wearable in WearableCatalog.WearableDictionary.Values)
                for (var i = 0; i < wearable.WearableAssets?.Length; i++)
                {
                    WearableAsset wearableAssets = wearable.WearableAssets[i]?.Asset;

                    if (wearableAssets == null)
                        wearable.WearableAssets[i] = null;
                    else if (wearableAssets.ReferenceCount == 0)
                    {
                        wearableAssets.Dispose();
                        wearable.WearableAssets[i] = null;
                    }
                }

            WearableAssetsCache.UnloadCachedWearablesKeys();

            assetBundleCache.Unload();
        }

        public void Register(AssetBundleCache assetBundleCache) =>
            this.assetBundleCache = assetBundleCache;

        public void Register(GltfContainerAssetsCache gltfContainerAssetsCache) =>
            this.gltfContainerAssetsCache = gltfContainerAssetsCache;

        public void Register(IWearableAssetsCache wearableAssetsCache) =>
            WearableAssetsCache = wearableAssetsCache;

        public void Register(WearableCatalog catalog) =>
            WearableCatalog = catalog;
    }
}
