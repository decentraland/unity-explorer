#ifndef UNIVERSAL_SIMPLE_LIT_PASS_INCLUDED
#define UNIVERSAL_SIMPLE_LIT_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "MountainLit_VertexFunctions.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

struct Attributes
{
    float4 positionOS    : POSITION;
    float3 normalOS      : NORMAL;
    float4 tangentOS     : TANGENT;
    float2 texcoord      : TEXCOORD0;
    float2 staticLightmapUV    : TEXCOORD1;
    float2 dynamicLightmapUV    : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv                       : TEXCOORD0;

    float3 positionWS                  : TEXCOORD1;    // xyz: posWS

    #ifdef _NORMALMAP
        half4 normalWS                 : TEXCOORD2;    // xyz: normal, w: viewDir.x
        half4 tangentWS                : TEXCOORD3;    // xyz: tangent, w: viewDir.y
        half4 bitangentWS              : TEXCOORD4;    // xyz: bitangent, w: viewDir.z
    #else
        half3  normalWS                : TEXCOORD2;
    #endif

    #ifdef _ADDITIONAL_LIGHTS_VERTEX
        half4 fogFactorAndVertexLight  : TEXCOORD5; // x: fogFactor, yzw: vertex light
    #else
        half  fogFactor                 : TEXCOORD5;
    #endif

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        float4 shadowCoord             : TEXCOORD6;
    #endif

    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 7);

#ifdef DYNAMICLIGHTMAP_ON
    float2  dynamicLightmapUV : TEXCOORD8; // Dynamic lightmap UVs
#endif

#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion : TEXCOORD9;
#endif
    
    float4 positionCS                  : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;

    inputData.positionWS = input.positionWS;
#if defined(DEBUG_DISPLAY)
    inputData.positionCS = input.positionCS;
#endif

    #ifdef _NORMALMAP
        half3 viewDirWS = half3(input.normalWS.w, input.tangentWS.w, input.bitangentWS.w);
        inputData.tangentToWorld = half3x3(input.tangentWS.xyz, input.bitangentWS.xyz, input.normalWS.xyz);
        inputData.normalWS = TransformTangentToWorld(normalTS, inputData.tangentToWorld);
    #else
        half3 viewDirWS = GetWorldSpaceNormalizeViewDir(inputData.positionWS);
        inputData.normalWS = input.normalWS;
    #endif

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    viewDirWS = SafeNormalize(viewDirWS);

    inputData.viewDirectionWS = viewDirWS;

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        inputData.shadowCoord = input.shadowCoord;
    #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
        inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
    #else
        inputData.shadowCoord = float4(0, 0, 0, 0);
    #endif

    #ifdef _ADDITIONAL_LIGHTS_VERTEX
        inputData.fogCoord = InitializeInputDataFog(float4(inputData.positionWS, 1.0), input.fogFactorAndVertexLight.x);
        inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
    #else
        inputData.fogCoord = InitializeInputDataFog(float4(inputData.positionWS, 1.0), input.fogFactor);
        inputData.vertexLighting = half3(0, 0, 0);
    #endif

    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

    #if defined(DEBUG_DISPLAY)
    #if defined(DYNAMICLIGHTMAP_ON)
    inputData.dynamicLightmapUV = input.dynamicLightmapUV.xy;
    #endif
    #if defined(LIGHTMAP_ON)
    inputData.staticLightmapUV = input.staticLightmapUV;
    #else
    inputData.vertexSH = input.vertexSH;
    #endif
    #if defined(USE_APV_PROBE_OCCLUSION)
    inputData.probeOcclusion = input.probeOcclusion;
    #endif
    #endif
}

void InitializeBakedGIData(Varyings input, inout InputData inputData)
{
#if defined(DYNAMICLIGHTMAP_ON)
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    inputData.bakedGI = SAMPLE_GI(input.vertexSH,
        GetAbsolutePositionWS(inputData.positionWS),
        inputData.normalWS,
        inputData.viewDirectionWS,
        input.positionCS.xy,
        input.probeOcclusion,
        inputData.shadowMask);
#else
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#endif
}

