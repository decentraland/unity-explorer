#ifndef SCENE_INPUT_INCLUDED
#define SCENE_INPUT_INCLUDED

#include "Scene_Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "../URP/Constants.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ParallaxMapping.hlsl"
#include "Scene_SurfaceInput.hlsl"

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
UNITY_DEFINE_INSTANCED_PROP(half4, _SpecColor)
UNITY_DEFINE_INSTANCED_PROP(half4, _EmissionColor)
UNITY_DEFINE_INSTANCED_PROP(half, _Cutoff)
UNITY_DEFINE_INSTANCED_PROP(half, _Smoothness)
UNITY_DEFINE_INSTANCED_PROP(half, _Metallic)
UNITY_DEFINE_INSTANCED_PROP(half, _BumpScale)
UNITY_DEFINE_INSTANCED_PROP(half, _Parallax)
UNITY_DEFINE_INSTANCED_PROP(half, _OcclusionStrength)
UNITY_DEFINE_INSTANCED_PROP(half, _Surface)
UNITY_DEFINE_INSTANCED_PROP(float4, _PlaneClipping)
UNITY_DEFINE_INSTANCED_PROP(int, _BaseMapArr_ID)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

#define _BaseMap_ST             UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST)
#define _BaseColor              UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor)
#define _SpecColor              UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SpecColor)
#define _EmissionColor          UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EmissionColor)
#define _Cutoff                 UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff)
#define _Smoothness             UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness)
#define _Metallic               UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic)
#define _BumpScale              UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BumpScale)
#define _Parallax               UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Parallax)
#define _OcclusionStrength      UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _OcclusionStrength)
#define _Surface                UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Surface)
#define _PlaneClipping          UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _PlaneClipping)
#define _BaseMapArr_ID          UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMapArr_ID)

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
    half4 specGloss = half4(0.0, 0.0, 0.0, 0.0); //half4(SAMPLE_METALLICSPECULAR(uv));
    #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
        specGloss.a = albedoAlpha * _Smoothness;
    #else
        specGloss.a *= _Smoothness;
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

inline void InitializeStandardLitSurfaceData_Scene(float2 uv, out SurfaceData_Scene outSurfaceData)
{
    half4 albedoAlpha = SampleAlbedoAlpha(uv);
    outSurfaceData.alpha = Alpha(albedoAlpha.a, _BaseColor, _Cutoff);
    outSurfaceData.albedo = AlphaModulate(albedoAlpha.rgb * _BaseColor.rgb, outSurfaceData.alpha);

    half4 specGloss = SampleMetallicSpecGloss(uv, albedoAlpha.a);
    outSurfaceData.metallic = specGloss.r;
    outSurfaceData.smoothness = specGloss.a;
    
    outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    outSurfaceData.occlusion = SampleOcclusion(uv);
    outSurfaceData.emission = SampleEmission(uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));
}

#endif // UNIVERSAL_INPUT_SURFACE_PBR_INCLUDED
