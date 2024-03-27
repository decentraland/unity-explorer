using Arch.Core;
using DCL.LOD;
using DCL.Roads.Settings;
using UnityEngine;

namespace DCL.Roads.Components
{
    public struct RoadInfo
    {
        public bool IsDirty;
        public string CurrentKey;
        public Transform CurrentAsset;

        public static RoadInfo Create() =>
            new ()
            {
                IsDirty = true,
            };

        public void Dispose(IRoadAssetPool roadAssetPool)
        {
            if (!string.IsNullOrEmpty(CurrentKey))
                roadAssetPool.Release(CurrentKey, CurrentAsset);
        }
    }
}
