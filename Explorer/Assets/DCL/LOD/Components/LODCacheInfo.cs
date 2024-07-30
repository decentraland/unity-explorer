using System;
using ECS.StreamableLoading.AssetBundles;
using Segment.Concurrent;
using UnityEngine;
using UnityEngine.Serialization;
using Utility;

namespace DCL.LOD.Components
{
    public struct LODCacheInfo : IDisposable
    {
        public LODGroup LodGroup;

        //We can represent 8 LODS loaded state with a byte
        public byte SuccessfullLODs;
        public byte FailedLODs;
        public float CullRelativeHeight;
        public LODAsset[] LODAssets;

        public void Dispose()
        {
            foreach (var lodAsset in LODAssets)
                lodAsset?.Dispose();
        }

        public int LODLoadedCount()
        {
            return SceneLODInfoUtils.CountLOD(SuccessfullLODs) + SceneLODInfoUtils.CountLOD(FailedLODs);
        }
    }
}