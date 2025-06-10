using Cysharp.Threading.Tasks;
using DCL.Landscape.Utils;
using System;
using UnityEngine;

namespace DCL.Landscape
{
    public class TerrainDetailSetter
    {
        private readonly bool avoidApplyingDetails;

        public TerrainDetailSetter(bool avoidApplyingDetails)
        {
            this.avoidApplyingDetails = avoidApplyingDetails;
        }


        public async UniTask ReadApplyTerrainDetailAsync(TerrainData terrainData,
            TerrainGeneratorLocalCache localCache, int offsetX, int offsetZ, int i)
        {
            if (avoidApplyingDetails) return;

            int[,]? detailLayer = await localCache.GetDetailLayerAsync(offsetX, offsetZ, i);
            ApplyDetailLayer(terrainData, i, detailLayer);
        }

        public void ApplyDetailLayer(TerrainData terrainData, int i, int[,] detailLayer)
        {
            if (avoidApplyingDetails) return;
            terrainData.SetDetailLayer(0, 0, i, detailLayer);
        }
    }
}
