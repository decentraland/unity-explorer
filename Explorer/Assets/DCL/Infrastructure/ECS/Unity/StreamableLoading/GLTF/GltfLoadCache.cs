using DCL.Profiling;
using ECS.StreamableLoading.Cache;
using GLTFast;
using System;
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

        /// <summary>
        ///     Evict every entry whose content hash matches, regardless of the Name it was loaded
        ///     under. <see cref="GetGLTFIntention" /> identity is (Name, Hash) and Name is the verbatim
        ///     src string from scene code, which a caller evicting a changed file cannot reconstruct
        ///     reliably — the hash alone identifies the content.
        /// </summary>
        public void RemoveByHash(string hash)
        {
            if (string.IsNullOrEmpty(hash))
                return;

            for (int i = listedCache.Count - 1; i >= 0; i--)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(listedCache[i].intention.Hash, hash))
                    Remove(listedCache[i].intention);
            }
        }
    }
}
