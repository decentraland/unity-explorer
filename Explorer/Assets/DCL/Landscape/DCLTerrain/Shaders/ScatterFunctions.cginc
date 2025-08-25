﻿#ifndef SCATTER_FUNCTIONS_INCLUDED
#define SCATTER_FUNCTIONS_INCLUDED

inline uint hash_int(uint x)
{
    x ^= x >> 16;
    x *= 0x7feb352du;
    x ^= x >> 15;
    x *= 0x846ca68bu;
    x ^= x >> 16;
    return x;
}

// 2D integer hash
inline uint hash_int2(int2 p)
{
    // Use large primes to avoid correlation
    uint h = (uint)p.x * 73856093u + (uint)p.y * 19349663u;
    return hash_int(h);
}

// Convert integer hash to float in [0,1] range: ~1 ALU
inline half hash_int_to_float(int2 p)
{
    uint h = hash_int2(p);
    return (half)h * (1.0f / 4294967296.0f); // Convert to [0,1]
}

inline half PackTwoBytesIntoHalf(uint value1, uint value2)
{
    // Ensure values are in 8-bit range
    value1 = value1 & 0xFF;
    value2 = value2 & 0xFF;

    // Pack into 16-bit uint, then reinterpret as half
    const uint packed = (value1 << 8) | value2;
    return asfloat(packed);
}

inline void UnpackTwoBytes(half packedValue, out uint value1, out uint value2)
{
    // Reinterpret half as 16-bit uint
    const uint packed = asuint(packedValue);

    // Extract the two bytes
    value1 = (packed >> 8) & 0xFF;
    value2 = packed & 0xFF;
}

// Create quaternion from Y-axis rotation
float4 QuaternionFromYRotation(float angleRadians)
{
    float halfAngle = angleRadians * 0.5;
    return float4(0, sin(halfAngle), 0, cos(halfAngle));
}

// Calculate quaternion from terrain normal
inline float4 QuaternionFromToRotation(float3 from, float3 to)
{
    float3 cross_vec = cross(from, to);
    float dot_product = dot(from, to);
    float s = sqrt((1.0 + dot_product) * 2.0);
    float3 xyz = cross_vec / s;
    float w = s * 0.5;
    return float4(xyz, w);
}

// Multiply two quaternions (q1 * q2)
float4 QuaternionMultiply(float4 q1, float4 q2)
{
    return float4(
        q1.w * q2.x + q1.x * q2.w + q1.y * q2.z - q1.z * q2.y,
        q1.w * q2.y - q1.x * q2.z + q1.y * q2.w + q1.z * q2.x,
        q1.w * q2.z + q1.x * q2.y - q1.y * q2.x + q1.z * q2.w,
        q1.w * q2.w - q1.x * q2.x - q1.y * q2.y - q1.z * q2.z
    );
}

inline float GetOccupancy(float2 UV_Coord, float4 TerrainBounds, int ParcelSize, in Texture2D _occupancyTexture, in SamplerState _occupancySampler)
{
    return _occupancyTexture.SampleLevel(_occupancySampler, UV_Coord, 0.0).r;
}

float CalculateHeightFromHeightmap(float2 uv, float terrainHeight, float fOccupancy, in Texture2D _heightMapTexture, in SamplerState _heightMapSampler)
{
    float _Heightmap_TexelSize = 1.0f / 8192.0f;

    // Sample the height at neighboring pixels
    float height00 = _heightMapTexture.SampleLevel(_heightMapSampler, uv + float2(0, 0), 0).r;
    float height10 = _heightMapTexture.SampleLevel(_heightMapSampler, uv + float2(_Heightmap_TexelSize, 0), 0).r;
    float height01 = _heightMapTexture.SampleLevel(_heightMapSampler, uv + float2(0, _Heightmap_TexelSize), 0).r;
    float height11 = _heightMapTexture.SampleLevel(_heightMapSampler, uv + float2(_Heightmap_TexelSize, _Heightmap_TexelSize), 0).r;

    float height = (height00 + height10 + height01 + height11) * 0.25f;

    float minValue = 175.f/255.f;
    float normalizedHeight = (fOccupancy - minValue) / (1 - minValue);
    height = normalizedHeight * terrainHeight; //= lerp(0.0, height * terrainHeight, fOccupancy * 4.0);
    height = max(0.0f, height);
    return height;
}