void CalculateNormalFromHeightmap(float2 uv, out float3 normalWS, out float3 tangentWS, out float3 bitangentWS)
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
    float height0 = SAMPLE_TEXTURE2D_LOD(_HeightMap, my_point_clamp_sampler, uv + (offset0 * _Heightmap_TexelSize), 0).r;
    float height1 = SAMPLE_TEXTURE2D_LOD(_HeightMap, my_point_clamp_sampler, uv + (offset1 * _Heightmap_TexelSize), 0).r;
    float height2 = SAMPLE_TEXTURE2D_LOD(_HeightMap, my_point_clamp_sampler, uv + (offset2 * _Heightmap_TexelSize), 0).r;
    float height3 = SAMPLE_TEXTURE2D_LOD(_HeightMap, my_point_clamp_sampler, uv + (offset3 * _Heightmap_TexelSize), 0).r;
    float height4 = SAMPLE_TEXTURE2D_LOD(_HeightMap, my_point_clamp_sampler, uv + (offset4 * _Heightmap_TexelSize), 0).r;
    float height5 = SAMPLE_TEXTURE2D_LOD(_HeightMap, my_point_clamp_sampler, uv + (offset5 * _Heightmap_TexelSize), 0).r;
    float height6 = SAMPLE_TEXTURE2D_LOD(_HeightMap, my_point_clamp_sampler, uv + (offset6 * _Heightmap_TexelSize), 0).r;
    float height7 = SAMPLE_TEXTURE2D_LOD(_HeightMap, my_point_clamp_sampler, uv + (offset7 * _Heightmap_TexelSize), 0).r;
    float height8 = SAMPLE_TEXTURE2D_LOD(_HeightMap, my_point_clamp_sampler, uv + (offset8 * _Heightmap_TexelSize), 0).r;

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
    float fOccupancy0 = SAMPLE_TEXTURE2D_LOD(_OccupancyMap, sampler_OccupancyMap, uv + (offset0 * _Heightmap_TexelSize), 0).r;
    float fOccupancy1 = SAMPLE_TEXTURE2D_LOD(_OccupancyMap, sampler_OccupancyMap, uv + (offset1 * _Heightmap_TexelSize), 0).r;
    float fOccupancy2 = SAMPLE_TEXTURE2D_LOD(_OccupancyMap, sampler_OccupancyMap, uv + (offset2 * _Heightmap_TexelSize), 0).r;
    float fOccupancy3 = SAMPLE_TEXTURE2D_LOD(_OccupancyMap, sampler_OccupancyMap, uv + (offset3 * _Heightmap_TexelSize), 0).r;
    float fOccupancy4 = SAMPLE_TEXTURE2D_LOD(_OccupancyMap, sampler_OccupancyMap, uv + (offset4 * _Heightmap_TexelSize), 0).r;
    float fOccupancy5 = SAMPLE_TEXTURE2D_LOD(_OccupancyMap, sampler_OccupancyMap, uv + (offset5 * _Heightmap_TexelSize), 0).r;
    float fOccupancy6 = SAMPLE_TEXTURE2D_LOD(_OccupancyMap, sampler_OccupancyMap, uv + (offset6 * _Heightmap_TexelSize), 0).r;
    float fOccupancy7 = SAMPLE_TEXTURE2D_LOD(_OccupancyMap, sampler_OccupancyMap, uv + (offset7 * _Heightmap_TexelSize), 0).r;
    float fOccupancy8 = SAMPLE_TEXTURE2D_LOD(_OccupancyMap, sampler_OccupancyMap, uv + (offset8 * _Heightmap_TexelSize), 0).r;

    float minValue = 175.0 / 255.0;

    fOccupancy0 = (fOccupancy0 <= minValue) ? 0.0f : (fOccupancy0 - minValue) / (1 - minValue);
    fOccupancy1 = (fOccupancy1 <= minValue) ? 0.0f : (fOccupancy1 - minValue) / (1 - minValue);
    fOccupancy2 = (fOccupancy2 <= minValue) ? 0.0f : (fOccupancy2 - minValue) / (1 - minValue);
    fOccupancy3 = (fOccupancy3 <= minValue) ? 0.0f : (fOccupancy3 - minValue) / (1 - minValue);
    fOccupancy4 = (fOccupancy4 <= minValue) ? 0.0f : (fOccupancy4 - minValue) / (1 - minValue);
    fOccupancy5 = (fOccupancy5 <= minValue) ? 0.0f : (fOccupancy5 - minValue) / (1 - minValue);
    fOccupancy6 = (fOccupancy6 <= minValue) ? 0.0f : (fOccupancy6 - minValue) / (1 - minValue);
    fOccupancy7 = (fOccupancy7 <= minValue) ? 0.0f : (fOccupancy7 - minValue) / (1 - minValue);
    fOccupancy8 = (fOccupancy8 <= minValue) ? 0.0f : (fOccupancy8 - minValue) / (1 - minValue);

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
        normalWS = float3(0.0f, 1.0f, 0.0f); // Up vector in Unity
        return;
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
        normalWS = float3(0.0f, 1.0f, 0.0f); // Up vector in Unity
        return;
    }

    // Average only the valid normals
    float3 averageNormal = float3(0, 0, 0);
    for (int j = 0; j < validCount; j++)
    {
        averageNormal += validNormals[j];
    }
    averageNormal /= (float)validCount;
    normalWS = averageNormal;
    
    float3 worldTangent = float3(1.0f, 0.0f, 0.0f);
    
    // Remove the component of worldTangent that's parallel to the normal
    // This gives us a tangent that's perpendicular to the normal
    tangentWS = normalize(worldTangent - dot(worldTangent, normalWS) * normalWS);
    
    // Calculate bitangent using cross product (ensures orthogonality)
    bitangentWS = normalize(cross(normalWS, tangentWS));
    
    // // Ensure right-handed coordinate system
    if (dot(cross(tangentWS, bitangentWS), normalWS) < 0.0f)
    {
        bitangentWS = -bitangentWS;
    }
}

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

