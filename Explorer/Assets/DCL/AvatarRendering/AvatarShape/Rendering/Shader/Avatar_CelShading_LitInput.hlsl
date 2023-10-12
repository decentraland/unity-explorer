#ifndef AVATAR_CELSHADING_LITINPUT_INCLUDED
#define AVATAR_CELSHADING_LITINPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "InputData_Avatar.hlsl"
#include "SurfaceData_Avatar.hlsl"
#include "BRDFData_Avatar.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.decentraland.unity-shared-dependencies/Runtime/Shaders/URP/Constants.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"



UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
// UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
// UNITY_DEFINE_INSTANCED_PROP(float4, _DetailAlbedoMap_ST)
UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
UNITY_DEFINE_INSTANCED_PROP(half4, _SpecColor)
UNITY_DEFINE_INSTANCED_PROP(half4, _EmissionColor)
UNITY_DEFINE_INSTANCED_PROP(half, _Cutoff)
UNITY_DEFINE_INSTANCED_PROP(half, _Smoothness)
UNITY_DEFINE_INSTANCED_PROP(half, _Metallic)
UNITY_DEFINE_INSTANCED_PROP(half, _BumpScale)
UNITY_DEFINE_INSTANCED_PROP(half, _Parallax)
UNITY_DEFINE_INSTANCED_PROP(half, _OcclusionStrength)
UNITY_DEFINE_INSTANCED_PROP(half, _DetailAlbedoMapScale)
UNITY_DEFINE_INSTANCED_PROP(half, _DetailNormalMapScale)
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

// #define _BaseMap_ST UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST)
// #define _DetailAlbedoMap_ST UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DetailAlbedoMap_ST)
#define _BaseColor UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor)
#define _SpecColor UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SpecColor)
#define _EmissionColor UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EmissionColor)
#define _Cutoff UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff)
#define _Smoothness UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness)
#define _Metallic UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic)
#define _BumpScale UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BumpScale)
#define _Parallax UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Parallax)
#define _OcclusionStrength UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _OcclusionStrength)
#define _DetailAlbedoMapScale UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DetailAlbedoMapScale)
#define _DetailNormalMapScale UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DetailNormalMapScale)
#define _Surface UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Surface)
#define _CullYPlane UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _CullYPlane)
#define _FadeThickness UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FadeThickness)
#define _FadeDirection UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FadeDirection)
#define _BaseMapUVs UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMapUVs)
#define _NormalMapUVs UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _NormalMapUVs)
#define _MetallicMapUVs UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _MetallicMapUVs)
#define _EmissiveMapUVs UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EmissiveMapUVs)
#define _BaseMapArr_ID UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMapArr_ID) 
#define _AlphaTextureArr_ID UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _AlphaTextureArr_ID) 
#define _MetallicGlossMapArr_ID UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _MetallicGlossMapArr_ID) 
#define _BumpMapArr_ID UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BumpMapArr_ID)
#define _lastWearableVertCount UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _lastWearableVertCount) 
#define _lastAvatarVertCount UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _lastAvatarVertCount)
#define _EmissionMapArr_ID UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EmissionMapArr_ID)
#define _OcclusionMapArr_ID UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _OcclusionMapArr_ID)
#define _DiffuseRampInnerMin UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DiffuseRampInnerMin)
#define _DiffuseRampInnerMax UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DiffuseRampInnerMax)
#define _DiffuseRampOuterMin UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DiffuseRampOuterMin)
#define _DiffuseRampOuterMax UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DiffuseRampOuterMax)
#define _SpecularRampInnerMin UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SpecularRampInnerMin)
#define _SpecularRampInnerMax UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SpecularRampInnerMax)
#define _SpecularRampOuterMin UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SpecularRampOuterMin)
#define _SpecularRampOuterMax UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SpecularRampOuterMax)

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
}

inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData_Avatar outSurfaceData)
{
    InitializeStandardLitSurfaceDataWithUV2( uv, uv, uv, uv, outSurfaceData );
}