float3 CalculateNormalFromHeightmap(float2 uv, float terrainHeight, float fOccupancy, in Texture2D _heightMapTexture, in SamplerState _heightMapSampler)
{
    float _Heightmap_TexelSize = 1.0f / 8192.0f;

    // Sample the height at neighboring pixels
    float height00 = _heightMapTexture.SampleLevel(_heightMapSampler, uv + float2(0, 0), 0).r;
    float height10 = _heightMapTexture.SampleLevel(_heightMapSampler, uv + float2(_Heightmap_TexelSize, 0), 0).r;
    float height01 = _heightMapTexture.SampleLevel(_heightMapSampler, uv + float2(0, _Heightmap_TexelSize), 0).r;
    float height11 = _heightMapTexture.SampleLevel(_heightMapSampler, uv + float2(_Heightmap_TexelSize, _Heightmap_TexelSize), 0).r;

    // Calculate the gradient in world space (Y is up in Unity)
    // Since each vertex is 1 meter apart, the horizontal distance is 2.0 (left to right)
    float3 va = float3(2.0, lerp((height10 - height01) * terrainHeight, 0.0f, fOccupancy * 4.0), 0.0); // X direction
    float3 vb = float3(0.0, lerp((height00 - height11) * terrainHeight, 0.0f, fOccupancy * 4.0), 2.0); // Z direction
    // Cross product to get the normal
    return normalize(cross(vb, va));
}

// Create combined quaternion: Y rotation then terrain alignment
float4 CreateGrassRotationQuaternion(float3 terrainNormal, float yRotationAngle)
{
    float4 yRotation = QuaternionFromYRotation(yRotationAngle);
    float3 up = float3(0, 1, 0);
    float4 terrainAlignment = QuaternionFromToRotation(up, terrainNormal);

    // Apply Y rotation first, then terrain alignment
    return QuaternionMultiply(terrainAlignment, yRotation);
}

half4 SplatmapMix(float2 uv, in Texture2D _terrainBlendTexture, in SamplerState _terrainBlendSampler,
    in Texture2D _groundDetailTexture, in SamplerState _groundDetailSampler,
    in Texture2D _sandDetailTexture, in SamplerState _sandDetailSampler,
    uint mipLevel = 0)
{
    half4 splatControl = half4(_terrainBlendTexture.SampleLevel(_terrainBlendSampler, uv, mipLevel).rgb, 1.0f);
    half4 diffAlbedo[4];

    diffAlbedo[0] = float4(_groundDetailTexture.SampleLevel(_groundDetailSampler, uv, 0).rgb, 1.0f);
    diffAlbedo[1] = float4(_sandDetailTexture.SampleLevel(_sandDetailSampler, uv, 0).rgb, 1.0f);
    diffAlbedo[2] = 1.0f;
    diffAlbedo[3] = 1.0f;

    // This might be a bit of a gamble -- the assumption here is that if the diffuseMap has no
    // alpha channel, then diffAlbedo[n].a = 1.0 (and _DiffuseHasAlphaN = 0.0)
    // Prior to coming in, _SmoothnessN is actually set to max(_DiffuseHasAlphaN, _SmoothnessN)
    // This means that if we have an alpha channel, _SmoothnessN is locked to 1.0 and
    // otherwise, the true slider value is passed down and diffAlbedo[n].a == 1.0.
    float _Smoothness0 = 0.0f;
    float _Smoothness1 = 0.15f;
    float _Smoothness2 = 0.0f;
    float _Smoothness3 = 0.0f;
    half4 defaultSmoothness = half4(diffAlbedo[0].a, diffAlbedo[1].a, diffAlbedo[2].a, diffAlbedo[3].a);
    defaultSmoothness *= half4(_Smoothness0, _Smoothness1, _Smoothness2, _Smoothness3);

    float4 _DiffuseRemapScale0 = float4(1.0f, 1.0f, 1.0f, 1.0f);
    float4 _DiffuseRemapScale1 = float4(1.0f, 1.0f, 1.0f, 1.0f);
    float4 _DiffuseRemapScale2 = float4(1.0f, 1.0f, 1.0f, 1.0f);
    float4 _DiffuseRemapScale3 = float4(1.0f, 1.0f, 1.0f, 1.0f);

    int _NumLayersCount = 2;
    if(_NumLayersCount <= 4)
    {
        // 20.0 is the number of steps in inputAlphaMask (Density mask. We decided 20 empirically)
        half4 opacityAsDensity = saturate((half4(diffAlbedo[0].a, diffAlbedo[1].a, diffAlbedo[2].a, diffAlbedo[3].a) - (1 - splatControl)) * 20.0);
        opacityAsDensity += 0.001h * splatControl;      // if all weights are zero, default to what the blend mask says
        half4 useOpacityAsDensityParam = { _DiffuseRemapScale0.w, _DiffuseRemapScale1.w, _DiffuseRemapScale2.w, _DiffuseRemapScale3.w }; // 1 is off
        splatControl = lerp(opacityAsDensity, splatControl, useOpacityAsDensityParam);
    }

    half4 mixedDiffuse = 0.0h;
    mixedDiffuse += diffAlbedo[0] * half4(_DiffuseRemapScale0.rgb * splatControl.rrr, 1.0h);
    mixedDiffuse += diffAlbedo[1] * half4(_DiffuseRemapScale1.rgb * splatControl.ggg, 1.0h);

    return mixedDiffuse;
}

#endif
