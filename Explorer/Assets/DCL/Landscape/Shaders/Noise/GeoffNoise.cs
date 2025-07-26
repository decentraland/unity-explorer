#if !GEOFFNOISE_INCLUDED
#define GEOFFNOISE_INCLUDED

#if SHADER_TARGET
#define internal
#define private
#define static
#else
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

namespace Decentraland.Terrain
{
    internal static class GeoffNoise
    {
#endif

        // Modulo 289 without a division (only multiplications)
        private static float2 mod289(float2 x)
        {
            return x - floor(x * (1.0f / 289.0f)) * 289.0f;
        }

        // Hash function for pseudorandom values - improved version from Quilez
        private static float hash1(float n)
        {
            return frac(n * 17.0f * frac(n * 0.3183099f));
        }

        private static float hash1(float3 p)
        {
            float n = p.x + 317.0f * p.y + 157.0f * p.z;
            return hash1(n);
        }

        // Returns 4D vector: (noise_value, derivative_x, derivative_y, derivative_z)
        // This matches Quilez's exact implementation, converted to HLSL
        private static float4 noised(float3 x)
        {
            float3 p = floor(x);
            float3 w = frac(x);

            // Quintic interpolation function and its derivative
            float3 u = w * w * w * (w * (w * 6.0f - 15.0f) + 10.0f);
            float3 du = 30.0f * w * w * (w * (w - 2.0f) + 1.0f);

            // Sample random values at 8 corners using Quilez's method
            float n = p.x + 317.0f * p.y + 157.0f * p.z;

            float a = hash1(n + 0.0f);
            float b = hash1(n + 1.0f);
            float c = hash1(n + 317.0f);
            float d = hash1(n + 318.0f);
            float e = hash1(n + 157.0f);
            float f = hash1(n + 158.0f);
            float g = hash1(n + 474.0f);
            float h = hash1(n + 475.0f);

            // Compute interpolation coefficients (exactly as in Quilez code)
            float k0 = a;
            float k1 = b - a;
            float k2 = c - a;
            float k3 = e - a;
            float k4 = a - b - c + d;
            float k5 = a - c - e + g;
            float k6 = a - b - e + f;
            float k7 = -a + b + c - d + e - f - g + h;

            // Compute noise value (remapped to [-1,1] range)
            float noiseValue = k0 + k1 * u.x + k2 * u.y + k3 * u.z +
                               k4 * u.x * u.y + k5 * u.y * u.z + k6 * u.z * u.x +
                               k7 * u.x * u.y * u.z;
            noiseValue = -1.0f + 2.0f * noiseValue;  // Remap [0,1] to [-1,1]

            // Compute analytical derivatives (exactly as in Quilez code)
            float3 derivative = 2.0f * du * float3(
                k1 + k4 * u.y + k6 * u.z + k7 * u.y * u.z,
                k2 + k5 * u.z + k4 * u.x + k7 * u.z * u.x,
                k3 + k6 * u.x + k5 * u.y + k7 * u.x * u.y
            );

            return float4(noiseValue, derivative);
        }

        // Fractal Brownian Motion with analytical derivatives - HLSL version of Quilez's fbmd_7
        private static float4 fbm(float3 x, int numOctaves)
        {
            float f = 1.92f;  // Frequency multiplier per octave
            float s = 0.5f;   // Amplitude multiplier per octave
            float a = 0.0f;   // Accumulated value
            float b = 0.5f;   // Current amplitude
            float3 d = float3(0, 0, 0);  // Accumulated derivatives

            // Transform matrix per octave (starts as identity)
            float3x3 m = float3x3(1.0f, 0.0f, 0.0f,
                                  0.0f, 1.0f, 0.0f,
                                  0.0f, 0.0f, 1.0f);

            // Rotation matrices for octaves (from Quilez code)
            float3x3 m3 = float3x3(  0.00f,  0.80f,  0.60f,
                                    -0.80f,  0.36f, -0.48f,
                                    -0.60f, -0.48f,  0.64f);

            float3x3 m3i = float3x3(  0.00f, -0.80f, -0.60f,
                                      0.80f,  0.36f, -0.48f,
                                      0.60f, -0.48f,  0.64f);

            // Clamp octaves to prevent infinite loops
            numOctaves = min(numOctaves, 8);

            for (int i = 0; i < numOctaves; i++)
            {
                float4 n = noised(x);

                a += b * n.x;                    // Accumulate values
                d += b * mul(m, n.yzw);         // Accumulate derivatives with proper transform

                b *= s;                         // Reduce amplitude
                x = mul(m3, x) * f;            // Transform coordinates for next octave
                m = mul(m3i, m) * f;           // Update derivative transform matrix
            }

            return float4(a, d);
        }

