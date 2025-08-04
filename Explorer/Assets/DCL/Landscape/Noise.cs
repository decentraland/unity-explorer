using System;
using UnityEngine;

namespace DCL.Landscape.Config
{
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
