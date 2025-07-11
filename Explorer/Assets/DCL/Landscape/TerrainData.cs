using AOT;
using Decentraland.Terrain;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.Landscape
{
    [BurstCompile, CreateAssetMenu(menuName = "DCL/Landscape/Terrain Data")]
    public sealed class TerrainData : Decentraland.Terrain.TerrainData
    {
        protected override void CompileNoiseFunctions()
        {
            getHeight = BurstCompiler.CompileFunctionPointer(new GetHeightDelegate(GetHeight));
            getNormal = BurstCompiler.CompileFunctionPointer(new GetNormalDelegate(GetNormal));
        }

        [BurstCompile, MonoPInvokeCallback(typeof(GetHeightDelegate))]
        private static float GetHeight(float x, float z) =>
            GeoffNoise.GetHeight(x, z);

        [BurstCompile, MonoPInvokeCallback(typeof(GetNormalDelegate))]
        private static void GetNormal(float x, float z, out float3 normal) =>
            normal = GeoffNoise.GetNormal(x, z);
    }
}
