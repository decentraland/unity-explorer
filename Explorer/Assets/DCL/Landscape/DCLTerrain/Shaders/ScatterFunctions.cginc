#ifndef SCATTER_FUNCTIONS_INCLUDED
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

float CalculateHeightFromHeightmap(float2 uv, float _DistanceFieldScale,
    in Texture2D _heightMapTexture, in SamplerState _heightMapSampler,
    in Texture2D _occupancyTexture, in SamplerState _occupancySampler)
{
    float _Heightmap_TexelSize = 1.0f / 8192.0f;

    // Sample the height at neighboring pixels
    float fHeight00 = _heightMapTexture.SampleLevel(_heightMapSampler, uv + float2(0, 0), 0).r;
    float fHeight10 = _heightMapTexture.SampleLevel(_heightMapSampler, uv + float2(_Heightmap_TexelSize, 0), 0).r;
    float fHeight01 = _heightMapTexture.SampleLevel(_heightMapSampler, uv + float2(0, _Heightmap_TexelSize), 0).r;
    float fHeight11 = _heightMapTexture.SampleLevel(_heightMapSampler, uv + float2(_Heightmap_TexelSize, _Heightmap_TexelSize), 0).r;

    float fHeight = (fHeight00 + fHeight10 + fHeight01 + fHeight11) * 0.25f;
    
    float fOccupancy00 = _occupancyTexture.SampleLevel(_occupancySampler, uv + float2(0, 0), 0).r;
    float fOccupancy10 = _occupancyTexture.SampleLevel(_occupancySampler, uv + float2(_Heightmap_TexelSize, 0), 0).r;
    float fOccupancy01 = _occupancyTexture.SampleLevel(_occupancySampler, uv + float2(0, _Heightmap_TexelSize), 0).r;
    float fOccupancy11 = _occupancyTexture.SampleLevel(_occupancySampler, uv + float2(_Heightmap_TexelSize, _Heightmap_TexelSize), 0).r;
    
    float fOccupancy = (fOccupancy00 + fOccupancy10 + fOccupancy01 + fOccupancy11) * 0.25f;
    float minValue = 155.0f / 255.0f;

    if (fOccupancy <= minValue)
    {
        // Flat surface (occupied parcels and above minValue threshold)
        return  0.0f;
    }
    
    float normalizedHeight = (fOccupancy - minValue) / (1.0f - minValue);

    float min = -4.135159f; // min value of the GeoffNoise.GetHeight
    float range = 8.236154f; // (max - min) of the GeoffNoise.GetHeight
            
    float fHeightNoise = fHeight * range + min;

    float saturationFactor = 20;
    return max(0.0f, normalizedHeight * _DistanceFieldScale + fHeightNoise * saturate( normalizedHeight * saturationFactor));
}

// float3 CalculateNormalFromHeightmap(float2 uv, float terrainHeight, float fOccupancy, in Texture2D _heightMapTexture, in SamplerState _heightMapSampler)
// {
//     float _Heightmap_TexelSize = 1.0f / 8192.0f;
//
//     // Sample the height at neighboring pixels
//     float height00 = _heightMapTexture.SampleLevel(_heightMapSampler, uv + float2(0, 0), 0).r;
//     float height10 = _heightMapTexture.SampleLevel(_heightMapSampler, uv + float2(_Heightmap_TexelSize, 0), 0).r;
//     float height01 = _heightMapTexture.SampleLevel(_heightMapSampler, uv + float2(0, _Heightmap_TexelSize), 0).r;
//     float height11 = _heightMapTexture.SampleLevel(_heightMapSampler, uv + float2(_Heightmap_TexelSize, _Heightmap_TexelSize), 0).r;
//
//     // Calculate the gradient in world space (Y is up in Unity)
//     // Since each vertex is 1 meter apart, the horizontal distance is 2.0 (left to right)
//     float3 va = float3(2.0, lerp((height10 - height01) * terrainHeight, 0.0f, fOccupancy * 4.0), 0.0); // X direction
//     float3 vb = float3(0.0, lerp((height00 - height11) * terrainHeight, 0.0f, fOccupancy * 4.0), 2.0); // Z direction
//     // Cross product to get the normal
//     return normalize(cross(vb, va));
// }

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

