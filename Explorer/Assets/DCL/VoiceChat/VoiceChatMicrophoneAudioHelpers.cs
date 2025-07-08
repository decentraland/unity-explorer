using Unity.Burst;
using Unity.Mathematics;
using System;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Burst-optimized audio processing helpers for mono conversion and resampling
    /// </summary>
    public static class VoiceChatMicrophoneAudioHelpers
    {
        /// <summary>
        /// Converts multi-channel audio to mono
        /// </summary>
        /// <param name="input">Input audio data span (interleaved channels)</param>
        /// <param name="output">Output mono audio data span</param>
        /// <param name="channels">Number of input channels</param>
        /// <param name="samplesPerChannel">Number of samples per channel</param>
        [BurstCompile]
        public static void ConvertToMono(Span<float> input, Span<float> output, int channels, int samplesPerChannel)
        {
            if (channels == 1)
            {
                input.Slice(0, samplesPerChannel).CopyTo(output);
                return;
            }

            if (channels == 2)
            {
                ConvertStereoToMono(input, output, samplesPerChannel);
                return;
            }

            // General case for any number of channels
            for (int i = 0; i < samplesPerChannel; i++)
            {
                float sum = 0f;
                for (int ch = 0; ch < channels; ch++)
                {
                    sum += input[(i * channels) + ch];
                }
                output[i] = sum / channels;
            }
        }

        /// <summary>
        /// Optimized stereo to mono conversion using SIMD
        /// </summary>
        [BurstCompile]
        private static void ConvertStereoToMono(Span<float> input, Span<float> output, int samplesPerChannel)
        {
            // Process 4 samples at a time using SIMD when possible
            int simdCount = samplesPerChannel & ~3; // Round down to nearest 4
            
            for (int i = 0; i < simdCount; i += 4)
            {
                // Load 4 stereo pairs (8 floats total)
                float4 left = new float4(
                    input[i * 2],
                    input[(i + 1) * 2],
                    input[(i + 2) * 2],
                    input[(i + 3) * 2]
                );
                
                float4 right = new float4(
                    input[i * 2 + 1],
                    input[(i + 1) * 2 + 1],
                    input[(i + 2) * 2 + 1],
                    input[(i + 3) * 2 + 1]
                );
                
                // Average left and right channels
                float4 mono = (left + right) * 0.5f;
                
                // Store result
                output[i] = mono.x;
                output[i + 1] = mono.y;
                output[i + 2] = mono.z;
                output[i + 3] = mono.w;
            }
            
            // Handle remaining samples
            for (int i = simdCount; i < samplesPerChannel; i++)
            {
                output[i] = (input[i * 2] + input[i * 2 + 1]) * 0.5f;
            }
        }

        /// <summary>
        /// Resamples audio using cubic interpolation with optimized bounds checking
        /// </summary>
        /// <param name="input">Input audio data span</param>
        /// <param name="inputRate">Input sample rate</param>
        /// <param name="output">Output audio data span</param>
        /// <param name="outputRate">Output sample rate</param>
        [BurstCompile]
        public static void ResampleCubic(Span<float> input, int inputRate, Span<float> output, int outputRate)
        {
            if (inputRate == outputRate)
            {
                input.CopyTo(output);
                return;
            }

            float ratio = (float)inputRate / outputRate;
            int lastIdx = input.Length - 1;

            // Pre-calculate bounds to avoid repeated math.clamp calls
            for (int i = 0; i < output.Length; i++)
            {
                float sourceIndex = i * ratio;
                int idx = (int)sourceIndex;
                float mu = sourceIndex - idx;
                
                // Optimized bounds checking
                int idx1 = math.max(0, math.min(idx, lastIdx));
                int idx2 = math.max(0, math.min(idx + 1, lastIdx));
                int idx3 = math.max(0, math.min(idx + 2, lastIdx));
                int idx4 = math.max(0, math.min(idx + 3, lastIdx));
                
                float y0 = input[idx1];
                float y1 = input[idx2];
                float y2 = input[idx3];
                float y3 = input[idx4];
                
                output[i] = CubicInterpolate(y0, y1, y2, y3, mu);
            }
        }

        /// <summary>
        /// Fast resampling for common ratios (2:1, 1:2, etc.) using linear interpolation
        /// </summary>
        /// <param name="input">Input audio data span</param>
        /// <param name="output">Output audio data span</param>
        /// <param name="ratio">Resampling ratio (input/output)</param>
        [BurstCompile]
        public static void ResampleLinear(Span<float> input, Span<float> output, float ratio)
        {
            int lastIdx = input.Length - 1;

            for (int i = 0; i < output.Length; i++)
            {
                float sourceIndex = i * ratio;
                int idx = (int)sourceIndex;
                float mu = sourceIndex - idx;
                
                int idx1 = math.max(0, math.min(idx, lastIdx));
                int idx2 = math.max(0, math.min(idx + 1, lastIdx));
                
                float y1 = input[idx1];
                float y2 = input[idx2];
                
                output[i] = math.lerp(y1, y2, mu);
            }
        }

        /// <summary>
        /// Optimized cubic interpolation using Horner's method
        /// </summary>
        [BurstCompile]
        private static float CubicInterpolate(float y0, float y1, float y2, float y3, float mu)
        {
            // Use Horner's method for better numerical stability and performance
            float mu2 = mu * mu;
            float mu3 = mu2 * mu;
            
            // Catmull-Rom spline coefficients
            float c0 = -0.5f * y0 + 1.5f * y1 - 1.5f * y2 + 0.5f * y3;
            float c1 = y0 - 2.5f * y1 + 2f * y2 - 0.5f * y3;
            float c2 = -0.5f * y0 + 0.5f * y2;
            float c3 = y1;
            
            return ((c0 * mu + c1) * mu + c2) * mu + c3;
        }
    }
} 