        // Enhanced terrain function matching Quilez's approach
        private static float4 terrain(float3 pos, float frequency, int octaves)
        {
            // Apply frequency scaling - this becomes our "p/2000.0" equivalent
            float3 p = pos * frequency;

            // Get FBM noise and derivatives
            float4 noise = fbm(p, octaves);
            float height = noise.x;
            float3 derivative = noise.yzw;

            // Scale derivatives properly: if we sample at p*frequency,
            // derivatives w.r.t. original pos are derivative*frequency
            derivative *= frequency;

            // Optional: Simple erosion-like effect (much simpler than original)
            // This creates varied terrain but keeps it stable
            // float erosion = 1.0 / (1.0 + 0.05 * dot(derivative, derivative));
            // height *= erosion;
            // derivative *= erosion;

            return float4(height, derivative);
        }

        ///////////////////////////
        ///////////////////////////
        ///////////////////////////

        // Optimised hash function: 3 ALU
        private static float hash_optimised(float2 p)
        {
            return frac(sin(dot(p, float2(127.1f, 311.7f))) * 43758.5453f);
        }

        // Optimised 2D noise: ~25 ALU
        private static float noise2d_optimised(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);

            float a = hash_optimised(i);
            float b = hash_optimised(i + float2(1.0f,0.0f));
            float c = hash_optimised(i + float2(0.0f,1.0f));
            float d = hash_optimised(i + float2(1.0f,1.0f));

            float2 u = f * f * (3.0f - 2.0f * f); // smoothstep
            return lerp(lerp(a,b,u.x), lerp(c,d,u.x), u.y);
        }

        // Optimised terrain with 2 octaves: ~60 ALU total
        private static float terrain_optimised(float2 pos, float frequency)
        {
            float2 p = pos * frequency;
            float height = 0.0f;
            float amp = 0.5f;
            float freq = 1.0f;

            // Octave 1
            height += noise2d_optimised(p * freq) * amp;
            amp *= 0.5f; freq *= 2.0f;

            // Octave 2
            height += noise2d_optimised(p * freq) * amp;

            return height;
        }

        // Fast normal calculation using finite differences: ~190 ALU
        private static float3 getNormal_optimised(float2 worldPos, float frequency, int quality)
        {
            float eps = 0.1f / frequency; // Scale epsilon with frequency

            float h_center = terrain_optimised(worldPos, frequency);
            float h_right = terrain_optimised(worldPos + float2(eps, 0.0f), frequency);
            float h_up = terrain_optimised(worldPos + float2(0.0f, eps), frequency);

            float dhdx = (h_right - h_center) / eps;
            float dhdy = (h_up - h_center) / eps;

            return normalize(float3(-dhdx, 1.0f, -dhdy));
        }

        // Optimised version - 60 ALU
        private static float getHeight_optimised(float2 worldPos, float frequency)
        {
            return terrain_optimised(worldPos, frequency);
        }

        // With normals - varies by quality
        private static float4 getHeightAndNormal(float2 worldPos, float frequency, int quality)
        {
            return float4(getHeight_optimised(worldPos, frequency), getNormal_optimised(worldPos, frequency, 0));
        }

        // Float Version - Integer Hash Optimized Noise
        // CPU/GPU shared code - all operations use float instead of half

        // ============================================================================
        // INTEGER-ONLY HASH FUNCTIONS
        // ============================================================================