float3 CalculateNormalFromHeightmap(float2 uv, float _DistanceFieldScale,
    in Texture2D _heightMapTexture, in SamplerState _heightMapSampler,
    in Texture2D _occupancyTexture, in SamplerState _occupancySampler)
{
    float _Heightmap_TexelSize = 1.0f / 8192.0f;
    
    // Sampling Grid
    // 0 | 1 | 2
    // 3 | 4 | 5
    // 6 | 7 | 8
    
    float2 offset0 = float2(-1.0f, -1.0f);
    float2 offset1 = float2(0.0f, -1.0f);
    float2 offset2 = float2(1.0f, -1.0f);
    float2 offset3 = float2(-1.0f, 0.0f);
    float2 offset4 = float2(0.0f, 0.0f);
    float2 offset5 = float2(1.0f, 0.0f);
    float2 offset6 = float2(-1.0f, 1.0f);
    float2 offset7 = float2(0.0f, 1.0f);
    float2 offset8 = float2(1.0f, 1.0f);

    // Sample the height at neighboring pixels
    float height0 = _heightMapTexture.SampleLevel(_heightMapSampler, uv + (offset0 * _Heightmap_TexelSize), 0).r;
    float height1 = _heightMapTexture.SampleLevel(_heightMapSampler, uv + (offset1 * _Heightmap_TexelSize), 0).r;
    float height2 = _heightMapTexture.SampleLevel(_heightMapSampler, uv + (offset2 * _Heightmap_TexelSize), 0).r;
    float height3 = _heightMapTexture.SampleLevel(_heightMapSampler, uv + (offset3 * _Heightmap_TexelSize), 0).r;
    float height4 = _heightMapTexture.SampleLevel(_heightMapSampler, uv + (offset4 * _Heightmap_TexelSize), 0).r;
    float height5 = _heightMapTexture.SampleLevel(_heightMapSampler, uv + (offset5 * _Heightmap_TexelSize), 0).r;
    float height6 = _heightMapTexture.SampleLevel(_heightMapSampler, uv + (offset6 * _Heightmap_TexelSize), 0).r;
    float height7 = _heightMapTexture.SampleLevel(_heightMapSampler, uv + (offset7 * _Heightmap_TexelSize), 0).r;
    float height8 = _heightMapTexture.SampleLevel(_heightMapSampler, uv + (offset8 * _Heightmap_TexelSize), 0).r;

    /// Value taken from generating HeightMap via TerrainGeneratorWithAnalysis. 
    float min = -4.135159f; // min value of the GeoffNoise.GetHeight
    float range = 8.236154f; // (max - min) of the GeoffNoise.GetHeight
    
    height0 = height0 * range + min;
    height1 = height1 * range + min;
    height2 = height2 * range + min;
    height3 = height3 * range + min;
    height4 = height4 * range + min;
    height5 = height5 * range + min;
    height6 = height6 * range + min;
    height7 = height7 * range + min;
    height8 = height8 * range + min;
    
    // Sample occupancy
    float fOccupancy0 = _occupancyTexture.SampleLevel(_occupancySampler, uv + (offset0 * _Heightmap_TexelSize), 0).r;
    float fOccupancy1 = _occupancyTexture.SampleLevel(_occupancySampler, uv + (offset1 * _Heightmap_TexelSize), 0).r;
    float fOccupancy2 = _occupancyTexture.SampleLevel(_occupancySampler, uv + (offset2 * _Heightmap_TexelSize), 0).r;
    float fOccupancy3 = _occupancyTexture.SampleLevel(_occupancySampler, uv + (offset3 * _Heightmap_TexelSize), 0).r;
    float fOccupancy4 = _occupancyTexture.SampleLevel(_occupancySampler, uv + (offset4 * _Heightmap_TexelSize), 0).r;
    float fOccupancy5 = _occupancyTexture.SampleLevel(_occupancySampler, uv + (offset5 * _Heightmap_TexelSize), 0).r;
    float fOccupancy6 = _occupancyTexture.SampleLevel(_occupancySampler, uv + (offset6 * _Heightmap_TexelSize), 0).r;
    float fOccupancy7 = _occupancyTexture.SampleLevel(_occupancySampler, uv + (offset7 * _Heightmap_TexelSize), 0).r;
    float fOccupancy8 = _occupancyTexture.SampleLevel(_occupancySampler, uv + (offset8 * _Heightmap_TexelSize), 0).r;

    float minValue = 175.0 / 255.0;

    fOccupancy0 = (fOccupancy0 <= minValue) ? 0.0f : (fOccupancy0 - minValue) / (1.0f - minValue);
    fOccupancy1 = (fOccupancy1 <= minValue) ? 0.0f : (fOccupancy1 - minValue) / (1.0f - minValue);
    fOccupancy2 = (fOccupancy2 <= minValue) ? 0.0f : (fOccupancy2 - minValue) / (1.0f - minValue);
    fOccupancy3 = (fOccupancy3 <= minValue) ? 0.0f : (fOccupancy3 - minValue) / (1.0f - minValue);
    fOccupancy4 = (fOccupancy4 <= minValue) ? 0.0f : (fOccupancy4 - minValue) / (1.0f - minValue);
    fOccupancy5 = (fOccupancy5 <= minValue) ? 0.0f : (fOccupancy5 - minValue) / (1.0f - minValue);
    fOccupancy6 = (fOccupancy6 <= minValue) ? 0.0f : (fOccupancy6 - minValue) / (1.0f - minValue);
    fOccupancy7 = (fOccupancy7 <= minValue) ? 0.0f : (fOccupancy7 - minValue) / (1.0f - minValue);
    fOccupancy8 = (fOccupancy8 <= minValue) ? 0.0f : (fOccupancy8 - minValue) / (1.0f - minValue);

    // Calculate occupied heights
    float saturationFactor = 20;
    float fOccupiedHeight0 = fOccupancy0 * _DistanceFieldScale + height0 * saturate( fOccupancy0 * saturationFactor);
    float fOccupiedHeight1 = fOccupancy1 * _DistanceFieldScale + height1 * saturate( fOccupancy1 * saturationFactor);
    float fOccupiedHeight2 = fOccupancy2 * _DistanceFieldScale + height2 * saturate( fOccupancy2 * saturationFactor);
    float fOccupiedHeight3 = fOccupancy3 * _DistanceFieldScale + height3 * saturate( fOccupancy3 * saturationFactor);
    float fOccupiedHeight4 = fOccupancy4 * _DistanceFieldScale + height4 * saturate( fOccupancy4 * saturationFactor);
    float fOccupiedHeight5 = fOccupancy5 * _DistanceFieldScale + height5 * saturate( fOccupancy5 * saturationFactor);
    float fOccupiedHeight6 = fOccupancy6 * _DistanceFieldScale + height6 * saturate( fOccupancy6 * saturationFactor);
    float fOccupiedHeight7 = fOccupancy7 * _DistanceFieldScale + height7 * saturate( fOccupancy7 * saturationFactor);
    float fOccupiedHeight8 = fOccupancy8 * _DistanceFieldScale + height8 * saturate( fOccupancy8 * saturationFactor);

    // Define world space positions of each vertex relative to center
    // Assuming each vertex is 1 meter apart in world space
    float3 centerPos = float3(0.0f, fOccupiedHeight4, 0.0f);
    
    // Positions of the 8 surrounding vertices in Unity world space (Y up, Z forward, X right)
    float3 pos0 = float3(-1.0f, fOccupiedHeight0, -1.0f); // Top-left (back-left)
    float3 pos1 = float3( 0.0f, fOccupiedHeight1, -1.0f); // Top-center (back-center)
    float3 pos2 = float3( 1.0f, fOccupiedHeight2, -1.0f); // Top-right (back-right)
    float3 pos3 = float3(-1.0f, fOccupiedHeight3,  0.0f); // Middle-left
    float3 pos5 = float3( 1.0f, fOccupiedHeight5,  0.0f); // Middle-right
    float3 pos6 = float3(-1.0f, fOccupiedHeight6,  1.0f); // Bottom-left (forward-left)
    float3 pos7 = float3( 0.0f, fOccupiedHeight7,  1.0f); // Bottom-center (forward-center)
    float3 pos8 = float3( 1.0f, fOccupiedHeight8,  1.0f); // Bottom-right (forward-right)

    // Calculate vectors from each surrounding vertex TO the center vertex
    float3 vec0 = centerPos - pos0;
    float3 vec1 = centerPos - pos1;
    float3 vec2 = centerPos - pos2;
    float3 vec3 = centerPos - pos3;
    float3 vec5 = centerPos - pos5;
    float3 vec6 = centerPos - pos6;
    float3 vec7 = centerPos - pos7;
    float3 vec8 = centerPos - pos8;

    // For each pair of adjacent vectors, calculate the normal using cross product
    // We'll calculate normals for triangular faces formed by center + two adjacent surrounding points

    // Check for flat terrain (all heights very similar)
    float heightVariance = 0.0f;
    float avgHeight = (fOccupiedHeight0 + fOccupiedHeight1 + fOccupiedHeight2 + fOccupiedHeight3 + 
                      fOccupiedHeight4 + fOccupiedHeight5 + fOccupiedHeight6 + fOccupiedHeight7 + fOccupiedHeight8) / 9.0f;

    // Calculate variance to detect flat areas
    heightVariance += abs(fOccupiedHeight0 - avgHeight);
    heightVariance += abs(fOccupiedHeight1 - avgHeight);
    heightVariance += abs(fOccupiedHeight2 - avgHeight);
    heightVariance += abs(fOccupiedHeight3 - avgHeight);
    heightVariance += abs(fOccupiedHeight4 - avgHeight);
    heightVariance += abs(fOccupiedHeight5 - avgHeight);
    heightVariance += abs(fOccupiedHeight6 - avgHeight);
    heightVariance += abs(fOccupiedHeight7 - avgHeight);
    heightVariance += abs(fOccupiedHeight8 - avgHeight);
    heightVariance /= 9.0f;
    
    // If terrain is essentially flat, return up vector
    float flatThreshold = 0.01f; // Adjust this value based on your terrain scale
    if (heightVariance < flatThreshold)
    {
        return float3(0.0f, 1.0f, 0.0f); // Up vector in Unity
    }

    // Calculate face normals (using right-hand rule for consistent winding)
    float3 normal0 = cross(vec1, vec0); // Face: center-1-0
    float3 normal1 = cross(vec2, vec1); // Face: center-2-1
    float3 normal2 = cross(vec5, vec2); // Face: center-5-2
    float3 normal3 = cross(vec8, vec5); // Face: center-8-5
    float3 normal4 = cross(vec7, vec8); // Face: center-7-8
    float3 normal5 = cross(vec6, vec7); // Face: center-6-7
    float3 normal6 = cross(vec3, vec6); // Face: center-3-6
    float3 normal7 = cross(vec0, vec3); // Face: center-0-3

    // Check if any normal is invalid (zero length) and skip it
    float3 validNormals[8];
    int validCount = 0;
    
    float3 normals[8] = {normal0, normal1, normal2, normal3, normal4, normal5, normal6, normal7};
    
    for (int i = 0; i < 8; i++)
    {
        float fLength = length(normals[i]);
        if (fLength > 0.001f) // Only use normals that have sufficient length
        {
            validNormals[validCount] = normals[i] / fLength; // Normalize
            validCount++;
        }
    }
    
    // If we don't have enough valid normals, return up vector
    if (validCount < 3)
    {
        return float3(0.0f, 1.0f, 0.0f); // Up vector in Unity
    }

    // Average only the valid normals
    float3 averageNormal = float3(0, 0, 0);
    for (int j = 0; j < validCount; j++)
    {
        averageNormal += validNormals[j];
    }
    averageNormal /= (float)validCount;
    return averageNormal;
    
    // float3 worldTangent = float3(1.0f, 0.0f, 0.0f);
    //
    // // Remove the component of worldTangent that's parallel to the normal
    // // This gives us a tangent that's perpendicular to the normal
    // tangentWS = normalize(worldTangent - dot(worldTangent, normalWS) * normalWS);
    //
    // // Calculate bitangent using cross product (ensures orthogonality)
    // bitangentWS = normalize(cross(normalWS, tangentWS));
    //
    // // // Ensure right-handed coordinate system
    // if (dot(cross(tangentWS, bitangentWS), normalWS) < 0.0f)
    // {
    //     bitangentWS = -bitangentWS;
    // }
}

#endif
