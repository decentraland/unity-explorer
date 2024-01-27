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

        public static float CalculateOctaves(ref Unity.Mathematics.Random random, ref NoiseSettings settings, ref NativeArray<float2> octaveOffsets)
        {
            float maxPossibleHeight = 0;
            float amplitude = 1;

            for (var i = 0; i < settings.octaves; i++)
            {
                float offsetX = random.NextFloat(-BIG_VALUE, BIG_VALUE) + settings.offset.x;
                float offsetY = random.NextFloat(-BIG_VALUE, BIG_VALUE) - settings.offset.y;
                octaveOffsets[i] = new float2(offsetX, offsetY);
                maxPossibleHeight += amplitude;
                amplitude *= settings.persistance;
            }

            return maxPossibleHeight;
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
        public uint seed;
        public Vector2 offset;
        public float cutoff;
        public NoiseType noiseType;
        public float baseValue;
        [Range(1, 10)]
        public float multiplyValue;
        [Range(1, 10)]
        public float divideValue;

        public void ValidateValues()
        {
            scale = Mathf.Max(scale, 0.01f);
            octaves = Mathf.Max(octaves, 1);
            lacunarity = Mathf.Max(lacunarity, 1);
            persistance = Mathf.Clamp01(persistance);
            seed = (uint)Mathf.Max(seed, 1);
        }
    }

    public enum NoiseType
    {
        PERLIN,
        SIMPLEX,
        CELLULAR,
    }
}