        // Lowbias32 hash function: ~5 ALU
        private static uint hash_int(uint x)
        {
            x ^= x >> 16;
            x *= 0x7feb352du;
            x ^= x >> 15;
            x *= 0x846ca68bu;
            x ^= x >> 16;
            return x;
        }

        // 2D integer hash
        private static uint hash_int2(int2 p)
        {
            // Use large primes to avoid correlation
            uint h = (uint)p.x * 73856093u + (uint)p.y * 19349663u;
            return hash_int(h);
        }

        // Convert integer hash to float in [0,1] range: ~1 ALU
        private static float hash_int_to_float(int2 p)
        {
            uint h = hash_int2(p);
            return (float)h * (1.0f / 4294967296.0f); // Convert to [0,1]
        }

        // ============================================================================
        // POSITION QUANTIZATION
        // ============================================================================

        // Quantize world position from metres to centimeters
        private static int2 quantize_to_cm(float2 worldPos)
        {
            return (int2)(worldPos * 100.0f); // Convert metres to centimeters
        }

        // ============================================================================
        // INTEGER-BASED NOISE (Float version of optimized functions)
        // ============================================================================

        // Integer hash function: 5 ALU
        private static float hash_optimised_int(float2 p)
        {
            // Quantize position to centimeters
            int2 ipos = quantize_to_cm(p);

            // Use lowbias32 hash and convert to float
            return hash_int_to_float(ipos);
        }

        // Integer-based 2D noise: ~30 ALU
        private static float noise2d_optimised_int(float2 p)
        {
            // Quantize to centimeters first
            int2 ipos = quantize_to_cm(p);

            // Get grid coordinates in centimeter space
            int2 i = ipos / 100; // Grid cell (back to meter grid)

            // Get fractional part within the cell (0-99 cm converted to 0-1)
            float2 f = (float2)(ipos % 100) * 0.01f;

            // Handle negative coordinates properly
            if (f.x < 0.0f) f.x += 1.0f;
            if (f.y < 0.0f) f.y += 1.0f;

            // Hash the four corners
            float a = hash_int_to_float(i);
            float b = hash_int_to_float(i + int2(1, 0));
            float c = hash_int_to_float(i + int2(0, 1));
            float d = hash_int_to_float(i + int2(1, 1));

            // Smoothstep interpolation
            float2 u = f * f * (3.0f - 2.0f * f);
            return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
        }

        // Integer-based terrain with 2 octaves: ~70 ALU total
        private static float terrain_optimised_int(float2 pos, float frequency)
        {
            float2 p = pos * frequency;
            float height = 0.0f;
            float amp = 0.5f;
            float freq = 1.0f;

            // Octave 1
            height += noise2d_optimised_int(p * freq) * amp;
            amp *= 0.5f; freq *= 2.0f;

            // Octave 2
            height += noise2d_optimised_int(p * freq) * amp;

            return height;
        }

        // Integer-based normal calculation: ~210 ALU
        private static float3 getNormal_optimised_int(float2 worldPos, float frequency, int quality)
        {
            float eps = 0.1f / frequency; // Scale epsilon with frequency

            float h_center = terrain_optimised_int(worldPos, frequency);
            float h_right = terrain_optimised_int(worldPos + float2(eps, 0), frequency);
            float h_up = terrain_optimised_int(worldPos + float2(0, eps), frequency);

            float dhdx = (h_right - h_center) / eps;
            float dhdy = (h_up - h_center) / eps;

            return normalize(float3(-dhdx, 1.0f, -dhdy));
        }

        // Integer-based height function: 70 ALU
        private static float getHeight_optimised_int(float2 worldPos, float frequency)
        {
            return terrain_optimised_int(worldPos, frequency);
        }

        // Integer-based height and normal: varies by quality (float version of getHeightAndNormal)
        private static float4 getHeightAndNormal_int(float2 worldPos, float frequency, int quality)
        {
            return float4(getHeight_optimised_int(worldPos, frequency), getNormal_optimised_int(worldPos, frequency, quality));
        }

