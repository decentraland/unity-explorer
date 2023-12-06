using DCL.Landscape.Config;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.Landscape.Jobs
{
    [BurstCompile(CompileSynchronously = true)]
    public struct NoiseJob : IJobParallelFor
    {
        public NativeArray<float> Result;
        [ReadOnly] public NativeArray<float2> OctaveOffsets;
        [ReadOnly] public int Width;
        [ReadOnly] public int Height;
        [ReadOnly] public NoiseSettings NoiseSettings;
        [ReadOnly] public float MaxHeight;

        public void Execute(int index)
        {
            int x = index % Width;
            int y = index / Width;

            float halfWidth = Width / 2f;
            float halfHeight = Height / 2f;

            float amplitude = 1;
            float frequency = 1;
            float noiseHeight = 0;

            for (var i = 0; i < NoiseSettings.octaves; i++)
            {
                float sampleX = (x - halfWidth + OctaveOffsets[i].x) / NoiseSettings.scale * frequency;
                float sampleY = (y - halfHeight + OctaveOffsets[i].y) / NoiseSettings.scale * frequency;

                float perlinValue = (Mathf.PerlinNoise(sampleX, sampleY) * 2) - 1;
                noiseHeight += perlinValue * amplitude;

                amplitude *= NoiseSettings.persistance;
                frequency *= NoiseSettings.lacunarity;
            }

            Result[index] = NoiseSettings.invert ? -noiseHeight : noiseHeight;

            if (NoiseSettings.normalize)
            {
                float normalizedHeight = (Result[index] + 1) / (MaxHeight / 0.9f);
                Result[index] = Mathf.Clamp(normalizedHeight, 0, int.MaxValue);
            }
        }
    }
}