inline void InitializeBRDFDataDirect_Avatar(half3 diffuse, half3 specular, half reflectivity, half oneMinusReflectivity, half smoothness, inout half alpha, out BRDFData_Avatar outBRDFData)
{
    outBRDFData.diffuse = diffuse;
    outBRDFData.specular = specular;
    outBRDFData.reflectivity = reflectivity;

    outBRDFData.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(smoothness);
    outBRDFData.roughness           = max(PerceptualRoughnessToRoughness(outBRDFData.perceptualRoughness), HALF_MIN_SQRT);
    outBRDFData.roughness2          = max(outBRDFData.roughness * outBRDFData.roughness, HALF_MIN);
    outBRDFData.grazingTerm         = saturate(smoothness + reflectivity);
    outBRDFData.normalizationTerm   = outBRDFData.roughness * 4.0h + 2.0h;
    outBRDFData.roughness2MinusOne  = outBRDFData.roughness2 - 1.0h;

    #ifdef _ALPHAPREMULTIPLY_ON
    outBRDFData.diffuse *= alpha;
    alpha = alpha * oneMinusReflectivity + reflectivity; // NOTE: alpha modified and propagated up.
    #endif
}

#define kDielectricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04) // standard dielectric reflectivity coef at incident angle (= 4%)

half OneMinusReflectivityMetallic_Avatar(half metallic)
{
    // We'll need oneMinusReflectivity, so
    //   1-reflectivity = 1-lerp(dielectricSpec, 1, metallic) = lerp(1-dielectricSpec, 0, metallic)
    // store (1-dielectricSpec) in kDielectricSpec.a, then
    //   1-reflectivity = lerp(alpha, 0, metallic) = alpha + metallic*(0 - alpha) =
    //                  = alpha - metallic * alpha
    half oneMinusDielectricSpec = kDielectricSpec.a;
    return oneMinusDielectricSpec - metallic * oneMinusDielectricSpec;
}

inline void InitializeBRDFData_Avatar(half3 albedo, half metallic, half3 specular, half smoothness, inout half alpha, out BRDFData_Avatar outBRDFData)
{
    half oneMinusReflectivity = OneMinusReflectivityMetallic_Avatar(metallic);
    half reflectivity = 1.0 - oneMinusReflectivity;
    half3 brdfDiffuse = albedo * oneMinusReflectivity;
    half3 brdfSpecular = lerp(kDieletricSpec.rgb, albedo, metallic);

    InitializeBRDFDataDirect_Avatar(brdfDiffuse, brdfSpecular, reflectivity, oneMinusReflectivity, smoothness, alpha, outBRDFData);
}

// Computes the specular term for EnvironmentBRDF
half3 EnvironmentBRDFSpecular_Avatar(BRDFData_Avatar brdfData, half fresnelTerm)
{
    float surfaceReduction = 1.0 / (brdfData.roughness2 + 1.0);
    return surfaceReduction * brdfData.specular;//lerp(brdfData.specular, brdfData.grazingTerm, fresnelTerm);
}

half3 EnvironmentBRDF_Avatar(BRDFData_Avatar brdfData, half3 indirectDiffuse, half3 indirectSpecular, half fresnelTerm)
{
    half3 c = indirectDiffuse * brdfData.diffuse;
    c += indirectSpecular * EnvironmentBRDFSpecular_Avatar(brdfData, fresnelTerm);
    return c;
}

// The *approximated* version of the non-linear remapping. It works by
// approximating the cone of the specular lobe, and then computing the MIP map level
// which (approximately) covers the footprint of the lobe with a single texel.
// Improves the perceptual roughness distribution.
real PerceptualRoughnessToMipmapLevel_Avatar(real perceptualRoughness, uint maxMipLevel)
{
    perceptualRoughness = perceptualRoughness * (1.7 - 0.7 * perceptualRoughness);
    return perceptualRoughness * maxMipLevel;
}