        /// <summary>
        /// lowbias32 updated with the latest best constants. See
        /// https://nullprogram.com/blog/2018/07/31/ and
        /// https://github.com/skeeto/hash-prospector/issues/19
        /// </summary>
        internal static uint lowbias32(uint x)
        {
            x ^= x >> 16;
            x *= 0x21f0aaad;
            x ^= x >> 15;
            x *= 0xd35a2d97;
            x ^= x >> 15;
            return x;
        }

        // ============================================================================
        // HEIGHT GENERATION USED FOR TEXTURE SETUP
        // ============================================================================

        // Hash function for pseudorandom values - integer version
        private static float hash1(uint n)
        {
            uint hashed = lowbias32(n);
            // Convert to float in [0,1] range
            return (float)hashed / 4294967295.0f; // uint max value
        }

        private static float SafeHash1(uint n)
        {
            uint hashed = lowbias32(n);
            // Use more stable normalization
            return (float)hashed * (1.0f / 4294967296.0f); // 2^32
        }

        private static float hash1(int3 p)
        {
            // Combine coordinates using large primes to avoid patterns
            // Cast to uint to handle negative numbers properly
            uint n = (uint)p.x + 317u * (uint)p.y + 157u * (uint)p.z;
            return hash1(n);
        }

        private static float SafeHash1(int3 p)
        {
            // Ensure positive coordinates for consistent hashing
            uint3 up = uint3(p + 100000); // Offset to ensure positive
            uint n = up.x + 317u * up.y + 157u * up.z;
            return SafeHash1(n);
        }

        // More stable coordinate quantization
        private static int3 SafeQuantize(float3 x, float scale)
        {
            // Add small epsilon to avoid edge cases
            float3 scaled = x * scale + 0.0001f;
            // Use round instead of floor for more stable behavior
            return int3(round(scaled));
        }

        // Returns 4D vector: (noise_value, derivative_x, derivative_y, derivative_z)
        // Modified to work with integer coordinates (centimeters)
        private static float4 noised_ts(float3 x)
        {
            // Quantize to centimeters and convert to int
            int3 p = (int3)floor(x * 100.0f);
            float3 w = frac(x * 100.0f);

            // Quintic interpolation function and its derivative
            float3 u = w * w * w * (w * (w * 6.0f - 15.0f) + 10.0f);
            float3 du = 30.0f * w * w * (w * (w - 2.0f) + 1.0f);

            // Sample random values at 8 corners using integer hash
            float a = hash1(p + int3(0, 0, 0));
            float b = hash1(p + int3(1, 0, 0));
            float c = hash1(p + int3(0, 1, 0));
            float d = hash1(p + int3(1, 1, 0));
            float e = hash1(p + int3(0, 0, 1));
            float f = hash1(p + int3(1, 0, 1));
            float g = hash1(p + int3(0, 1, 1));
            float h = hash1(p + int3(1, 1, 1));

            // Compute interpolation coefficients (exactly as in Quilez code)
            float k0 = a;
            float k1 = b - a;
            float k2 = c - a;
            float k3 = e - a;
            float k4 = a - b - c + d;
            float k5 = a - c - e + g;
            float k6 = a - b - e + f;
            float k7 = -a + b + c - d + e - f - g + h;

            // Compute noise value (remapped to [-1,1] range)
            float noiseValue = k0 + k1 * u.x + k2 * u.y + k3 * u.z +
                               k4 * u.x * u.y + k5 * u.y * u.z + k6 * u.z * u.x +
                               k7 * u.x * u.y * u.z;
            noiseValue = -1.0f + 2.0f * noiseValue;  // Remap [0,1] to [-1,1]

            // Compute analytical derivatives - need to scale by 100 since we're working in centimeter space
            float3 derivative = 2.0f * du * 100.0f * float3(
                k1 + k4 * u.y + k6 * u.z + k7 * u.y * u.z,
                k2 + k5 * u.z + k4 * u.x + k7 * u.z * u.x,
                k3 + k6 * u.x + k5 * u.y + k7 * u.x * u.y
            );

            return float4(noiseValue, derivative);
        }

