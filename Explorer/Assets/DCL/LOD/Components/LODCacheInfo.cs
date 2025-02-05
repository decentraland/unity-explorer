﻿using System;
using UnityEngine;

namespace DCL.LOD.Components
{
    public class LODCacheInfo : IDisposable
    {
        public readonly LODGroup LodGroup;
        public LODAsset[] LODAssets { get; private set; }

        public float CullRelativeHeightPercentage;
        public float LODChangeRelativeDistance;
        
        //We can represent 8 LODS loaded state with a byte
        public byte SuccessfullLODs;
        public byte FailedLODs;


        private readonly int lodLevels;
        public LODCacheInfo(LODGroup lodGroup, int lodLevels)
        {
            LodGroup = lodGroup;
            LODAssets = new LODAsset[lodLevels];
            CullRelativeHeightPercentage = 0;
            LODChangeRelativeDistance = 0;
            SuccessfullLODs = 0;
            FailedLODs = 0;
            this.lodLevels = lodLevels;
        }

        public void Reset()
        {
            LODAssets = new LODAsset[lodLevels];
            SuccessfullLODs = 0;
            FailedLODs = 0;
        }
        
        public void Dispose()
        {
            foreach (var lodAsset in LODAssets)
                lodAsset?.Dispose();

            LODAssets = null;
        }

        public int LODLoadedCount()
        {
            return SceneLODInfoUtils.LODCount(SuccessfullLODs) + SceneLODInfoUtils.LODCount(FailedLODs);
        }
        
    }
}