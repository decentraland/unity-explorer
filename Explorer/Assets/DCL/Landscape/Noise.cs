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

        public static float CalculateOctaves(NoiseData data, ref NativeArray<float2> octaveOffsets)
        {
            var random = new Random(data.settings.seed);
            float maxPossibleHeight = 0;
            float amplitude = 1;

            for (var i = 0; i < data.settings.octaves; i++)
            {
                float offsetX = random.Next(-BIG_VALUE, BIG_VALUE) + data.settings.offset.x;
                float offsetY = random.Next(-BIG_VALUE, BIG_VALUE) - data.settings.offset.y;
                octaveOffsets[i] = new float2(offsetX, offsetY);
                maxPossibleHeight += amplitude;
                amplitude *= data.settings.persistance;
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
        public int seed;
        public Vector2 offset;

        public void ValidateValues()
        {
            scale = Mathf.Max(scale, 0.01f);
            octaves = Mathf.Max(octaves, 1);
            lacunarity = Mathf.Max(lacunarity, 1);
            persistance = Mathf.Clamp01(persistance);
        }
    }
}