real3 DecodeHDREnvironment_Avatar(real4 encodedIrradiance, real4 decodeInstructions)
{
    // Take into account texture alpha if decodeInstructions.w is true(the alpha value affects the RGB channels)
    real alpha = max(decodeInstructions.w * (encodedIrradiance.a - 1.0) + 1.0, 0.0);

    // If Linear mode is not supported we can skip exponent part
    return (decodeInstructions.x * PositivePow(alpha, decodeInstructions.y)) * encodedIrradiance.rgb;
}

#ifndef UNITY_SPECCUBE_LOD_STEPS
// This is actuall the last mip index, we generate 7 mips of convolution
#define UNITY_SPECCUBE_LOD_STEPS 6
#endif

half3 GlossyEnvironmentReflection_Avatar(half3 reflectVector, half perceptualRoughness, half occlusion)
{
    #if !defined(_ENVIRONMENTREFLECTIONS_OFF)
    half mip = PerceptualRoughnessToMipmapLevel_Avatar(perceptualRoughness, UNITY_SPECCUBE_LOD_STEPS);
    half4 encodedIrradiance = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectVector, mip);

    //TODO:DOTS - we need to port probes to live in c# so we can manage this manually.
    #if defined(UNITY_USE_NATIVE_HDR) || defined(UNITY_DOTS_INSTANCING_ENABLED)
    half3 irradiance = encodedIrradiance.rgb;
    #else
    half3 irradiance = DecodeHDREnvironment_Avatar(encodedIrradiance, unity_SpecCube0_HDR);
    #endif

    return irradiance * occlusion;
    #endif // GLOSSY_REFLECTIONS

    return _GlossyEnvironmentColor.rgb * occlusion;
}

half3 GlobalIllumination_Avatar(BRDFData_Avatar brdfData,
                                half3 bakedGI,
                                half occlusion,
                                half3 normalWS,
                                half3 viewDirectionWS)
{
    half3 reflectVector = reflect(-viewDirectionWS, normalWS);
    half NoV = saturate(dot(normalWS, viewDirectionWS));
    half fresnelTerm = Pow4(1.0 - NoV);

    half3 indirectDiffuse = bakedGI * occlusion;
    half3 indirectSpecular = GlossyEnvironmentReflection_Avatar(reflectVector, brdfData.perceptualRoughness, occlusion);
    
    return EnvironmentBRDF_Avatar(brdfData, indirectDiffuse, indirectSpecular, fresnelTerm);
}

// Computes the scalar specular term for Minimalist CookTorrance BRDF
// NOTE: needs to be multiplied with reflectance f0, i.e. specular color to complete
half DirectBRDFSpecular_Avatar(BRDFData_Avatar brdfData, half3 normalWS, half3 lightDirectionWS, half3 viewDirectionWS)
{
    float3 halfDir = SafeNormalize(float3(lightDirectionWS) + float3(viewDirectionWS));

    float NoH = saturate(dot(normalWS, halfDir));
    half LoH = saturate(dot(lightDirectionWS, halfDir));

    // GGX Distribution multiplied by combined approximation of Visibility and Fresnel
    // BRDFspec = (D * V * F) / 4.0
    // D = roughness^2 / ( NoH^2 * (roughness^2 - 1) + 1 )^2
    // V * F = 1.0 / ( LoH^2 * (roughness + 0.5) )
    // See "Optimizing PBR for Mobile" from Siggraph 2015 moving mobile graphics course
    // https://community.arm.com/events/1155

    // Final BRDFspec = roughness^2 / ( NoH^2 * (roughness^2 - 1) + 1 )^2 * (LoH^2 * (roughness + 0.5) * 4.0)
    // We further optimize a few light invariant terms
    // brdfData.normalizationTerm = (roughness + 0.5) * 4.0 rewritten as roughness * 4.0 + 2.0 to a fit a MAD.
    float d = NoH * NoH * brdfData.roughness2MinusOne + 1.00001f;

    half LoH2 = LoH * LoH;
    half specularTerm = brdfData.roughness2 / ((d * d) * max(0.1h, LoH2) * brdfData.normalizationTerm);
    
    half specularInner = smoothstep(_SpecularRampInnerMin, _SpecularRampInnerMax, specularTerm);
    half specularOuter = smoothstep(_SpecularRampOuterMin, _SpecularRampOuterMax, specularTerm);
    specularTerm = (specularInner * 0.5f) + (specularOuter * 0.5f);
    return specularTerm;
}

