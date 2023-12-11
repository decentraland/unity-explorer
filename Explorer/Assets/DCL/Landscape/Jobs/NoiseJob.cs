using DCL.Landscape.Config;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.Landscape.Jobs
{
    public enum NoiseJobOperation
    {
        SET,
        ADD,
        MULTIPLY,
        SUBTRACT,
    }


    [BurstCompile(CompileSynchronously = true)]
    public struct NoiseJob : IJobParallelFor
    {
        public NativeArray<float> Result;
        [ReadOnly] public NativeArray<float2> OctaveOffsets;
        [ReadOnly] public int Width;
        [ReadOnly] public int Height;
        [ReadOnly] public NoiseSettings NoiseSettings;
        [ReadOnly] public float MaxHeight;
        [ReadOnly] private readonly float2 offset;
        [ReadOnly] private readonly NoiseJobOperation operation;

        public NoiseJob(ref NativeArray<float> result, in NativeArray<float2> octaveOffsets, int width, int height, in NoiseSettings noiseSettings,
            float maxHeight, float2 offset, NoiseJobOperation operation)
        {
            this.offset = offset;
            this.operation = operation;
            Result = result;
            OctaveOffsets = octaveOffsets;
            Width = width;
            Height = height;
            NoiseSettings = noiseSettings;
            MaxHeight = maxHeight;
        }

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
                float sampleX = (x - halfWidth + OctaveOffsets[i].x + offset.x) / NoiseSettings.scale * frequency;
                float sampleY = (y - halfHeight + OctaveOffsets[i].y + offset.y) / NoiseSettings.scale * frequency;

                float perlinValue = (Mathf.PerlinNoise(sampleX, sampleY) * 2) - 1;
                noiseHeight += perlinValue * amplitude;

                amplitude *= NoiseSettings.persistance;
                frequency *= NoiseSettings.lacunarity;
            }

            float tempValue = NoiseSettings.invert ? -noiseHeight : noiseHeight;

            if (NoiseSettings.normalize)
            {
                float normalizedHeight = (tempValue + 1) / MaxHeight;
                tempValue = Mathf.Clamp(normalizedHeight, 0, 1);
            }

            if (tempValue < NoiseSettings.cutoff)
                tempValue = 0;

            switch (operation)
            {
                case NoiseJobOperation.SET:
                    Result[index] = tempValue;
                    break;
                case NoiseJobOperation.ADD:
                    Result[index] += tempValue;
                    break;
                case NoiseJobOperation.MULTIPLY:
                    Result[index] *= tempValue;
                    break;
                case NoiseJobOperation.SUBTRACT:
                    Result[index] -= tempValue;
                    break;
            }
        }
    }
}
