using AOT;
using Decentraland.Terrain;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using TerrainData = Decentraland.Terrain.TerrainData;

namespace TerrainProto
{
    [BurstCompile] [CreateAssetMenu]
    public sealed class MountainsTerrainData : TerrainData
    {
        protected override void CompileNoiseFunctions()
        {
            getHeight = BurstCompiler.CompileFunctionPointer(new GetHeightDelegate(GetHeight));
            getNormal = BurstCompiler.CompileFunctionPointer(new GetNormalDelegate(GetNormal));
        }

        [BurstCompile] [MonoPInvokeCallback(typeof(GetHeightDelegate))]
        private static float GetHeight(float x, float z) =>
            MountainsNoise.GetHeight(x, z);

        [BurstCompile] [MonoPInvokeCallback(typeof(GetNormalDelegate))]
        private static void GetNormal(float x, float z, out float3 normal) =>
            normal = MountainsNoise.GetNormal(x, z);
    }
}