half3 LightingPhysicallyBased_Avatar(   BRDFData_Avatar brdfData,
                                        half3 lightColor,
                                        half3 lightDirectionWS,
                                        half lightAttenuation,
                                        half3 normalWS,
                                        half3 viewDirectionWS,
                                        half2 matCapUV,
                                        bool specularHighlightsOff)
{
    half NdotL1 = smoothstep(_DiffuseRampInnerMin, _DiffuseRampInnerMax, saturate(dot(normalWS, lightDirectionWS)));
    half NdotL2 = smoothstep(_DiffuseRampOuterMin, _DiffuseRampOuterMax, saturate(dot(normalWS, lightDirectionWS)));
    half3 radiance1 = lightColor * (lightAttenuation * NdotL1);
    half3 radiance2 = lightColor * (lightAttenuation * NdotL2);
    half3 radiance = (radiance1 * 0.5f) + (radiance2 * 0.5f);

    half3 brdf = brdfData.diffuse;
    #ifndef _SPECULARHIGHLIGHTS_OFF
    [branch] if (!specularHighlightsOff)
    {
        brdf += brdfData.specular * SAMPLE_TEXTURE2D(_MatCap, sampler_MatCap, matCapUV) * 5.0f * DirectBRDFSpecular_Avatar(brdfData, normalWS, lightDirectionWS, viewDirectionWS);
    }
    #endif // _SPECULARHIGHLIGHTS_OFF

    return brdf * radiance;
}

// Abstraction over Light shading data.
struct Light_Avatar
{
    half3   direction;
    half3   color;
    half    distanceAttenuation;
    half    shadowAttenuation;
};

Light_Avatar GetMainLight_Avatar()
{
    Light_Avatar light;
    light.direction = _MainLightPosition.xyz;
    light.distanceAttenuation = unity_LightData.z; // unity_LightData.z is 1 when not culled by the culling mask, otherwise 0.
    light.shadowAttenuation = 1.0;
    light.color = _MainLightColor.rgb;

    return light;
}

half MainLightRealtimeShadow_Avatar(float4 shadowCoord)
{
    #if !defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    return half(1.0);
    #elif defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
    return SampleScreenSpaceShadowmap(shadowCoord);
    #else
    ShadowSamplingData shadowSamplingData = GetMainLightShadowSamplingData();
    half4 shadowParams = GetMainLightShadowParams();
    return SampleShadowmap(TEXTURE2D_ARGS(_MainLightShadowmapTexture, sampler_LinearClampCompare), shadowCoord, shadowSamplingData, shadowParams, false);
    #endif
}

half MixRealtimeAndBakedShadows_Avatar(half realtimeShadow, half bakedShadow, half shadowFade)
{
    #if defined(LIGHTMAP_SHADOW_MIXING)
    return min(lerp(realtimeShadow, 1, shadowFade), bakedShadow);
    #else
    return lerp(realtimeShadow, bakedShadow, shadowFade);
    #endif
}

half MainLightShadow_Avatar(float4 shadowCoord, float3 positionWS, half4 shadowMask, half4 occlusionProbeChannels)
{
    half realtimeShadow = MainLightRealtimeShadow_Avatar(shadowCoord);

    #ifdef CALCULATE_BAKED_SHADOWS
    half bakedShadow = BakedShadow(shadowMask, occlusionProbeChannels);
    #else
    half bakedShadow = half(1.0);
    #endif

    #ifdef MAIN_LIGHT_CALCULATE_SHADOWS
    half shadowFade = GetMainLightShadowFade(positionWS);
    #else
    half shadowFade = half(1.0);
    #endif

    return MixRealtimeAndBakedShadows_Avatar(realtimeShadow, bakedShadow, shadowFade);
}

