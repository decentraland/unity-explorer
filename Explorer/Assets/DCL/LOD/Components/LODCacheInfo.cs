using System;
using UnityEngine;

namespace DCL.LOD.Components
{
    public class LODCacheInfo : IDisposable
    {
        public readonly LODGroup LodGroup;
        public LODAsset?[] LODAssets { get; private set; }

        // One owned LOD[] reused across SceneLODInfo's GetLODs()/SetLODs() roundtrips instead of allocating per call
        // (Unity's GetLODs has no non-alloc overload). Lazily seeded, kept authoritative because callers only
        // mutate-then-SetLODs this instance; any out-of-band SetLODs must InvalidateReusableLODs() to re-read native state.
        private UnityEngine.LOD[]? reusableLODs;

        public float CullRelativeHeightPercentage;
        public float LODChangeRelativeDistance;

        //We can represent 8 LODS loaded state with a byte
        public byte SuccessfullLODs;
        public byte FailedLODs;

        public LODCacheInfo(LODGroup lodGroup, int lodLevels)
        {
            LodGroup = lodGroup;
            LODAssets = new LODAsset[lodLevels];
            CullRelativeHeightPercentage = 0;
            LODChangeRelativeDistance = 0;
            SuccessfullLODs = 0;
            FailedLODs = 0;
        }

        public void Dispose()
        {
            foreach (var lodAsset in LODAssets)
                lodAsset?.Dispose();

            LODAssets = null!;
        }

        public int LODLoadedCount() =>
            SceneLODInfoUtils.LODCount(SuccessfullLODs) + SceneLODInfoUtils.LODCount(FailedLODs);

        /// <summary>
        ///     The reusable LOD buffer mirroring the LODGroup's current LODs, seeded once from <see cref="LODGroup.GetLODs" />.
        ///     Callers mutate it in place and write it back with <see cref="LODGroup.SetLODs" />.
        /// </summary>
        public UnityEngine.LOD[] RentReusableLODs() =>
            reusableLODs ??= LodGroup.GetLODs();

        /// <summary>Drop the cached buffer after an out-of-band SetLODs so the next <see cref="RentReusableLODs" /> re-reads native state.</summary>
        public void InvalidateReusableLODs() =>
            reusableLODs = null;

        internal UnityEngine.LOD[]? ReusableLODsBufferForTests => reusableLODs;
    }
}
