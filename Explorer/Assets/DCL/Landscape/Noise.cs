using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Random = System.Random;

namespace DCL.Landscape.Config
{
    public static class Noise
    {
        private const int BIG_VALUE = 100000;

        private static float CalculateOctaves(Func<float> nextRandom, ref NoiseSettings settings, ref NativeArray<float2> octaveOffsets)
        {
            float maxPossibleHeight = 0;
            float amplitude = 1;

            for (var i = 0; i < settings.octaves; i++)
            {
                float offsetX = nextRandom() + settings.offset.x;
                float offsetY = nextRandom() - settings.offset.y;
                octaveOffsets[i] = new float2(offsetX, offsetY);
                maxPossibleHeight += amplitude;
                amplitude *= settings.persistance;
            }

            return maxPossibleHeight;
        }

        public static float CalculateOctaves(Random random, ref NoiseSettings settings, ref NativeArray<float2> octaveOffsets)
        {
            return CalculateOctaves(() => random.Next(-BIG_VALUE, BIG_VALUE), ref settings, ref octaveOffsets);
        }

        public static float CalculateOctaves(Unity.Mathematics.Random random, ref NoiseSettings settings, ref NativeArray<float2> octaveOffsets)
        {
            return CalculateOctaves(() => random.NextFloat(-BIG_VALUE, BIG_VALUE), ref settings, ref octaveOffsets);
        }
    }

    [Serializable]
    public struct NoiseSettings
    {
        public bool invert;
        public bool normalize;
        public float scale;
        [Range(1, 10)] public int octaves;
        [Range(0.1f, 1)] public float persistance;
        public float lacunarity;
        public int seed;
        public Vector2 offset;
        public float cutoff;
        public NoiseType noiseType;

        public void ValidateValues()
        {
            scale = Mathf.Max(scale, 0.01f);
            octaves = Mathf.Max(octaves, 1);
            lacunarity = Mathf.Max(lacunarity, 1);
            persistance = Mathf.Clamp01(persistance);
        }
    }

    public enum NoiseType
    {
        PERLIN,
        SIMPLEX,
        CELLULAR,
    }
}
