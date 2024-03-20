#ifndef AVATAR_CELSHADING_LITINPUT_INCLUDED
#define AVATAR_CELSHADING_LITINPUT_INCLUDED

#include "Avatar_CelShading_Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.decentraland.unity-shared-dependencies/Runtime/Shaders/URP/Constants.hlsl"
#include "Avatar_CelShading_Shadows.hlsl"
#include "Avatar_CelShading_Input.hlsl"
#include "Avatar_CelShading_SurfaceData.hlsl"
#include "Avatar_CelShading_BRDF.hlsl"
#include "Avatar_CelShading_RealtimeLights.hlsl"

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
UNITY_DEFINE_INSTANCED_PROP(float, _CullYPlane)
UNITY_DEFINE_INSTANCED_PROP(half, _FadeThickness)
UNITY_DEFINE_INSTANCED_PROP(half, _FadeDirection)
UNITY_DEFINE_INSTANCED_PROP(int, _BaseMapUVs)
UNITY_DEFINE_INSTANCED_PROP(int, _NormalMapUVs)
UNITY_DEFINE_INSTANCED_PROP(int, _MetallicMapUVs)
UNITY_DEFINE_INSTANCED_PROP(int, _EmissiveMapUVs)
UNITY_DEFINE_INSTANCED_PROP(int, _BaseMapArr_ID)
UNITY_DEFINE_INSTANCED_PROP(int, _AlphaTextureArr_ID)
UNITY_DEFINE_INSTANCED_PROP(int, _MetallicGlossMapArr_ID)
UNITY_DEFINE_INSTANCED_PROP(int, _BumpMapArr_ID)
UNITY_DEFINE_INSTANCED_PROP(int, _EmissionMapArr_ID)
UNITY_DEFINE_INSTANCED_PROP(int, _OcclusionMapArr_ID)
UNITY_DEFINE_INSTANCED_PROP(int, _lastWearableVertCount)
UNITY_DEFINE_INSTANCED_PROP(int, _lastAvatarVertCount)
UNITY_DEFINE_INSTANCED_PROP(float, _DiffuseRampInnerMin)
UNITY_DEFINE_INSTANCED_PROP(float, _DiffuseRampInnerMax)
UNITY_DEFINE_INSTANCED_PROP(float, _DiffuseRampOuterMin)
UNITY_DEFINE_INSTANCED_PROP(float, _DiffuseRampOuterMax)
UNITY_DEFINE_INSTANCED_PROP(float, _SpecularRampInnerMin)
UNITY_DEFINE_INSTANCED_PROP(float, _SpecularRampInnerMax)
UNITY_DEFINE_INSTANCED_PROP(float, _SpecularRampOuterMin)
UNITY_DEFINE_INSTANCED_PROP(float, _SpecularRampOuterMax)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

#define _BaseMap_ST                     UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST)
#define _BaseColor                      UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor)
#define _SpecColor                      UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SpecColor)
#define _EmissionColor                  UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EmissionColor)
#define _Cutoff                         UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff)
#define _Smoothness                     UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness)
#define _Metallic                       UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic)
#define _BumpScale                      UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BumpScale)
#define _Parallax                       UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Parallax)
#define _OcclusionStrength              UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _OcclusionStrength)
#define _Surface                        UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Surface)
#define _CullYPlane                     UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _CullYPlane)
#define _FadeThickness                  UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FadeThickness)
#define _FadeDirection                  UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FadeDirection)
#define _BaseMapUVs                     UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMapUVs)
#define _NormalMapUVs                   UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _NormalMapUVs)
#define _MetallicMapUVs                 UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _MetallicMapUVs)
#define _EmissiveMapUVs                 UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EmissiveMapUVs)
#define _BaseMapArr_ID                  UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMapArr_ID) 
#define _AlphaTextureArr_ID             UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _AlphaTextureArr_ID) 
#define _MetallicGlossMapArr_ID         UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _MetallicGlossMapArr_ID) 
#define _BumpMapArr_ID                  UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BumpMapArr_ID)
#define _lastWearableVertCount          UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _lastWearableVertCount) 
#define _lastAvatarVertCount            UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _lastAvatarVertCount)
#define _EmissionMapArr_ID              UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EmissionMapArr_ID)
#define _OcclusionMapArr_ID             UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _OcclusionMapArr_ID)
#define _DiffuseRampInnerMin            UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DiffuseRampInnerMin)
#define _DiffuseRampInnerMax            UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DiffuseRampInnerMax)
#define _DiffuseRampOuterMin            UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DiffuseRampOuterMin)
#define _DiffuseRampOuterMax            UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DiffuseRampOuterMax)
#define _SpecularRampInnerMin           UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SpecularRampInnerMin)
#define _SpecularRampInnerMax           UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SpecularRampInnerMax)
#define _SpecularRampOuterMin           UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SpecularRampOuterMin)
#define _SpecularRampOuterMax           UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SpecularRampOuterMax)

