#ifndef NOISE_INCLUDED
#define NOISE_INCLUDED

// Hash function for pseudorandom values - improved version from Quilez
float hash1(float n)
{
    return frac(n * 17.0f * frac(n * 0.3183099f));
}

float hash1(float3 p)
{
    float n = p.x + 317.0f * p.y + 157.0f * p.z;
    return hash1(n);
}

// Returns 4D vector: (noise_value, derivative_x, derivative_y, derivative_z)
// This matches Quilez's exact implementation, converted to HLSL
float4 noised(float3 x)
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
float4 fbm(float3 x, int numOctaves)
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
float4 terrain(float3 pos, float frequency, int octaves)
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

#endif
