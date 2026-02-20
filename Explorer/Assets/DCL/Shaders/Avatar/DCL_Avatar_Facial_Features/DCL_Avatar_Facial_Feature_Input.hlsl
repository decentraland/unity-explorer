#ifndef DCL_AVATAR_FACIAL_FEATURES_INPUT_INCLUDED
#define DCL_AVATAR_FACIAL_FEATURES_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half4 _SpecColor;
    half4 _EmissionColor;
    half _Cutoff;
    half _Surface;
    int _lastWearableVertCount;
    int _lastAvatarVertCount;
    int _MainTexArr_ID;
    int _MaskTexArr_ID;
    float _EndFadeDistance;
    float _StartFadeDistance;
    float _FadeDistance;
CBUFFER_END

#ifdef UNITY_DOTS_INSTANCING_ENABLED
    UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
        UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
        UNITY_DOTS_INSTANCED_PROP(float4, _SpecColor)
        UNITY_DOTS_INSTANCED_PROP(float4, _EmissionColor)
        UNITY_DOTS_INSTANCED_PROP(float , _Cutoff)
        UNITY_DOTS_INSTANCED_PROP(float , _Surface)
        UNITY_DOTS_INSTANCED_PROP(int, _lastWearableVertCount)
        UNITY_DOTS_INSTANCED_PROP(int, _lastAvatarVertCount)
        UNITY_DOTS_INSTANCED_PROP(int, _MainTexArr_ID)
        UNITY_DOTS_INSTANCED_PROP(int, _MaskTexArr_ID)
        UNITY_DOTS_INSTANCED_PROP(float, _EndFadeDistance)
        UNITY_DOTS_INSTANCED_PROP(float, _StartFadeDistance)
        UNITY_DOTS_INSTANCED_PROP(float, _FadeDistance)
    UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

    static float4 unity_DOTS_Sampled_BaseColor;
    static float4 unity_DOTS_Sampled_SpecColor;
    static float4 unity_DOTS_Sampled_EmissionColor;
    static float unity_DOTS_Sampled_Cutoff;
    static float unity_DOTS_Sampled_Surface;
    static int unity_DOTS_Sampled_lastWearableVertCount;
    static int unity_DOTS_Sampled_lastAvatarVertCount;
    static int unity_DOTS_Sampled_MainTexArr_ID;
    static int unity_DOTS_Sampled_MaskTexArr_ID;
    static float unity_DOTS_Sampled_EndFadeDistance;
    static float unity_DOTS_Sampled_StartFadeDistance;
    static float unity_DOTS_Sampled_FadeDistance;

    void SetupDOTSFacialFeaturesMaterialPropertyCaches()
    {
        unity_DOTS_Sampled_BaseColor                = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor);
        unity_DOTS_Sampled_SpecColor                = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _SpecColor);
        unity_DOTS_Sampled_EmissionColor            = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _EmissionColor);
        unity_DOTS_Sampled_Cutoff                   = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Cutoff);
        unity_DOTS_Sampled_Surface                  = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Surface);
        unity_DOTS_Sampled_lastWearableVertCount    = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(int, _lastWearableVertCount);
        unity_DOTS_Sampled_lastAvatarVertCount      = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(int, _lastAvatarVertCount);
        unity_DOTS_Sampled_MainTexArr_ID            = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(int, _MainTexArr_ID);
        unity_DOTS_Sampled_MaskTexArr_ID            = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(int, _MaskTexArr_ID);
        unity_DOTS_Sampled_EndFadeDistance          = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _EndFadeDistance);
        unity_DOTS_Sampled_StartFadeDistance        = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _StartFadeDistance);
        unity_DOTS_Sampled_FadeDistance             = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _FadeDistance);
    }

    #undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
    #define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupDOTSFacialFeaturesMaterialPropertyCaches()

    #define _BaseColor              unity_DOTS_Sampled_BaseColor
    #define _SpecColor              unity_DOTS_Sampled_SpecColor
    #define _EmissionColor          unity_DOTS_Sampled_EmissionColor
    #define _Cutoff                 unity_DOTS_Sampled_Cutoff
    #define _Surface                unity_DOTS_Sampled_Surface
    #define _lastWearableVertCount  unity_DOTS_Sampled_lastWearableVertCount
    #define _lastAvatarVertCount    unity_DOTS_Sampled_lastAvatarVertCount
    #define _MainTexArr_ID          unity_DOTS_Sampled_MainTexArr_ID
    #define _MaskTexArr_ID          unity_DOTS_Sampled_MaskTexArr_ID
    #define _EndFadeDistance        unity_DOTS_Sampled_EndFadeDistance
    #define _StartFadeDistance      unity_DOTS_Sampled_StartFadeDistance
    #define _FadeDistance           unity_DOTS_Sampled_FadeDistance
