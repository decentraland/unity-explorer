using ECS.StreamableLoading.Cache;
using System.Collections.Generic;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     An eternal runtime cache to prevent parallel loading of the same bundle
    /// </summary>
    public class AssetBundleCache : IStreamableCache<AssetBundleData, GetAssetBundleIntention>
    {
        private readonly Dictionary<GetAssetBundleIntention, AssetBundleData> cache;

        public AssetBundleCache()
        {
            cache = new Dictionary<GetAssetBundleIntention, AssetBundleData>(256, this);
        }

        public bool Equals(GetAssetBundleIntention x, GetAssetBundleIntention y) =>
            x.Hash == y.Hash;

        public int GetHashCode(GetAssetBundleIntention obj) =>
            obj.Hash.GetHashCode();

        public bool TryGet(in GetAssetBundleIntention key, out AssetBundleData asset) =>
            cache.TryGetValue(key, out asset);

        public void Add(in GetAssetBundleIntention key, AssetBundleData asset)
        {
            cache.Add(key, asset);
        }

        public void Dereference(in GetAssetBundleIntention key, AssetBundleData asset) { }
    }
}
