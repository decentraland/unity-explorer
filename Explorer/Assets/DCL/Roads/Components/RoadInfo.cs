using DCL.LOD;
using UnityEngine;

namespace DCL.Roads.Components
{
    public struct RoadInfo
    {
        public string CurrentKey;
        public Transform CurrentAsset;

        public static RoadInfo Create() =>
            new ();

        public void Dispose(IRoadAssetPool roadAssetPool)
        {
            if (!string.IsNullOrEmpty(CurrentKey) && CurrentAsset != null)
                roadAssetPool.Release(CurrentKey, CurrentAsset);
        }
    }
}
