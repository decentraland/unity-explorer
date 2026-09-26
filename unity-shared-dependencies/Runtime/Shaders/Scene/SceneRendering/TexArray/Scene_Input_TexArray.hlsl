#ifndef SCENE_INPUT_INCLUDED
#define SCENE_INPUT_INCLUDED

#include "../Scene_InputData.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "../../URP/Constants.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ParallaxMapping.hlsl"
#include "Scene_SurfaceInput_TexArray.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
half4 _BaseColor;
half4 _SpecColor;
half4 _EmissionColor;
float4 _PlaneClipping;
float4 _VerticalClipping;
half _Cutoff;
half _Smoothness;
half _Metallic;
half _BumpScale;
half _Parallax;
half _OcclusionStrength;
half _Surface;
int _BaseMapArr_ID;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

// NOTE: Do not ifdef the properties for dots instancing, but ifdef the actual usage.
// Otherwise you might break CPU-side as property constant-buffer offsets change per variant.
// NOTE: Dots instancing is orthogonal to the constant buffer above.
#ifdef UNITY_DOTS_INSTANCING_ENABLED

UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _SpecColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _EmissionColor)
    UNITY_DOTS_INSTANCED_PROP(float , _Cutoff)
    UNITY_DOTS_INSTANCED_PROP(float , _Smoothness)
    UNITY_DOTS_INSTANCED_PROP(float , _Metallic)
    UNITY_DOTS_INSTANCED_PROP(float , _BumpScale)
    UNITY_DOTS_INSTANCED_PROP(float , _Parallax)
    UNITY_DOTS_INSTANCED_PROP(float , _OcclusionStrength)
    UNITY_DOTS_INSTANCED_PROP(float , _Surface)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

UNITY_DOTS_INSTANCING_START(UserPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP(float4, _PlaneClipping)
    UNITY_DOTS_INSTANCED_PROP(float4, _VerticalClipping)
    UNITY_DOTS_INSTANCED_PROP(int, _BaseMapArr_ID)
UNITY_DOTS_INSTANCING_END(UserPropertyMetadata)

// Here, we want to avoid overriding a property like e.g. _BaseColor with something like this:
// #define _BaseColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor0)
//
// It would be simpler, but it can cause the compiler to regenerate the property loading code for each use of _BaseColor.
//
// To avoid this, the property loads are cached in some static values at the beginning of the shader.
// The properties such as _BaseColor are then overridden so that it expand directly to the static value like this:
// #define _BaseColor unity_DOTS_Sampled_BaseColor
//
// This simple fix happened to improve GPU performances by ~10% on Meta Quest 2 with URP on some scenes.
static float4 unity_DOTS_Sampled_BaseColor;
static float4 unity_DOTS_Sampled_SpecColor;
static float4 unity_DOTS_Sampled_EmissionColor;
static float4 unity_DOTS_Sampled_PlaneClipping;
static float4 unity_DOTS_Sampled_VerticalClipping;
static float  unity_DOTS_Sampled_Cutoff;
static float  unity_DOTS_Sampled_Smoothness;
static float  unity_DOTS_Sampled_Metallic;
static float  unity_DOTS_Sampled_BumpScale;
static float  unity_DOTS_Sampled_Parallax;
static float  unity_DOTS_Sampled_OcclusionStrength;
static float  unity_DOTS_Sampled_Surface;
static int unity_DOTS_Sampled_BaseMapArr_ID;

void SetupDOTSSceneTexArrayMaterialPropertyCaches()
{
    unity_DOTS_Sampled_BaseColor            = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor);
    unity_DOTS_Sampled_SpecColor            = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _SpecColor);
    unity_DOTS_Sampled_EmissionColor        = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _EmissionColor);
    unity_DOTS_Sampled_PlaneClipping        = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _PlaneClipping);
    unity_DOTS_Sampled_VerticalClipping     = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _VerticalClipping);
    unity_DOTS_Sampled_Cutoff               = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Cutoff);
    unity_DOTS_Sampled_Smoothness           = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Smoothness);
    unity_DOTS_Sampled_Metallic             = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Metallic);
    unity_DOTS_Sampled_BumpScale            = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _BumpScale);
    unity_DOTS_Sampled_Parallax             = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Parallax);
    unity_DOTS_Sampled_OcclusionStrength    = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _OcclusionStrength);
    unity_DOTS_Sampled_Surface              = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Surface);
    unity_DOTS_Sampled_BaseMapArr_ID        = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(int , _BaseMapArr_ID);
}

#undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
#define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupDOTSSceneTexArrayMaterialPropertyCaches()

#define _BaseColor              unity_DOTS_Sampled_BaseColor
#define _SpecColor              unity_DOTS_Sampled_SpecColor
#define _EmissionColor          unity_DOTS_Sampled_EmissionColor
#define _PlaneClipping          unity_DOTS_Sampled_PlaneClipping
#define _VerticalClipping       unity_DOTS_Sampled_VerticalClipping
#define _Cutoff                 unity_DOTS_Sampled_Cutoff
#define _Smoothness             unity_DOTS_Sampled_Smoothness
#define _Metallic               unity_DOTS_Sampled_Metallic
#define _BumpScale              unity_DOTS_Sampled_BumpScale
#define _Parallax               unity_DOTS_Sampled_Parallax
#define _OcclusionStrength      unity_DOTS_Sampled_OcclusionStrength
#define _Surface                unity_DOTS_Sampled_Surface
#define _BaseMapArr_ID          unity_DOTS_Sampled_BaseMapArr_ID
#endif

#define _DCL_TEXTURE_ARRAYS

#ifdef _DCL_TEXTURE_ARRAYS
    #define DCL_DECLARE_TEX2DARRAY(tex) Texture2DArray tex; SamplerState sampler##tex
    #define DCL_SAMPLE_TEX2DARRAY(tex,coord) tex.Sample (sampler##tex,coord)

    DCL_DECLARE_TEX2DARRAY(_BaseMapArr);
    #define SAMPLE_BASEMAP(uv, texArrayID)                  DCL_SAMPLE_TEX2DARRAY(_BaseMapArr, float3(uv, texArrayID))
#else
    TEXTURE2D(_BaseMap);
    SAMPLER(sampler_BaseMap);
    #define SAMPLE_BASEMAP(uv,texArrayID)                   SAMPLE_TEXTURE2D(_BaseMap,                  sampler_BaseMap, uv)
#endif

half4 SampleAlbedoAlpha(float2 uv)
{
    int nBaseMapArrID = _BaseMapArr_ID;
    return half4(SAMPLE_BASEMAP(uv,nBaseMapArrID));
}

TEXTURE2D(_ParallaxMap);        SAMPLER(sampler_ParallaxMap);
TEXTURE2D(_OcclusionMap);       SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_MetallicGlossMap);   SAMPLER(sampler_MetallicGlossMap);

#define SAMPLE_METALLICSPECULAR(uv) SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv)

half4 SampleMetallicSpecGloss(float2 uv, half albedoAlpha)
{
    half4 specGloss;
    #ifdef _METALLICSPECGLOSSMAP
        specGloss = half4(SAMPLE_METALLICSPECULAR(uv));
        //ARM Texture - Provides Height in R, Metallic in B and Roughness in G
        specGloss.a = 1.0 - specGloss.g; //Conversion from RoughnessToSmoothness
        specGloss.rgb = specGloss.rgb;
    #else // _METALLICSPECGLOSSMAP
        specGloss.rgb = _Metallic.rrr;
        specGloss.a = _Smoothness;
    #endif
    return specGloss;
}

half SampleOcclusion(float2 uv)
{
    #ifdef _OCCLUSIONMAP
        half occ = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).g;
        return LerpWhiteTo(occ, _OcclusionStrength);
    #else
        return half(1.0);
    #endif
}

