using DCL.Profiling;
using ECS.StreamableLoading.Cache;
using System;
using Unity.Profiling;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     An eternal runtime cache to prevent parallel loading of the same bundle
    /// </summary>
    public class AssetBundleCache : RefCountStreamableCacheBase<AssetBundleData, AssetBundle, GetAssetBundleIntention>, IStreamableCache<AssetBundleData, GetAssetBundleIntention>
    {
        protected override ref ProfilerCounterValue<int> inCacheCount => ref ProfilingCounters.AssetBundlesInCache;

        public override bool Equals(GetAssetBundleIntention x, GetAssetBundleIntention y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Hash, y.Hash);

        public override int GetHashCode(GetAssetBundleIntention obj) =>
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Hash);
    }
}