        // Fractal Brownian Motion with analytical derivatives - integer version
        private static float4 fbm_ts(float3 x, int numOctaves)
        {
            float f = 1.92f;  // Frequency multiplier per octave
            float s = 0.5f;   // Amplitude multiplier per octave
            float a = 0.0f;   // Accumulated value
            float b = 0.5f;   // Current amplitude
            float3 d = float3(0, 0, 0);  // Accumulated derivatives

            // Transform matrix per octave (starts as identity)
            float3x3 m = float3x3(1.0f, 0.0f, 0.0f,
                                  0.0f, 1.0f, 0.0f,
                                  0.0f, 0.0f, 1.0f);

            // Rotation matrices for octaves (from Quilez code)
            float3x3 m3 = float3x3(  0.00f,  0.80f,  0.60f,
                                    -0.80f,  0.36f, -0.48f,
                                    -0.60f, -0.48f,  0.64f);

            float3x3 m3i = float3x3(  0.00f, -0.80f, -0.60f,
                                      0.80f,  0.36f, -0.48f,
                                      0.60f, -0.48f,  0.64f);

            // Clamp octaves to prevent infinite loops
            numOctaves = min(numOctaves, 8);

            for (int i = 0; i < numOctaves; i++)
            {
                float4 n = noised_ts(x);

                a += b * n.x;                    // Accumulate values
                d += b * mul(n.yzw, m);         // Accumulate derivatives with proper transform

                b *= s;                         // Reduce amplitude
                x = mul(x, m3) * f;            // Transform coordinates for next octave
                m = mul(m, m3i) * f;           // Update derivative transform matrix
            }

            return float4(a, d);
        }

        // Deterministic noise function
        private static float4 DeterministicNoise(float3 x)
        {
            // Use consistent quantization
            const float SCALE = 100.0f;
            int3 p = SafeQuantize(x, SCALE);
            float3 w = frac(x * SCALE + 0.0001f); // Small epsilon for stability

            // Ensure w is in [0,1] range
            w = clamp(w, 0.0f, 1.0f);

            // More stable quintic interpolation
            float3 u = w * w * w * (w * (w * 6.0f - 15.0f) + 10.0f);
            float3 du = 30.0f * w * w * (w * (w - 2.0f) + 1.0f);

            // Sample with consistent hashing
            float a = SafeHash1(p + int3(0, 0, 0));
            float b = SafeHash1(p + int3(1, 0, 0));
            float c = SafeHash1(p + int3(0, 1, 0));
            float d = SafeHash1(p + int3(1, 1, 0));
            float e = SafeHash1(p + int3(0, 0, 1));
            float f = SafeHash1(p + int3(1, 0, 1));
            float g = SafeHash1(p + int3(0, 1, 1));
            float h = SafeHash1(p + int3(1, 1, 1));

            // Trilinear interpolation coefficients
            float k0 = a;
            float k1 = b - a;
            float k2 = c - a;
            float k3 = e - a;
            float k4 = a - b - c + d;
            float k5 = a - c - e + g;
            float k6 = a - b - e + f;
            float k7 = -a + b + c - d + e - f - g + h;

            // Compute noise value
            float noiseValue = k0 + k1 * u.x + k2 * u.y + k3 * u.z +
                               k4 * u.x * u.y + k5 * u.y * u.z + k6 * u.z * u.x +
                               k7 * u.x * u.y * u.z;

            // Remap to [-1,1] with stable math
            noiseValue = mad(noiseValue, 2.0f, -1.0f);

            // Compute derivatives with consistent scaling
            float3 derivative = 2.0f * du * SCALE * float3(
                k1 + k4 * u.y + k6 * u.z + k7 * u.y * u.z,
                k2 + k5 * u.z + k4 * u.x + k7 * u.z * u.x,
                k3 + k6 * u.x + k5 * u.y + k7 * u.x * u.y
            );

            return float4(noiseValue, derivative);
        }