Light_Avatar GetMainLight_Avatar(float4 shadowCoord, float3 positionWS, half4 shadowMask)
{
    Light_Avatar light = GetMainLight_Avatar();
    light.shadowAttenuation = MainLightShadow_Avatar(shadowCoord, positionWS, shadowMask, _MainLightOcclusionProbes);
    return light;
}

// void MixRealtimeAndBakedGI_Avatar(inout Light_Avatar light, half3 normalWS, inout half3 bakedGI)
// {
//     #if defined(LIGHTMAP_ON) && defined(_MIXED_LIGHTING_SUBTRACTIVE)
//     bakedGI = SubtractDirectMainLightFromLightmap(light, normalWS, bakedGI);
//     #endif
// }

half4 UniversalFragmentPBR_Avatar(InputData_Avatar inputData, SurfaceData_Avatar surfaceData)
{
    // #ifdef _SPECULARHIGHLIGHTS_OFF
    //     bool specularHighlightsOff = true;
    // #else
        bool specularHighlightsOff = false;
    //#endif

    BRDFData_Avatar brdfData;

    // NOTE: can modify alpha
    InitializeBRDFData_Avatar(surfaceData.albedo, surfaceData.metallic, surfaceData.specular, surfaceData.smoothness, surfaceData.alpha, brdfData);
    
    // To ensure backward compatibility we have to avoid using shadowMask input, as it is not present in older shaders
    #if defined(SHADOWS_SHADOWMASK) && defined(LIGHTMAP_ON)
        half4 shadowMask = inputData.shadowMask;
    #elif !defined (LIGHTMAP_ON)
        half4 shadowMask = unity_ProbesOcclusion;
    #else
        half4 shadowMask = half4(1, 1, 1, 1);
    #endif

    Light_Avatar mainLight = GetMainLight_Avatar(inputData.shadowCoord, inputData.positionWS, shadowMask);

    #if defined(_SCREEN_SPACE_OCCLUSION)
        #if !defined(_SURFACE_TYPE_TRANSPARENT)
            AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
            mainLight.color *= aoFactor.directAmbientOcclusion;
            surfaceData.occlusion = min(surfaceData.occlusion, aoFactor.indirectAmbientOcclusion);
        #endif
    #endif

    //MixRealtimeAndBakedGI_Avatar(mainLight, inputData.normalWS, inputData.bakedGI);
    half3 color = GlobalIllumination_Avatar(brdfData,
                                            inputData.bakedGI,
                                            surfaceData.occlusion,
                                            inputData.normalWS,
                                            inputData.viewDirectionWS);
    color += LightingPhysicallyBased_Avatar(brdfData,
                                            mainLight.color,
                                            mainLight.direction,
                                            mainLight.distanceAttenuation * mainLight.shadowAttenuation,
                                            inputData.normalWS,
                                            inputData.viewDirectionWS,
                                            inputData.matCapUV,
                                            specularHighlightsOff);

    #ifdef _ADDITIONAL_LIGHTS
        uint pixelLightCount = GetAdditionalLightsCount();
        for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
        {
            Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
            #if defined(_SCREEN_SPACE_OCCLUSION)
            #if !defined(_SURFACE_TYPE_TRANSPARENT)
                light.color *= aoFactor.directAmbientOcclusion;
            #endif
            #endif
            color += LightingPhysicallyBased_Avatar(brdfData,
                                                    light,
                                                    inputData.normalWS,
                                                    inputData.viewDirectionWS,
                                                    inputData.matCapUV,
                                                    specularHighlightsOff);
        }
    #endif

    color += surfaceData.emission;

    return half4(color, surfaceData.alpha);
}

#endif