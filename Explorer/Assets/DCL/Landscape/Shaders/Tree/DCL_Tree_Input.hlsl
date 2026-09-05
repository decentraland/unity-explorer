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
    half _RequiresWind;
    float _WindSpeed;
    float _WindStrength;
    float _WindFrequency;
    float _WindTurbulence;
    float4 _WindDirection;
    float _FlutterSpeed;
    float _FlutterStrength;
    float _FlutterFrequency;
    UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

#ifdef UNITY_DOTS_INSTANCING_ENABLED
UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _SpecColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _EmissionColor)
    UNITY_DOTS_INSTANCED_PROP(float , _Cutoff)
    UNITY_DOTS_INSTANCED_PROP(float , _Surface)
    UNITY_DOTS_INSTANCED_PROP(float , _RequiresWind)
    UNITY_DOTS_INSTANCED_PROP(float, _WindSpeed)
    UNITY_DOTS_INSTANCED_PROP(float, _WindStrength)
    UNITY_DOTS_INSTANCED_PROP(float, _WindFrequency)
    UNITY_DOTS_INSTANCED_PROP(float, _WindTurbulence)
    UNITY_DOTS_INSTANCED_PROP(float4, _WindDirection)
    UNITY_DOTS_INSTANCED_PROP(float, _FlutterSpeed)
    UNITY_DOTS_INSTANCED_PROP(float, _FlutterStrength)
    UNITY_DOTS_INSTANCED_PROP(float, _FlutterFrequency)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

static float4 unity_DOTS_Sampled_BaseColor;
static float4 unity_DOTS_Sampled_SpecColor;
static float4 unity_DOTS_Sampled_EmissionColor;
static float  unity_DOTS_Sampled_Cutoff;
static float  unity_DOTS_Sampled_Surface;
static float  unity_DOTS_Sampled_RequiresWind;
static float  unity_DOTS_Sampled_WindSpeed;
static float  unity_DOTS_Sampled_WindStrength;
static float  unity_DOTS_Sampled_WindFrequency;
static float  unity_DOTS_Sampled_WindTurbulence;
static float  unity_DOTS_Sampled_WindDirection;
static float  unity_DOTS_Sampled_FlutterSpeed;
static float  unity_DOTS_Sampled_FlutterStrength;
static float  unity_DOTS_Sampled_FlutterFrequency;


void SetupDOTSSimpleLitMaterialPropertyCaches()
{
    unity_DOTS_Sampled_BaseColor        = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4 , _BaseColor);
    unity_DOTS_Sampled_SpecColor        = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4 , _SpecColor);
    unity_DOTS_Sampled_EmissionColor    = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4 , _EmissionColor);
    unity_DOTS_Sampled_Cutoff           = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float  , _Cutoff);
    unity_DOTS_Sampled_Surface          = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float  , _Surface);
    unity_DOTS_Sampled_RequiresWind     = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float  , _RequiresWind);
    unity_DOTS_Sampled_WindSpeed        = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _WindSpeed);
    unity_DOTS_Sampled_WindStrength     = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _WindStrength);
    unity_DOTS_Sampled_WindFrequency    = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _WindFrequency);
    unity_DOTS_Sampled_WindTurbulence   = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _WindTurbulence);
    unity_DOTS_Sampled_WindDirection    = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _WindDirection);
    unity_DOTS_Sampled_FlutterSpeed     = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _FlutterSpeed);
    unity_DOTS_Sampled_FlutterStrength  = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _FlutterStrength);
    unity_DOTS_Sampled_FlutterFrequency = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _FlutterFrequency);
}

#undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
#define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupDOTSSimpleLitMaterialPropertyCaches()

#define _BaseColor          unity_DOTS_Sampled_BaseColor
#define _SpecColor          unity_DOTS_Sampled_SpecColor
#define _EmissionColor      unity_DOTS_Sampled_EmissionColor
#define _Cutoff             unity_DOTS_Sampled_Cutoff
#define _Surface            unity_DOTS_Sampled_Surface
#define _RequiresWind       unity_DOTS_Sampled_RequiresWind
#define _WindSpeed          unity_DOTS_Sampled_WindSpeed
#define _WindStrength       unity_DOTS_Sampled_WindStrength
#define _WindFrequency      unity_DOTS_Sampled_WindFrequency
#define _WindTurbulence     unity_DOTS_Sampled_WindTurbulence
#define _WindDirection      unity_DOTS_Sampled_WindDirection
#define _FlutterSpeed       unity_DOTS_Sampled_FlutterSpeed
#define _FlutterStrength    unity_DOTS_Sampled_FlutterStrength
#define _FlutterFrequency   unity_DOTS_Sampled_FlutterFrequency

#endif

TEXTURE2D(_SpecGlossMap);       SAMPLER(sampler_SpecGlossMap);

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

// Simplex-style noise for organic movement
float hash(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}
            
float noise3D(float3 x)
{
    float3 i = floor(x);
    float3 f = frac(x);
    f = f * f * (3.0 - 2.0 * f);
                
    return lerp(
        lerp(
            lerp(hash(i + float3(0,0,0)), hash(i + float3(1,0,0)), f.x),
            lerp(hash(i + float3(0,1,0)), hash(i + float3(1,1,0)), f.x),
            f.y
        ),
        lerp(
            lerp(hash(i + float3(0,0,1)), hash(i + float3(1,0,1)), f.x),
            lerp(hash(i + float3(0,1,1)), hash(i + float3(1,1,1)), f.x),
            f.y
        ),
        f.z
    );
}
            
// Fractal Brownian Motion for more natural turbulence
float fbm(float3 p, int octaves)
{
    float value = 0.0;
    float amplitude = 0.5;
    float frequency = 1.0;
                
    for (int i = 0; i < octaves; i++)
    {
        value += amplitude * noise3D(p * frequency);
        amplitude *= 0.5;
        frequency *= 2.0;
    }
                
    return value;
}

#endif