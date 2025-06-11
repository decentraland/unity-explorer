using Cysharp.Threading.Tasks;
using DCL.Landscape.Utils;
using System;
using UnityEngine;

namespace DCL.Landscape
{

    /// <summary>
    ///     Helper class to be used with GPUI. If we are using it, we want details to be applied only by the GPUIWrapper, not on the terrainData
    /// </summary>
    public interface ITerrainDetailSetter
    {
        UniTask ReadApplyTerrainDetailAsync(TerrainData terrainData,
            TerrainGeneratorLocalCache localCache, int offsetX, int offsetZ, int i);

        void ApplyDetailLayer(TerrainData terrainData, int i, int[,] detailLayer);
    }

    //This method will later be setted via GPUI
    public class GPUTerrainDetailSetter : ITerrainDetailSetter
    {
        public UniTask ReadApplyTerrainDetailAsync(TerrainData terrainData, TerrainGeneratorLocalCache localCache, int offsetX, int offsetZ, int i) =>
            UniTask.CompletedTask;

        public void ApplyDetailLayer(TerrainData terrainData, int i, int[,] detailLayer) { }
    }

    public class CPUTerrainDetailSetter : ITerrainDetailSetter
    {

        public async UniTask ReadApplyTerrainDetailAsync(TerrainData terrainData,
            TerrainGeneratorLocalCache localCache, int offsetX, int offsetZ, int i)
        {
            int[,]? detailLayer = await localCache.GetDetailLayerAsync(offsetX, offsetZ, i);
            ApplyDetailLayer(terrainData, i, detailLayer);
        }

        public void ApplyDetailLayer(TerrainData terrainData, int i, int[,] detailLayer)
        {
            terrainData.SetDetailLayer(0, 0, i, detailLayer);
        }
    }
}
