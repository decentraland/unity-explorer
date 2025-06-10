using Cysharp.Threading.Tasks;
using DCL.Landscape.Utils;
using System;
using UnityEngine;

namespace DCL.Landscape
{
    public class TerrainDetailSetter
    {
        public async UniTask ReadApplyTerrainDetailAsync(TerrainData terrainData,
            TerrainGeneratorLocalCache localCache, int offsetX, int offsetZ, int i)
        {
            int[,]? detailLayer = await localCache.GetDetailLayerAsync(offsetX, offsetZ, i);
            ApplyDetailLayer(terrainData, i, detailLayer);
        }

        public void ApplyDetailLayer(TerrainData terrainData, int i, int[,] detailLayer)
        {
            try { terrainData.SetDetailLayer(0, 0, i, detailLayer); }
            catch (Exception e) { Debug.Log("WHY"); }
        }
    }
}
