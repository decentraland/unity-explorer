using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Cache;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Components
{
    /// <summary>
    ///     An eternal runtime cache to prevent parallel loading of the same bundle
    /// </summary>
    public class WearableAssetBundleCache : OngoingRequestsCacheBase<AssetBundleData>, IStreamableCache<AssetBundleData, GetWearableAssetBundleIntention>
    {
        private readonly Dictionary<GetWearableAssetBundleIntention, AssetBundleData> cache;

        public WearableAssetBundleCache()
        {
            cache = new Dictionary<GetWearableAssetBundleIntention, AssetBundleData>(256, this);
        }

        public bool Equals(GetWearableAssetBundleIntention x, GetWearableAssetBundleIntention y) =>
            x.Hash == y.Hash;

        public int GetHashCode(GetWearableAssetBundleIntention obj) =>
            obj.Hash.GetHashCode();

        public bool TryGet(in GetWearableAssetBundleIntention key, out AssetBundleData asset) =>
            cache.TryGetValue(key, out asset);

        public void Add(in GetWearableAssetBundleIntention key, AssetBundleData asset)
        {
            cache.Add(key, asset);
        }

        public void Dereference(in GetWearableAssetBundleIntention key, AssetBundleData asset) { }
    }
}
