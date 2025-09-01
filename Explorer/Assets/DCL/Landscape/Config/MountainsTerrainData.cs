using AOT;
using Decentraland.Terrain;
using Unity.Burst;
using UnityEngine;
using TerrainData = Decentraland.Terrain.TerrainData;

namespace TerrainProto
{
    [BurstCompile] [CreateAssetMenu]
    public sealed class MountainsTerrainData : TerrainData
    {
        protected override void CompileNoiseFunctions() =>
            getHeight = BurstCompiler.CompileFunctionPointer(new GetHeightDelegate(GetHeight));

        [BurstCompile] [MonoPInvokeCallback(typeof(GetHeightDelegate))]
        private static float GetHeight(float x, float z) =>
            MountainsNoise.GetHeight(x, z);
    }
}
