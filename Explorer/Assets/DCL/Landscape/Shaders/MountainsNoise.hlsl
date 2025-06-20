// Copy-pasted from com.unity.mathematics/Unity.Mathematics/Noise/common.cs

// Modulo 289 without a division (only multiplications)
float3 mod289(float3 x)
{
    return x - floor(x * (1.0f / 289.0f)) * 289.0f;
}

float2 mod289(float2 x)
{
    return x - floor(x * (1.0f / 289.0f)) * 289.0f;
}

// Permutation polynomial: (34x^2 + x) math.mod 289
float3 permute(float3 x)
{
    return mod289((34.0f * x + 1.0f) * x);
}

// Copy-pasted from com.unity.mathematics/Unity.Mathematics/Noise/noise2D.cs

float snoise(float2 v)
{
    float4 C = float4(0.211324865405187f, // (3.0-math.sqrt(3.0))/6.0
        0.366025403784439f, // 0.5*(math.sqrt(3.0)-1.0)
        -0.577350269189626f, // -1.0 + 2.0 * C.x
        0.024390243902439f); // 1.0 / 41.0
    // First corner
    float2 i = floor(v + dot(v, C.yy));
    float2 x0 = v - i + dot(i, C.xx);

    // Other corners
    float2 i1;
    //i1.x = math.step( x0.y, x0.x ); // x0.x > x0.y ? 1.0 : 0.0
    //i1.y = 1.0 - i1.x;
    i1 = (x0.x > x0.y) ? float2(1.0f, 0.0f) : float2(0.0f, 1.0f);
    // x0 = x0 - 0.0 + 0.0 * C.xx ;
    // x1 = x0 - i1 + 1.0 * C.xx ;
    // x2 = x0 - 1.0 + 2.0 * C.xx ;
    float4 x12 = x0.xyxy + C.xxzz;
    x12.xy -= i1;

    // Permutations
    i = mod289(i); // Avoid truncation effects in permutation
    float3 p = permute(permute(i.y + float3(0.0f, i1.y, 1.0f)) + i.x + float3(0.0f, i1.x, 1.0f));

    float3 m = max(0.5f - float3(dot(x0, x0), dot(x12.xy, x12.xy), dot(x12.zw, x12.zw)), 0.0f);
    m = m * m;
    m = m * m;

    // Gradients: 41 points uniformly over a line, mapped onto a diamond.
    // The ring size 17*17 = 289 is close to a multiple of 41 (41*7 = 287)

    float3 x = 2.0f * frac(p * C.www) - 1.0f;
    float3 h = abs(x) - 0.5f;
    float3 ox = floor(x + 0.5f);
    float3 a0 = x - ox;

    // Normalise gradients implicitly by scaling m
    // Approximation of: m *= inversemath.sqrt( a0*a0 + h*h );
    m *= 1.79284291400159f - 0.85373472095314f * (a0 * a0 + h * h);

    // Compute final noise value at P

    float gx = a0.x * x0.x + h.x * x0.y;
    float2 gyz = a0.yz * x12.xz + h.yz * x12.yw;
    float3 g = float3(gx, gyz);

    return 130.0f * dot(m, g);
}

/// <summary>
/// lowbias32 updated with the latest best constants. See
/// https://nullprogram.com/blog/2018/07/31/ and
/// https://github.com/skeeto/hash-prospector/issues/19
/// </summary>
uint lowbias32(uint x)
{
    x ^= x >> 16;
    x *= 0x21f0aaad;
    x ^= x >> 15;
    x *= 0xd35a2d97;
    x ^= x >> 15;
    return x;
}

float GetHeight(float x, float z)
{
    float scale = 0.02f;
    float2 octave0 = float2(-99974.82f, -93748.33f);
    float2 octave1 = float2(-67502.3f, -22190.19f);
    float2 octave2 = float2(77881.34f, -61863.88f);
    float persistence = 0.338f;
    float lacunarity = 2.9f;
    float multiplyValue = 3.0f;

    float amplitude = 1.0f;
    float frequency = 1.0f;
    float noiseHeight = 0.0f;

    // Octave 0
    {
        float2 sample = (float2(x, z) + octave0) * scale * frequency;
        noiseHeight += snoise(sample) * amplitude;
        amplitude *= persistence;
        frequency *= lacunarity;
    }

    // Octave 1
    {
        float2 sample = (float2(x, z) + octave1) * scale * frequency;
        noiseHeight += snoise(sample) * amplitude;
        amplitude *= persistence;
        frequency *= lacunarity;
    }

    // Octave 2
    {
        float2 sample = (float2(x, z) + octave2) * scale * frequency;
        noiseHeight += snoise(sample) * amplitude;
        //amplitude *= Persistence;
        //frequency *= Lacunarity;
    }

    return noiseHeight * multiplyValue;
}

float3 GetNormal(float x, float z)
{
    return float3(0.0f, 1.0f, 0.0f);
}

void Noise_float(float3 PositionIn, float ParcelSize, float4 TerrainBounds, UnityTexture2D OccupancyMap,
    out float3 PositionOut, out float3 Normal)
{
    PositionOut.x = clamp(PositionIn.x, TerrainBounds.x, TerrainBounds.y);
    PositionOut.z = clamp(PositionIn.z, TerrainBounds.z, TerrainBounds.w);

    // The occupancy map has a 1 pixel border around the terrain.
    float2 scale = float2(1.0 / (TerrainBounds.y - TerrainBounds.x + ParcelSize * 2.0),
        1.0 / (TerrainBounds.w - TerrainBounds.z + ParcelSize * 2.0));

    float occupancy = SAMPLE_TEXTURE2D_LOD(OccupancyMap, OccupancyMap.samplerstate,
        (PositionOut.xz - TerrainBounds.xz + ParcelSize) * scale, 0.0).r;

    // In the "worst case", if occupancy is 0.25, it can mean that the current vertex is on a corner
    // between one occupied parcel and three free ones, and height must be zero.
    if (occupancy < 0.25)
    {
        float height = GetHeight(PositionOut.x, PositionOut.z);
        PositionOut.y = lerp(height, 0.0, occupancy * 4.0);
        Normal = GetNormal(PositionOut.x, PositionOut.z);
    }
    else
    {
        PositionOut.y = 0.0;
        Normal = float3(0.0, 1.0, 0.0);
    }
}
