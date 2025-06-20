using UnityEngine;
using System;

namespace DCL.VoiceChat
{
    public static class VoiceChatAudioResampler
    {
        public static void ResampleCubic(Span<float> input, int inputRate, Span<float> output, int outputRate)
        {
            float ratio = (float)inputRate / outputRate;
            int lastIdx = input.Length - 1;

            for (var i = 0; i < output.Length; i++)
            {
                float sourceIndex = i * ratio;
                var idx = (int)sourceIndex;
                float mu = sourceIndex - idx;
                float y0 = input[Mathf.Clamp(idx - 1, 0, lastIdx)];
                float y1 = input[Mathf.Clamp(idx, 0, lastIdx)];
                float y2 = input[Mathf.Clamp(idx + 1, 0, lastIdx)];
                float y3 = input[Mathf.Clamp(idx + 2, 0, lastIdx)];
                output[i] = CubicInterpolate(y0, y1, y2, y3, mu);
            }
        }

        private static float CubicInterpolate(float y0, float y1, float y2, float y3, float mu)
        {
            float a0 = y3 - y2 - y0 + y1;
            float a1 = y0 - y1 - a0;
            float a2 = y2 - y0;
            float a3 = y1;
            return (a0 * mu * mu * mu) + (a1 * mu * mu) + (a2 * mu) + a3;
        }
    }
}
