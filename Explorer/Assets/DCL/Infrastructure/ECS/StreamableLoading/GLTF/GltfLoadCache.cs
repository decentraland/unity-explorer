using DCL.Profiling;
using ECS.StreamableLoading.Cache;
using GLTFast;
using Unity.Profiling;

namespace ECS.StreamableLoading.GLTF
{
    /// <summary>
    ///     Raw GLTF load cache for <see cref="LoadGLTFSystem"/>. Concurrent requests for the same
    ///     hash deduplicate via <c>OngoingRequests</c>, and each consumer gets its own reference
    ///     count bump via <see cref="RefCountStreamableCacheBase{TAssetData,TAsset,TLoadingIntention}.AddReference"/>.
    /// </summary>
    public class GltfLoadCache : RefCountStreamableCacheBase<GLTFData, GltfImport, GetGLTFIntention>
    {
        protected override ref ProfilerCounterValue<int> inCacheCount => ref ProfilingCounters.GltfDataInCache;
    }
}