// Used in Standard (Simple Lighting) shader
Varyings LitPassVertexSimple(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    float occupancy = 0.0f;
    VertexPositionInputs vertexInput = GetVertexPositionInputs_Mountain(input.positionOS.xyz, _TerrainBounds, occupancy);
    output.positionWS.xyz = vertexInput.positionWS;

    VertexNormalInputs normalInput;

    float2 heightUV = (output.positionWS.xz + 4096.0f) / 8192.0f;
    float3 normalWS;
    float3 tangentWS;
    float3 bitangentWS;
    
    CalculateNormalFromHeightmap(heightUV, normalWS, tangentWS, bitangentWS);
    normalInput.normalWS = normalWS;
    normalInput.tangentWS = tangentWS;
    normalInput.bitangentWS = bitangentWS;

#if defined(_FOG_FRAGMENT)
        half fogFactor = 0;
#else
        half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
#endif

    output.uv = float2(vertexInput.positionWS.x, vertexInput.positionWS.z);
    output.uv = (output.uv + 4096.0f) / 8192.0f;
    output.positionWS.xyz = vertexInput.positionWS;
    output.positionCS = vertexInput.positionCS;

#ifdef _NORMALMAP
    half3 viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
    output.normalWS = half4(normalInput.normalWS, viewDirWS.x);
    output.tangentWS = half4(normalInput.tangentWS, viewDirWS.y);
    output.bitangentWS = half4(normalInput.bitangentWS, viewDirWS.z);
#else
    output.normalWS = NormalizeNormalPerVertex(normalInput.normalWS);
#endif

    OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
#ifdef DYNAMICLIGHTMAP_ON
    output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif
    OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.vertexSH, output.probeOcclusion);

    #ifdef _ADDITIONAL_LIGHTS_VERTEX
        half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
        output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
    #else
        output.fogFactor = fogFactor;
    #endif

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        output.shadowCoord = GetShadowCoord(vertexInput);
    #endif

    return output;
}

SamplerState OccupancyPointClampSampler;

// Used for StandardSimpleLighting shader
void LitPassFragmentSimple(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out float4 outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    SurfaceData surfaceData;
    InitializeSimpleLitSurfaceData(input.uv, surfaceData);

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));

#if defined(_DBUFFER)
    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
#endif

    InitializeBakedGIData(input, inputData);

    half4 color = UniversalFragmentPBR(inputData, surfaceData.albedo, 0.0, /* specular */ half3(0.0h, 0.0h, 0.0h), 0.0, surfaceData.occlusion, /* emission */ half3(0, 0, 0), surfaceData.alpha);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent(_Surface));

    outColor = color;
#ifdef _WRITE_RENDERING_LAYERS
    uint renderingLayers = GetMeshRenderingLayer();
    outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
#endif
}

#endif