void ApplyPerPixelDisplacement(half3 viewDirTS, inout float2 uv)
{
#if defined(_PARALLAXMAP)
    uv += ParallaxMapping(TEXTURE2D_ARGS(_ParallaxMap, sampler_ParallaxMap), viewDirTS, _Parallax, uv);
#endif
}

inline void InitializeStandardLitSurfaceData_Scene(float2 uv, float4 _PerInstanceColour, out SurfaceData_Scene outSurfaceData)
{
    half4 albedoAlpha = SampleAlbedoAlpha(uv);
    outSurfaceData.alpha = Alpha(albedoAlpha.a, _BaseColor * _PerInstanceColour, _Cutoff);
    outSurfaceData.albedo = AlphaModulate(albedoAlpha.rgb * _BaseColor.rgb * _PerInstanceColour.rgb, outSurfaceData.alpha);

    half4 specGloss = SampleMetallicSpecGloss(uv, albedoAlpha.a);
    outSurfaceData.metallic = specGloss.b;
    outSurfaceData.smoothness = specGloss.a;
    
    outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    outSurfaceData.occlusion = SampleOcclusion(uv);
    outSurfaceData.emission = SampleEmission(uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));
    outSurfaceData.height = specGloss.r;
}

#ifdef _GPU_INSTANCER_BATCHER
float3 TransformObjectToWorld_PerInstance(float3 positionOS, uint _instanceID)
{
    #if defined(SHADER_STAGE_RAY_TRACING)
    return mul(ObjectToWorld3x4(), float4(positionOS, 1.0)).xyz;
    #else
    uint instID = _PerInstanceLookUpAndDitherBuffer[instanceID].instanceID;
    return mul(_PerInstanceBuffer[instID].instMatrix, float4(positionOS, 1.0)).xyz;
    #endif
}

VertexPositionInputs GetVertexPositionInputs_PerInstance(float3 positionOS, uint _instanceID)
{
    VertexPositionInputs input;
    input.positionWS = TransformObjectToWorld_PerInstance(positionOS, _instanceID);
    input.positionVS = TransformWorldToView(input.positionWS);
    input.positionCS = TransformWorldToHClip(input.positionWS);

    float4 ndc = input.positionCS * 0.5f;
    input.positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
    input.positionNDC.zw = input.positionCS.zw;

    return input;
}
#endif

VertexPositionInputs GetVertexPositionInputs_Scene(float3 _positionOS, uint _svInstanceID)
{
    #ifdef _GPU_INSTANCER_BATCHER
    uint cmdID = GetCommandID(0);
    uint instanceID = GetIndirectInstanceID(_svInstanceID);
    VertexPositionInputs vertexInput = GetVertexPositionInputs_PerInstance(_positionOS, instanceID);
    return vertexInput;
    #else
    VertexPositionInputs vertexInput = GetVertexPositionInputs(_positionOS);
    return vertexInput;
    #endif
}

VertexNormalInputs GetVertexNormalInputs_Scene(float3 normalOS, float4 tangentOS, uint _svInstanceID)
{
    VertexNormalInputs tbn;
#ifdef _GPU_INSTANCER_BATCHER
    // Use per-instance transformations for GPU instancing
    tbn.normalWS = TransformObjectToWorldNormal_Scene(normalOS, _svInstanceID);
    tbn.tangentWS = TransformObjectToWorldDir_Scene(tangentOS.xyz, _svInstanceID);
    tbn.bitangentWS = cross(tbn.normalWS, tbn.tangentWS) * tangentOS.w;
#else
    // Use standard transformations for non-instanced rendering
    tbn = GetVertexNormalInputs(normalOS, tangentOS);
#endif
    return tbn;
}

#endif // UNIVERSAL_INPUT_SURFACE_PBR_INCLUDED
