using ECS.StreamableLoading.AssetBundles;
using UnityEngine;

namespace DCL.LOD.Components
{
    public struct LODCacheInfo
    {
        public LODGroup LodGroup;
        public byte LoadedLODs;
        public byte FailedLODs;
        public float CullRelativeHeight;
        public AssetBundleData[] AssetBundleData;
    }
}