        // Simplified FBM that avoids matrix accumulation issues
        private static float4 StableFBM(float3 x, int numOctaves)
        {
            float frequency = 1.92f;
            float amplitude = 0.5f;
            float value = 0.0f;
            float3 derivative = float3(0.0f, 0.0f, 0.0f);

            #if SHADER_TARGET
                // Pre-computed rotation matrices to avoid accumulation
                static const float3x3 rotations[8] = {
                    float3x3(1.00f, 0.00f, 0.00f,  0.00f, 1.00f, 0.00f,  0.00f, 0.00f, 1.00f),
                    float3x3(0.00f, 0.80f, 0.60f, -0.80f, 0.36f,-0.48f, -0.60f,-0.48f, 0.64f),
                    float3x3(0.64f,-0.48f,-0.60f, -0.48f, 0.36f, 0.80f, -0.60f, 0.80f, 0.00f),
                    float3x3(0.00f,-0.80f, 0.60f,  0.80f, 0.36f, 0.48f, -0.60f, 0.48f, 0.64f),
                    float3x3(0.64f, 0.48f,-0.60f,  0.48f, 0.36f,-0.80f, -0.60f,-0.80f, 0.00f),
                    float3x3(0.00f, 0.80f,-0.60f, -0.80f, 0.36f, 0.48f,  0.60f, 0.48f, 0.64f),
                    float3x3(0.64f,-0.48f, 0.60f, -0.48f, 0.36f,-0.80f,  0.60f,-0.80f, 0.00f),
                    float3x3(0.00f,-0.80f,-0.60f,  0.80f, 0.36f,-0.48f,  0.60f,-0.48f, 0.64f)
                };
            #else
                float3x3[] rotations = new float3x3[8];
                rotations[0] = new float3x3(1.00f, 0.00f, 0.00f,  0.00f, 1.00f, 0.00f,  0.00f, 0.00f, 1.00f);
                rotations[1] = new float3x3(0.00f, 0.80f, 0.60f, -0.80f, 0.36f,-0.48f, -0.60f,-0.48f, 0.64f);
                rotations[2] = new float3x3(0.64f,-0.48f,-0.60f, -0.48f, 0.36f, 0.80f, -0.60f, 0.80f, 0.00f);
                rotations[3] = new float3x3(0.00f,-0.80f, 0.60f,  0.80f, 0.36f, 0.48f, -0.60f, 0.48f, 0.64f);
                rotations[4] = new float3x3(0.64f, 0.48f,-0.60f,  0.48f, 0.36f,-0.80f, -0.60f,-0.80f, 0.00f);
                rotations[5] = new float3x3(0.00f, 0.80f,-0.60f, -0.80f, 0.36f, 0.48f,  0.60f, 0.48f, 0.64f);
                rotations[6] = new float3x3(0.64f,-0.48f, 0.60f, -0.48f, 0.36f,-0.80f,  0.60f,-0.80f, 0.00f);
                rotations[7] = new float3x3(0.00f,-0.80f,-0.60f,  0.80f, 0.36f,-0.48f,  0.60f,-0.48f, 0.64f);
            #endif

            numOctaves = clamp(numOctaves, 1, 8);

            for (int i = 0; i < numOctaves; i++)
            {
                // Transform coordinates for this octave
                float3 p = mul(x, rotations[i]) * pow(frequency, i);

                float4 noise = DeterministicNoise(p);

                float octaveAmplitude = pow(amplitude, i);
                value += octaveAmplitude * noise.x;

                // Transform derivatives back to original space
                float3 transformedDerivative = mul(noise.yzw, transpose(rotations[i])) * pow(frequency, i);
                derivative += octaveAmplitude * transformedDerivative;
            }

            return float4(value, derivative);
        }

