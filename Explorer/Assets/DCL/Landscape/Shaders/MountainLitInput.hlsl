#ifndef UNIVERSAL_SIMPLE_LIT_INPUT_INCLUDED
#define UNIVERSAL_SIMPLE_LIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseMap_TexelSize;
    half4 _BaseColor;
    half4 _SpecColor;
    half4 _EmissionColor;
    half _Cutoff;
    half _Surface;
    half _terrainScale;
    float4 _TerrainBounds;
    half _terrainHeight;
    int _octaves;
    half _frequency;
    float _sandThreshold;
    float _sandSoftness;
    float _sandScale;
    int _UseHeightMap;
    UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

#ifdef UNITY_DOTS_INSTANCING_ENABLED
UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _SpecColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _EmissionColor)
    UNITY_DOTS_INSTANCED_PROP(float , _Cutoff)
    UNITY_DOTS_INSTANCED_PROP(float , _Surface)
    UNITY_DOTS_INSTANCED_PROP(float , _terrainScale)
    UNITY_DOTS_INSTANCED_PROP(float , _TerrainBounds)
    UNITY_DOTS_INSTANCED_PROP(float , _terrainHeight)
    UNITY_DOTS_INSTANCED_PROP(int ,   _octaves)
    UNITY_DOTS_INSTANCED_PROP(float , _frequency)
    UNITY_DOTS_INSTANCED_PROP(float, _sandThreshold)
    UNITY_DOTS_INSTANCED_PROP(float, _sandSoftness)
    UNITY_DOTS_INSTANCED_PROP(float, _sandScale)
    UNITY_DOTS_INSTANCED_PROP(int, _UseHeightMap)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

static float4   unity_DOTS_Sampled_BaseColor;
static float4   unity_DOTS_Sampled_SpecColor;
static float4   unity_DOTS_Sampled_EmissionColor;
static float    unity_DOTS_Sampled_Cutoff;
static float    unity_DOTS_Sampled_Surface;
static float    unity_DOTS_Sampled_terrainScale;
static float4   unity_DOTS_Sampled_TerrainBounds;
static float    unity_DOTS_Sampled_terrainHeight;
static int      unity_DOTS_Sampled_octaves;
static float    unity_DOTS_Sampled_frequency;
static float    unity_DOTS_Sampled_sandThreshold;
static float    unity_DOTS_Sampled_sandSoftness;
static float    unity_DOTS_Sampled_sandScale;
static int      unity_DOTS_Sampled_UseHeightMap;

void SetupDOTSSimpleLitMaterialPropertyCaches()
{
    unity_DOTS_Sampled_BaseColor        = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4 , _BaseColor);
    unity_DOTS_Sampled_SpecColor        = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4 , _SpecColor);
    unity_DOTS_Sampled_EmissionColor    = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4 , _EmissionColor);
    unity_DOTS_Sampled_Cutoff           = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float  , _Cutoff);
    unity_DOTS_Sampled_Surface          = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float  , _Surface);
    unity_DOTS_Sampled_terrainScale     = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float  , _terrainScale);
    unity_DOTS_Sampled_terrainScale     = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float  , _TerrainBounds);
    unity_DOTS_Sampled_terrainHeight    = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float  , _terrainHeight);
    unity_DOTS_Sampled_octaves          = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(int  , _octaves);
    unity_DOTS_Sampled_frequency        = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float  , _frequency);
    unity_DOTS_Sampled_sandThreshold    = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _sandThreshold);
    unity_DOTS_Sampled_sandSoftness     = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _sandSoftness);
    unity_DOTS_Sampled_sandScale        = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _sandScale);
    unity_DOTS_Sampled_UseHeightMap     = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(int, _UseHeightMap);
}

#undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
#define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupDOTSSimpleLitMaterialPropertyCaches()

#define _BaseColor          unity_DOTS_Sampled_BaseColor
#define _SpecColor          unity_DOTS_Sampled_SpecColor
#define _EmissionColor      unity_DOTS_Sampled_EmissionColor
#define _Cutoff             unity_DOTS_Sampled_Cutoff
#define _Surface            unity_DOTS_Sampled_Surface
#define _terrainScale       unity_DOTS_Sampled_terrainScale
#define _TerrainBounds      unity_DOTS_Sampled_TerrainBounds
#define _terrainHeight      unity_DOTS_Sampled_terrainHeight
#define _octaves            unity_DOTS_Sampled_octaves
#define _frequency          unity_DOTS_Sampled_frequency
#define _sandThreshold      unity_DOTS_Sampled_sandThreshold
#define _sandSoftness       unity_DOTS_Sampled_sandSoftness
#define _sandScale          unity_DOTS_Sampled_sandScale
#define _UseHeightMap       unity_DOTS_Sampled_UseHeightMap

#endif

TEXTURE2D(_SpecGlossMap);       SAMPLER(sampler_SpecGlossMap);
TEXTURE2D(_BlendMap);           SAMPLER(sampler_BlendMap);
TEXTURE2D(_HeightMap);          SAMPLER(sampler_HeightMap);
TEXTURE2D(_OccupancyMap);       SAMPLER(sampler_OccupancyMap);

half4 SampleSpecularSmoothness(float2 uv, half alpha, half4 specColor, TEXTURE2D_PARAM(specMap, sampler_specMap))
{
    half4 specularSmoothness = half4(0, 0, 0, 1);
#ifdef _SPECGLOSSMAP
    specularSmoothness = SAMPLE_TEXTURE2D(specMap, sampler_specMap, uv) * specColor;
#elif defined(_SPECULAR_COLOR)
    specularSmoothness = specColor;
#endif

#ifdef _GLOSSINESS_FROM_BASE_ALPHA
    specularSmoothness.a = alpha;
#endif

    return specularSmoothness;
}

inline void InitializeSimpleLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    outSurfaceData = (SurfaceData)0;

    half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    outSurfaceData.alpha = albedoAlpha.a * _BaseColor.a;
    outSurfaceData.alpha = AlphaDiscard(outSurfaceData.alpha, _Cutoff);

    outSurfaceData.albedo = albedoAlpha.rgb * _BaseColor.rgb;
    outSurfaceData.albedo = AlphaModulate(outSurfaceData.albedo, outSurfaceData.alpha);

    half4 specularSmoothness = SampleSpecularSmoothness(uv, outSurfaceData.alpha, _SpecColor, TEXTURE2D_ARGS(_SpecGlossMap, sampler_SpecGlossMap));
    outSurfaceData.metallic = 0.0; // unused
    outSurfaceData.specular = specularSmoothness.rgb;
    outSurfaceData.smoothness = specularSmoothness.a;
    outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap));
    outSurfaceData.occlusion = 1.0;
    outSurfaceData.emission = SampleEmission(uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));
}

float GetOccupancy(float2 UV_Coords, float4 TerrainBounds, int ParcelSize)
{
    return SAMPLE_TEXTURE2D_LOD(_OccupancyMap, sampler_OccupancyMap, UV_Coords, 0.0).r;
}

#endif
