using DCL.Landscape.Config;
using System;
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
        private NativeArray<float> result;
        [ReadOnly] private NativeArray<float2> octaveOffsets;
        [ReadOnly] private readonly int width;
        [ReadOnly] private readonly int height;
        [ReadOnly] private readonly NoiseSettings noiseSettings;
        [ReadOnly] private readonly float maxHeight;
        [ReadOnly] private readonly float2 offset;
        [ReadOnly] private readonly NoiseJobOperation operation;

        public NoiseJob(
            NativeArray<float> result,
            in NativeArray<float2> octaveOffsets,
            int width,
            int height,
            in NoiseSettings noiseSettings,
            float maxHeight,
            float2 offset,
            NoiseJobOperation operation)
        {
            this.offset = offset;
            this.operation = operation;
            this.result = result;
            this.octaveOffsets = octaveOffsets;
            this.width = width;
            this.height = height;
            this.noiseSettings = noiseSettings;
            this.maxHeight = maxHeight;
        }

        public void Execute(int index)
        {
            int x = index % width;
            int y = index / width;

            float halfWidth = width / 2f;
            float halfHeight = height / 2f;

            float amplitude = 1;
            float frequency = 1;
            float noiseHeight = 0;

            for (var i = 0; i < noiseSettings.octaves; i++)
            {
                float sampleX = (x - halfWidth + octaveOffsets[i].x + offset.x) / noiseSettings.scale * frequency;
                float sampleY = (y - halfHeight + octaveOffsets[i].y + offset.y) / noiseSettings.scale * frequency;

                float noiseValue = 0;

                switch (noiseSettings.noiseType)
                {
                    case NoiseType.PERLIN:
                        noiseValue = noise.cnoise(new float2(sampleX, sampleY));
                        break;
                    case NoiseType.SIMPLEX:
                        noiseValue = noise.snoise(new float2(sampleX, sampleY));
                        break;
                    case NoiseType.CELLULAR:
                        noiseValue = noise.cellular(new float2(sampleX, sampleY)).x;
                        break;
                    default: throw new ArgumentOutOfRangeException();
                }

                noiseValue = (noiseValue * 2) - 1;
                noiseHeight += noiseValue * amplitude;

                amplitude *= noiseSettings.persistance;
                frequency *= noiseSettings.lacunarity;
            }

            float tempValue = noiseSettings.invert ? -noiseHeight : noiseHeight;

            tempValue += noiseSettings.baseValue;
            tempValue *= math.max(noiseSettings.multiplyValue, 1);
            tempValue /= math.max(noiseSettings.divideValue, 1);

            if (noiseSettings.normalize)
            {
                float normalizedHeight = (tempValue + 1) / maxHeight;
                tempValue = Mathf.Clamp(normalizedHeight, 0, 1);
            }

            if (tempValue < noiseSettings.cutoff)
                tempValue = 0;

            switch (operation)
            {
                case NoiseJobOperation.SET:
                    result[index] = tempValue;
                    break;
                case NoiseJobOperation.ADD:
                    result[index] += tempValue;
                    break;
                case NoiseJobOperation.MULTIPLY:
                    result[index] *= tempValue;
                    break;
                case NoiseJobOperation.SUBTRACT:
                    result[index] -= tempValue;
                    break;
            }
        }
    }
}
