﻿using UnityEngine;

namespace DCL.Multiplayer.Movement
{
    /// <summary>
    ///     Compress float via scaled integer approach (fixed-size quantization)
    /// </summary>
    public static class FloatQuantizer
    {
        public static int Compress(float value, float minValue, float maxValue, int bits)
        {
            int maxStep = (1 << bits) - 1;
            float normalizedValue = (value - minValue) / (maxValue - minValue);
            return Mathf.RoundToInt(Mathf.Clamp01(normalizedValue) * maxStep);
        }

        public static float Decompress(int compressed, float minValue, float maxValue, int bits)
        {
            float maxStep = (1 << bits) - 1f;
            float normalizedValue = compressed / maxStep;
            return (normalizedValue * (maxValue - minValue)) + minValue;
        }
    }
}