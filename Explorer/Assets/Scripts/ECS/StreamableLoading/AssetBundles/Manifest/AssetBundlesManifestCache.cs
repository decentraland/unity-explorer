using ECS.StreamableLoading.Cache;
using SceneRunner.Scene;
using System.Collections.Generic;

namespace ECS.StreamableLoading.AssetBundles.Manifest
{
    /// <summary>
    ///     Keeps SceneAbDto forever
    /// </summary>
    public class AssetBundlesManifestCache : IStreamableCache<SceneAssetBundleManifest, GetAssetBundleManifestIntention>
    {
        private readonly Dictionary<GetAssetBundleManifestIntention, SceneAssetBundleManifest> cache;

        public AssetBundlesManifestCache()
        {
            cache = new Dictionary<GetAssetBundleManifestIntention, SceneAssetBundleManifest>(256, this);
        }

        public bool TryGet(in GetAssetBundleManifestIntention key, out SceneAssetBundleManifest asset) =>
            cache.TryGetValue(key, out asset);

        public void Add(in GetAssetBundleManifestIntention key, SceneAssetBundleManifest asset)
        {
            cache.Add(key, asset);
        }

        public void Dereference(in GetAssetBundleManifestIntention key, SceneAssetBundleManifest asset)
        {
            // Eternal cache - no action required
        }

        public bool Equals(GetAssetBundleManifestIntention x, GetAssetBundleManifestIntention y) =>
            URLComparer<GetAssetBundleManifestIntention>.INSTANCE.Equals(x, y);

        public int GetHashCode(GetAssetBundleManifestIntention obj) =>
            URLComparer<GetAssetBundleManifestIntention>.INSTANCE.GetHashCode(obj);
    }
}