/////////////////////////
// from SurfaceInput.hlsl
// TEXTURE2D(_BaseMap);
// SAMPLER(sampler_BaseMap);
// float4 _BaseMap_TexelSize;
// float4 _BaseMap_MipInfo;
/////////////////////////

#define _tex_arrays

#ifdef _tex_arrays
    #define DCL_DECLARE_TEX2DARRAY(tex) Texture2DArray tex; SamplerState sampler##tex
    #define DCL_SAMPLE_TEX2DARRAY(tex,coord) tex.Sample (sampler##tex,coord)

    DCL_DECLARE_TEX2DARRAY(_BaseMapArr);
    DCL_DECLARE_TEX2DARRAY(_AlphaTextureArr);
    DCL_DECLARE_TEX2DARRAY(_MetallicGlossMapArr);
    DCL_DECLARE_TEX2DARRAY(_SpecGlossMapArr);
    DCL_DECLARE_TEX2DARRAY(_BumpMapArr);
    DCL_DECLARE_TEX2DARRAY(_ParallaxMapArr);
    DCL_DECLARE_TEX2DARRAY(_OcclusionMapArr);
    DCL_DECLARE_TEX2DARRAY(_EmissionMapArr);

    #define SAMPLE_BASEMAP(uv, texArrayID)                  DCL_SAMPLE_TEX2DARRAY(_BaseMapArr, float3(uv, texArrayID))
    #define SAMPLE_ALPHA(uv, texArrayID)                    DCL_SAMPLE_TEX2DARRAY(_AlphaTextureArr, float3(uv, texArrayID))
    #define SAMPLE_METALLICSPECULAR(uv, texArrayID)     DCL_SAMPLE_TEX2DARRAY(_MetallicGlossMapArr, float3(uv, texArrayID))
    #define SAMPLE_BUMP(uv, texArrayID)                     DCL_SAMPLE_TEX2DARRAY(_BumpMapArr, float3(uv, texArrayID))
    #define SAMPLE_PARALLAX(uv, texArrayID)                 DCL_SAMPLE_TEX2DARRAY(_ParallaxMapArr, float3(uv, texArrayID))
    #define SAMPLE_OCCLUSION(uv, texArrayID)                DCL_SAMPLE_TEX2DARRAY(_OcclusionMapArr, float3(uv, texArrayID))
    #define SAMPLE_EMISSION(uv, texArrayID)                 DCL_SAMPLE_TEX2DARRAY(_EmissionMapArr, float3(uv, texArrayID))
    TEXTURE2D(_MatCap);                                     SAMPLER(sampler_MatCap);
#else
    TEXTURE2D(_AlphaTexture);       SAMPLER(sampler_AlphaTexture);
    TEXTURE2D(_ParallaxMap);        SAMPLER(sampler_ParallaxMap);
    TEXTURE2D(_OcclusionMap);       SAMPLER(sampler_OcclusionMap);
    TEXTURE2D(_DetailMask);         SAMPLER(sampler_DetailMask);
    TEXTURE2D(_DetailAlbedoMap);    SAMPLER(sampler_DetailAlbedoMap);
    TEXTURE2D(_DetailNormalMap);    SAMPLER(sampler_DetailNormalMap);
    TEXTURE2D(_MetallicGlossMap);   SAMPLER(sampler_MetallicGlossMap);
    TEXTURE2D(_SpecGlossMap);       SAMPLER(sampler_SpecGlossMap);
    TEXTURE2D(_ClearCoatMap);       SAMPLER(sampler_ClearCoatMap);

    #define SAMPLE_BASEMAP(uv)                  SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap))
    #define SAMPLE_ALPHA(uv)                    SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_AlphaTexture, sampler_AlphaTexture))
    #define SAMPLE_METALLICSPECULAR(uv)     SAMPLE_TEXTURE2D(_SpecGlossMap, sampler_SpecGlossMap, uv)
    #define SAMPLE_OCCLUSION(uv)                SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv)
    #define SAMPLE_EMISSION(uv)                 SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv)
#endif

half Alpha(half albedoAlpha, half4 color, half cutoff)
{
    #if !defined(_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A) && !defined(_GLOSSINESS_FROM_BASE_ALPHA)
    half alpha = albedoAlpha * color.a;
    #else
    half alpha = color.a;
    #endif

    alpha = AlphaDiscard(alpha, cutoff);

    return alpha;
}

half4 SampleAlbedoAlpha(float2 uv, TEXTURE2D_PARAM(albedoAlphaMap, sampler_albedoAlphaMap))
{
    return half4(SAMPLE_TEXTURE2D(albedoAlphaMap, sampler_albedoAlphaMap, uv));
}

