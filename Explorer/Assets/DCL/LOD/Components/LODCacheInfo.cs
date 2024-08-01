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
        public readonly LODGroup LodGroup;
        public readonly LODAsset[] LODAssets;

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
        }

        public int LODLoadedCount()
        {
            return SceneLODInfoUtils.LODCount(SuccessfullLODs) + SceneLODInfoUtils.LODCount(FailedLODs);
        }
        
    }
}