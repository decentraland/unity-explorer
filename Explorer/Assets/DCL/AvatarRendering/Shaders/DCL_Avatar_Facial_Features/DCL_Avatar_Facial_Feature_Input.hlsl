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
    UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

    #define _BaseColor              UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4 , _BaseColor)
    #define _SpecColor              UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4 , _SpecColor)
    #define _EmissionColor          UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4 , _EmissionColor)
    #define _Cutoff                 UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float  , _Cutoff)
    #define _Surface                UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float  , _Surface)
    #define _lastWearableVertCount  UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(int, _lastWearableVertCount)
    #define _lastAvatarVertCount    UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(int, _lastAvatarVertCount)
    #define _MainTexArr_ID          UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(int, _MainTexArr_ID)
    #define _MaskTexArr_ID          UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(int, _MaskTexArr_ID)
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
    half4 invertedColour = half4(half3(1.0, 1.0, 1.0) - maskColour.rgb, maskColour.a);
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
    outSurfaceData.albedo = albedoAlpha.rgb * (maskColour.rgb * _BaseColor.rgb);
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