half4 SampleMetallicSpecGloss(float2 uv, half albedoAlpha)
{
    half4 specGloss;
    int nMetallicGlossMapArrID = _MetallicGlossMapArr_ID;
    specGloss = SAMPLE_METALLICSPECULAR(uv, nMetallicGlossMapArrID);
    
     //GLTF Provides Metallic in B and Roughness in G
    specGloss.a = 1.0 - specGloss.g; //Conversion to GLTF and from RoughnessToSmoothness
    specGloss.rgb = specGloss.bbb; //Conversion to GLTF

    #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
        specGloss.a = albedoAlpha * _Smoothness;
    #else
        specGloss.a *= _Smoothness;
    #endif
       
    specGloss.rgb *= _Metallic.rrr;
    return specGloss;
}

half SampleOcclusion(float2 uv)
{
    #if defined(_SURFACE_TYPE_TRANSPARENT)
        return 1.0;
    #endif

    // No occlusion for transparent surfaces. They don't render normals.
    if (_Surface == SURFACE_TRANSPARENT)
        return 1.0;
         
    #ifdef _OCCLUSIONMAP
        // TODO: Controls things like these by exposing SHADER_QUALITY levels (low, medium, high)
        #if defined(SHADER_API_GLES)
            int nOcclusionMapArrID = _OcclusionMapArr_ID;
            return SAMPLE_OCCLUSION(uv, nOcclusionMapArrID).g;
        #else
            int nOcclusionMapArrID = _OcclusionMapArr_ID;
            half occ = SAMPLE_OCCLUSION(uv, nOcclusionMapArrID).g;
            return LerpWhiteTo(occ, _OcclusionStrength);
        #endif
    #else
        return 1.0;
    #endif
}

half3 SampleNormal(float2 uv, half scale = half(1.0))
{
    #ifdef _NORMALMAP
        int nBumpMapArrID = _BumpMapArr_ID;
        half4 n = SAMPLE_BUMP(uv, nBumpMapArrID);
        #if BUMP_SCALE_NOT_SUPPORTED
            return UnpackNormal(n);
        #else
            return UnpackNormalScale(n, scale);
        #endif
    #else
        return half3(0.0h, 0.0h, 1.0h);
    #endif
}

half3 SampleEmission(float2 uv, half3 emissionColor)
{
    #ifndef _EMISSION
        return 0;
    #else
        int nEmissionMapArrID = _EmissionMapArr_ID;
        return SAMPLE_EMISSION(uv, nEmissionMapArrID).rgb * emissionColor;
    #endif
}

inline void InitializeStandardLitSurfaceDataWithUV2(float2 uvAlbedo, float2 uvNormal, float2 uvMetallic, float2 uvEmissive, out SurfaceData_Avatar outSurfaceData)
{
    int nBaseMapArrID = _BaseMapArr_ID;
    int nAlphaTextureArrID = _AlphaTextureArr_ID;
    half4 albedoAlpha = half4(SAMPLE_BASEMAP(uvAlbedo, nBaseMapArrID));
    half4 alphaTexture = half4(SAMPLE_ALPHA(uvAlbedo, nAlphaTextureArrID));

    outSurfaceData.alpha = Alpha(albedoAlpha.a, _BaseColor, _Cutoff) * saturate(length(alphaTexture.rgb));
    
    half4 specGloss = SampleMetallicSpecGloss(uvMetallic, albedoAlpha.a);
    outSurfaceData.albedo = albedoAlpha.rgb * _BaseColor.rgb;
    outSurfaceData.metallic = specGloss.r;
    outSurfaceData.specular = half3(0.0h, 0.0h, 0.0h);
    outSurfaceData.smoothness = specGloss.a;
    outSurfaceData.normalTS = SampleNormal(uvNormal, _BumpScale);
    
    // NOTE(Brian): Enabling _NORMAL_MAP without maps gives precision artifacts, we have to round up the normals
    if (outSurfaceData.normalTS.x > -.004 && outSurfaceData.normalTS.x < .004)
        outSurfaceData.normalTS.x = 0;

    if (outSurfaceData.normalTS.y > -.004 && outSurfaceData.normalTS.y < .004)
        outSurfaceData.normalTS.y = 0;

    outSurfaceData.occlusion = SampleOcclusion(uvAlbedo);
    outSurfaceData.emission = SampleEmission(uvEmissive, _EmissionColor.rgb);

    outSurfaceData.specularRampInnerMin = _SpecularRampInnerMin;
    outSurfaceData.specularRampInnerMax = _SpecularRampInnerMax;
    outSurfaceData.specularRampOuterMin = _SpecularRampOuterMin;
    outSurfaceData.specularRampOuterMax = _SpecularRampOuterMax;
}

inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData_Avatar outSurfaceData)
{
    InitializeStandardLitSurfaceDataWithUV2( uv, uv, uv, uv, outSurfaceData );
}

#endif