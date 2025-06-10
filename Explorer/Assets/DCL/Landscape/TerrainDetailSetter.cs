using Cysharp.Threading.Tasks;
using DCL.Landscape.Utils;
using UnityEngine;

namespace DCL.Landscape
{
    /// <summary>
    ///     Helper class to be used with GPUI. If we are using it, we want details to be applied only by the GPUIWrapper, not on the terrainData
    /// </summary>
    public class TerrainDetailSetter
    {
        private readonly bool isGPUIPresent;

        public TerrainDetailSetter(bool isGpuiPresent)
        {
            isGPUIPresent = isGpuiPresent;
        }


        public async UniTask ReadApplyTerrainDetailAsync(TerrainData terrainData,
            TerrainGeneratorLocalCache localCache, int offsetX, int offsetZ, int i)
        {
            if (isGPUIPresent) return;

            int[,]? detailLayer = await localCache.GetDetailLayerAsync(offsetX, offsetZ, i);
            ApplyDetailLayer(terrainData, i, detailLayer);
        }

        public void ApplyDetailLayer(TerrainData terrainData, int i, int[,] detailLayer)
        {
            if (isGPUIPresent) return;

            terrainData.SetDetailLayer(0, 0, i, detailLayer);
        }
    }
}
