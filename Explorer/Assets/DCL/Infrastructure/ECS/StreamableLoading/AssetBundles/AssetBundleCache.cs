using DCL.Profiling;
using ECS.StreamableLoading.Cache;
using Unity.Profiling;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     An eternal runtime cache to prevent parallel loading of the same bundle
    /// </summary>
    public class AssetBundleCache : RefCountStreamableCacheBase<AssetBundleData, AssetBundle, GetAssetBundleIntention>
    {
        protected override ref ProfilerCounterValue<int> inCacheCount => ref ProfilingCounters.AssetBundlesInCache;
    }
}