        // Enhanced terrain function with integer-based hashing
        private static float4 terrain_ts(float3 pos, float frequency, int octaves)
        {
            // Apply frequency scaling
            float3 p = pos * frequency;

            // Get FBM noise and derivatives
            float4 noise = fbm_ts(p, octaves);
            float height = noise.x;
            float3 derivative = noise.yzw;

            // Scale derivatives properly: if we sample at p*frequency,
            // derivatives w.r.t. original pos are derivative*frequency
            derivative *= frequency;

            return float4(height, derivative);
        }

        private static float3 ClampPosition(float3 PositionIn, float4 TerrainBounds)
        {
            return float3(
                clamp(PositionIn.x, TerrainBounds.x, TerrainBounds.y),
                PositionIn.y,
                clamp(PositionIn.z, TerrainBounds.z, TerrainBounds.w));
        }

        #if !SHADER_TARGET
        private static float SampleBilinearClamp(NativeArray<byte> texture, int2 textureSize, float2 uv)
        {
            uv = uv * textureSize - 0.5f;
            int2 min = (int2)floor(uv);

            // A quick prayer for Burst to SIMD this. 🙏
            int4 index = clamp(min.y + int4(1, 1, 0, 0), 0, textureSize.y - 1) * textureSize.x +
                         clamp(min.x + int4(0, 1, 1, 0), 0, textureSize.x - 1);

            float2 t = frac(uv);
            float top = lerp(texture[index.w], texture[index.z], t.x);
            float bottom = lerp(texture[index.x], texture[index.y], t.x);
            return lerp(top, bottom, t.y) * (1f / 255f);
        }
        #endif

        #if !SHADER_TARGET
        private static float GetOccupancy(NativeArray<byte> occupancyMap, int2 occupancyMapSize, float3 PositionIn, RectInt bounds, int parcelSize)
        {
            float2 scale = 1f / ((float2(bounds.width, bounds.height) + 2f) * parcelSize);

            return SampleBilinearClamp(occupancyMap, occupancyMapSize, float2(
                (PositionIn.x - (bounds.x - 1) * parcelSize) * scale.x,
                (PositionIn.z - (bounds.y - 1) * parcelSize) * scale.y));
        }
        #endif

        // // In the "worst case", if occupancy is 0.25, it can mean that the current vertex is on a corner
        // // between one occupied parcel and three free ones, and height must be zero.
        // if (occupancy < 0.25f)
        // {
        //     float height = GetHeight(PositionIn.x, PositionIn.z);
        //     PositionIn.y = lerp(height, 0.0, occupancy * 4.0);
        //     Normal = GetNormal(PositionIn.x, PositionIn.z);
        // }
        // else
        // {
        //     PositionIn.y = 0.0;
        //     Normal = float3(0.0, 1.0, 0.0);
        // }

        // ============================================================================
        // CPU USAGE FUNCTIONS
        // ============================================================================

        // internal static float GetHeight(float x, float z)
        // {
        //     float frequencyCS = 0.21f;
        //     float terrainHeight = 3.0f;
        //
        //     float terrainData = getHeight_optimised_int(float2(x, z), frequencyCS);
        //     return terrainData * terrainHeight;
        // }
        //
        // internal static float3 GetNormal(float x, float z)
        // {
        //     float frequencyCS = 0.21f;
        //     return getNormal_optimised_int(float2(x, z), frequencyCS, 0);
        // }

        internal static float GetHeight(float x, float z)
        {
            float frequencyCS = 0.1f;
            int octaves = 8;
            float terrainHeight = 4.0f;

            float TERRAIN_MIN = -0.9960938f;
            float TERRAIN_MAX = 0.8251953f;
            float TERRAIN_RANGE = 1.8212891f; // Pre-calculated

            float4 terrainData = terrain( float3(x, 0.0f, z), frequencyCS, octaves);
            float height = (terrainData.x - TERRAIN_MIN) / TERRAIN_RANGE;
            height = saturate(height);
            return height * terrainHeight;
        }

        internal static float3 GetNormal(float x, float z)
        {
            float frequencyCS = 0.001f;
            int octaves = 8;
            return terrain( float3(x, 0.0f, z), frequencyCS, octaves).yzw;
        }

#if !SHADER_TARGET
    }
}
#endif

#endif
