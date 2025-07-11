#ifndef PERLIN_NOISE_INCLUDED
#define PERLIN_NOISE_INCLUDED

// Optimised hash function: 3 ALU
half hash_optimised(half2 p)
{
    return frac(sin(dot(p, half2(127.1h, 311.7h))) * 43758.5453h);
}

// Optimised 2D noise: ~25 ALU
half noise2d_optimised(half2 p)
{
    half2 i = floor(p);
    half2 f = frac(p);

    half a = hash_optimised(i);
    half b = hash_optimised(i + half2(1,0));
    half c = hash_optimised(i + half2(0,1));
    half d = hash_optimised(i + half2(1,1));

    half2 u = f * f * (3.0h - 2.0h * f); // smoothstep
    return lerp(lerp(a,b,u.x), lerp(c,d,u.x), u.y);
}

// Optimised terrain with 2 octaves: ~60 ALU total
half terrain_optimised(half2 pos, half frequency)
{
    half2 p = pos * frequency;
    half height = 0.0h;
    half amp = 0.5h;
    half freq = 1.0h;

    // Octave 1
    height += noise2d_optimised(p * freq) * amp;
    amp *= 0.5h; freq *= 2.0h;

    // Octave 2
    height += noise2d_optimised(p * freq) * amp;

    return height;
}

// Fast normal calculation using finite differences: ~190 ALU
half3 getNormal_optimised(half2 worldPos, half frequency, int quality)
{
    half eps = 0.1h / frequency; // Scale epsilon with frequency

    half h_center = terrain_optimised(worldPos, frequency);
    half h_right = terrain_optimised(worldPos + half2(eps, 0), frequency);
    half h_up = terrain_optimised(worldPos + half2(0, eps), frequency);

    half dhdx = (h_right - h_center) / eps;
    half dhdy = (h_up - h_center) / eps;

    return normalize(half3(-dhdx, 1.0h, -dhdy));
}

// Optimised version - 60 ALU
half getHeight_optimised(half2 worldPos, half frequency)
{
    return terrain_optimised(worldPos, frequency);
}

// With normals - varies by quality
half4 getHeightAndNormal(half2 worldPos, half frequency, int quality)
{
    return half4(getHeight_optimised(worldPos, frequency), getNormal_optimised(worldPos, frequency, 0));
}

// Float Version - Integer Hash Optimized Noise
// CPU/GPU shared code - all operations use float instead of half

// ============================================================================
// INTEGER-ONLY HASH FUNCTIONS
// ============================================================================

// Lowbias32 hash function: ~5 ALU
uint hash_int(uint x)
{
    x ^= x >> 16;
    x *= 0x7feb352du;
    x ^= x >> 15;
    x *= 0x846ca68bu;
    x ^= x >> 16;
    return x;
}

// 2D integer hash
uint hash_int2(int2 p)
{
    // Use large primes to avoid correlation
    uint h = (uint)p.x * 73856093u + (uint)p.y * 19349663u;
    return hash_int(h);
}

// Convert integer hash to float in [0,1] range: ~1 ALU
float hash_int_to_float(int2 p)
{
    uint h = hash_int2(p);
    return (float)h * (1.0f / 4294967296.0f); // Convert to [0,1]
}

// ============================================================================
// POSITION QUANTIZATION
// ============================================================================

// Quantize world position from metres to centimeters
int2 quantize_to_cm(float2 worldPos)
{
    return (int2)(worldPos * 100.0f); // Convert metres to centimeters
}

// ============================================================================
// INTEGER-BASED NOISE (Float version of optimized functions)
// ============================================================================

// Integer hash function: 5 ALU
float hash_optimised_int(float2 p)
{
    // Quantize position to centimeters
    int2 ipos = quantize_to_cm(p);

    // Use lowbias32 hash and convert to float
    return hash_int_to_float(ipos);
}

// Integer-based 2D noise: ~30 ALU
float noise2d_optimised_int(float2 p)
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
float terrain_optimised_int(float2 pos, float frequency)
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
float3 getNormal_optimised_int(float2 worldPos, float frequency, int quality)
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
float getHeight_optimised_int(float2 worldPos, float frequency)
{
    return terrain_optimised_int(worldPos, frequency);
}

// Integer-based height and normal: varies by quality (float version of getHeightAndNormal)
float4 getHeightAndNormal_int(float2 worldPos, float frequency, int quality)
{
    return float4(getHeight_optimised_int(worldPos, frequency), getNormal_optimised_int(worldPos, frequency, quality));
}

#endif // PERLIN_NOISE_INCLUDED