#endif

#ifdef _DCL_TEXTURE_ARRAYS
    #define DCL_DECLARE_TEX2DARRAY(tex) Texture2DArray tex; SamplerState sampler##tex
    #define DCL_SAMPLE_TEX2DARRAY(tex,coord) tex.Sample (sampler##tex,coord)
    #define DCL_DECLARE_TEX2DARRAY_DEFAULT_SAMPLER(tex) Texture2DArray tex;
    #define DCL_SAMPLE_TEX2DARRAY_DEFAULT_SAMPLER(tex,coord) tex.Sample (sampler_MainTexArr,coord)
    #define DCL_SAMPLE_TEX2DARRAY_LOD(tex,coord,lod) tex.SampleLevel (sampler##tex,coord,lod)

    DCL_DECLARE_TEX2DARRAY(_MainTexArr);
    #define SAMPLE_MAINTEX(uv,texArrayID)                   DCL_SAMPLE_TEX2DARRAY_DEFAULT_SAMPLER(_MainTexArr, float3(uv, texArrayID))
    DCL_DECLARE_TEX2DARRAY(_MaskTexArr);
    #define SAMPLE_MASKTEX(uv,texArrayID)                   DCL_SAMPLE_TEX2DARRAY_DEFAULT_SAMPLER(_MaskTexArr, float3(uv, texArrayID))
#else
    TEXTURE2D(_MainTex);                                    SAMPLER(sampler_MainTex);
    #define SAMPLE_MAINTEX(uv,texArrayID)                   SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv)
    TEXTURE2D(_MaskTex);                                    SAMPLER(sampler_MaskTex);
    #define SAMPLE_MASKTEX(uv,texArrayID)                   SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, uv)
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

half4 SampleAlbedoAlpha(float2 uv)
{
    int nMainTexArrID = _MainTexArr_ID;
    float4 _MainTex_var = SAMPLE_MAINTEX(uv,nMainTexArrID);
    return _MainTex_var;
}

half4 SampleMaskMap(float2 uv)
{
    int nMaskTexArrID = _MaskTexArr_ID;
    half4 maskColour = half4(SAMPLE_MASKTEX(uv, nMaskTexArrID));
    half4 invertedColour = half4(half3(1.0, 1.0, 1.0) - maskColour.rgb, 1.0 - maskColour.r);
    return invertedColour;
}

inline void InitializeSimpleLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    outSurfaceData = (SurfaceData)0;

    half4 albedoAlpha = SampleAlbedoAlpha(uv);
    //half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    outSurfaceData.alpha = albedoAlpha.a * _BaseColor.a;
    outSurfaceData.alpha = AlphaDiscard(outSurfaceData.alpha, _Cutoff);

    half4 maskColour = SampleMaskMap(uv);
    outSurfaceData.albedo = albedoAlpha.rgb * lerp(half3(1.0, 1.0, 1.0), maskColour.rgb * _BaseColor.rgb, maskColour.a);
    outSurfaceData.albedo = AlphaModulate(outSurfaceData.albedo, outSurfaceData.alpha);

    half4 specularSmoothness = SampleSpecularSmoothness(uv, outSurfaceData.alpha, _SpecColor, TEXTURE2D_ARGS(_SpecGlossMap, sampler_SpecGlossMap));
    outSurfaceData.metallic = 0.0; // unused
    outSurfaceData.specular = specularSmoothness.rgb;
    outSurfaceData.smoothness = specularSmoothness.a;
    outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap));
    outSurfaceData.occlusion = 1.0;
    outSurfaceData.emission = SampleEmission(uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));
}

#endif
