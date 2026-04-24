using DCL.Profiling;
using ECS.StreamableLoading.Cache;
using GLTFast;
using Unity.Profiling;

namespace ECS.StreamableLoading.GLTF
{
    /// <summary>
    ///     Raw GLTF load cache. Inherits the ref-counted pool behaviour from <see cref="RefCountStreamableCacheBase{TAssetData,TAsset,TLoadingIntention}"/>
    ///     so concurrent requests for the same hash deduplicate via OngoingRequests and each consumer gets its own
    ///     reference count bump via <see cref="RefCountStreamableCacheBase{TAssetData,TAsset,TLoadingIntention}.AddReference"/>.
    ///     Used by <see cref="LoadGLTFSystem"/> in place of the previous <c>NoCache&lt;GLTFData, GetGLTFIntention&gt;</c>
    ///     which provided no deduplication at all.
    /// </summary>
    public class GltfLoadCache : RefCountStreamableCacheBase<GLTFData, GltfImport, GetGLTFIntention>
    {
        protected override ref ProfilerCounterValue<int> inCacheCount => ref ProfilingCounters.GltfDataInCache;
    }
}
