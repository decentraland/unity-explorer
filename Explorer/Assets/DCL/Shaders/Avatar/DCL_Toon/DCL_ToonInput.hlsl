#ifndef DCL_TOON_INPUT_INCLUDED
#define DCL_TOON_INPUT_INCLUDED

#define _DCL_VARIABLE_OPTIMISATION

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "DCL_ToonVariables.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _MainTex_ST; // Per Material
float4 _NormalMap_ST; // Per Material
float4 _MatCap_Sampler_ST; // Per Material
float4 _Emissive_Tex_ST; // Per Material
float4 _BaseMap_ST; // Per Material
half4 _BaseColor;
half4 _SpecColor;
float4 _Emissive_Color;
float _EndFadeDistance;
float _StartFadeDistance;
float _FadeDistance;
float _Clipping_Level;
float _Tweak_transparency;
int _MainTexArr_ID; 
int _NormalMapArr_ID;
int _MatCap_SamplerArr_ID; 
int _Emissive_TexArr_ID; 
int _MetallicGlossMapArr_ID;
int _lastWearableVertCount;
int _lastAvatarVertCount;
CBUFFER_END

// NOTE: Do not ifdef the properties for dots instancing, but ifdef the actual usage.
// Otherwise you might break CPU-side as property constant-buffer offsets change per variant.
// NOTE: Dots instancing is orthogonal to the constant buffer above.
#ifdef UNITY_DOTS_INSTANCING_ENABLED

UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP(float4, _MainTex_ST)
    UNITY_DOTS_INSTANCED_PROP(float4, _NormalMap_ST)
    UNITY_DOTS_INSTANCED_PROP(float4, _MatCap_Sampler_ST)
    UNITY_DOTS_INSTANCED_PROP(float4, _Emissive_Tex_ST)
    UNITY_DOTS_INSTANCED_PROP(float4, _BaseMap_ST)
    UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _SpecColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _Emissive_Color)
    UNITY_DOTS_INSTANCED_PROP(float, _Clipping_Level)
    UNITY_DOTS_INSTANCED_PROP(float, _Tweak_transparency)
    UNITY_DOTS_INSTANCED_PROP(int, _lastWearableVertCount)
    UNITY_DOTS_INSTANCED_PROP(int, _lastAvatarVertCount)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

UNITY_DOTS_INSTANCING_START(UserPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP(int, _MainTexArr_ID) 
    UNITY_DOTS_INSTANCED_PROP(int, _NormalMapArr_ID)
    UNITY_DOTS_INSTANCED_PROP(int, _MatCap_SamplerArr_ID) 
    UNITY_DOTS_INSTANCED_PROP(int, _Emissive_TexArr_ID) 
    UNITY_DOTS_INSTANCED_PROP(int, _MetallicGlossMapArr_ID)
    UNITY_DOTS_INSTANCED_PROP(float, _EndFadeDistance)
    UNITY_DOTS_INSTANCED_PROP(float, _StartFadeDistance)
    UNITY_DOTS_INSTANCED_PROP(float, _FadeDistance)
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
static float4 unity_DOTS_Sampled_MainTex_ST;
static float4 unity_DOTS_Sampled_NormalMap_ST;
static float4 unity_DOTS_Sampled_MatCap_Sampler_ST;
static float4 unity_DOTS_Sampled_Emissive_Tex_ST;
static float4 unity_DOTS_Sampled_BaseMap_ST;
static float4 unity_DOTS_Sampled_BaseColor;
static float4 unity_DOTS_Sampled_SpecColor;
static float4 unity_DOTS_Sampled_Emissive_Color;
static float unity_DOTS_Sampled_EndFadeDistance;
static float unity_DOTS_Sampled_StartFadeDistance;
static float unity_DOTS_Sampled_FadeDistance;
static float unity_DOTS_Sampled_Clipping_Level;
static float unity_DOTS_Sampled_Tweak_transparency;
static int unity_DOTS_Sampled_MainTexArr_ID;
static int unity_DOTS_Sampled_NormalMapArr_ID;
static int unity_DOTS_Sampled_MatCap_SamplerArr_ID;
static int unity_DOTS_Sampled_Emissive_TexArr_ID;
static int unity_DOTS_Sampled_MetallicGlossMapArr_ID;
static int unity_DOTS_Sampled_lastWearableVertCount; 
static int unity_DOTS_Sampled_lastAvatarVertCount;


void SetupDOTSToonMaterialPropertyCaches()
{
    unity_DOTS_Sampled_MainTex_ST 					= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _MainTex_ST); 
    unity_DOTS_Sampled_NormalMap_ST 				= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _NormalMap_ST); 
    unity_DOTS_Sampled_MatCap_Sampler_ST 			= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _MatCap_Sampler_ST); 
    unity_DOTS_Sampled_Emissive_Tex_ST 				= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _Emissive_Tex_ST); 
    unity_DOTS_Sampled_BaseMap_ST 					= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseMap_ST); 
    unity_DOTS_Sampled_BaseColor 					= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor); 
    unity_DOTS_Sampled_SpecColor 					= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _SpecColor); 
    unity_DOTS_Sampled_Emissive_Color 				= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _Emissive_Color); 
    unity_DOTS_Sampled_EndFadeDistance 				= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _EndFadeDistance); 
    unity_DOTS_Sampled_StartFadeDistance 			= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _StartFadeDistance); 
    unity_DOTS_Sampled_FadeDistance 				= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _FadeDistance); 
    unity_DOTS_Sampled_Clipping_Level 				= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Clipping_Level); 
    unity_DOTS_Sampled_Tweak_transparency 			= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Tweak_transparency); 
    unity_DOTS_Sampled_MainTexArr_ID 				= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(int, _MainTexArr_ID); 
    unity_DOTS_Sampled_NormalMapArr_ID 				= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(int, _NormalMapArr_ID); 
    unity_DOTS_Sampled_MatCap_SamplerArr_ID 		= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(int, _MatCap_SamplerArr_ID); 
    unity_DOTS_Sampled_Emissive_TexArr_ID 			= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(int, _Emissive_TexArr_ID); 
    unity_DOTS_Sampled_MetallicGlossMapArr_ID 		= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(int, _MetallicGlossMapArr_ID); 
    unity_DOTS_Sampled_lastWearableVertCount 		= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(int, _lastWearableVertCount); 
    unity_DOTS_Sampled_lastAvatarVertCount 			= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(int, _lastAvatarVertCount); 
}

#undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
#define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupDOTSToonMaterialPropertyCaches()

#define _MainTex_ST                         unity_DOTS_Sampled_MainTex_ST
#define _NormalMap_ST                       unity_DOTS_Sampled_NormalMap_ST
#define _MatCap_Sampler_ST                  unity_DOTS_Sampled_MatCap_Sampler_ST
#define _Emissive_Tex_ST                    unity_DOTS_Sampled_Emissive_Tex_ST
#define _BaseMap_ST                         unity_DOTS_Sampled_BaseMap_ST
#define _BaseColor                          unity_DOTS_Sampled_BaseColor
#define _SpecColor                          unity_DOTS_Sampled_SpecColor
#define _Emissive_Color                     unity_DOTS_Sampled_Emissive_Color
#define _EndFadeDistance                    unity_DOTS_Sampled_EndFadeDistance
#define _StartFadeDistance                  unity_DOTS_Sampled_StartFadeDistance
#define _FadeDistance                       unity_DOTS_Sampled_FadeDistance
#define _Clipping_Level                     unity_DOTS_Sampled_Clipping_Level
#define _Tweak_transparency                 unity_DOTS_Sampled_Tweak_transparency
#define _MainTexArr_ID                      unity_DOTS_Sampled_MainTexArr_ID
#define _NormalMapArr_ID                    unity_DOTS_Sampled_NormalMapArr_ID
#define _MatCap_SamplerArr_ID               unity_DOTS_Sampled_MatCap_SamplerArr_ID
#define _Emissive_TexArr_ID                 unity_DOTS_Sampled_Emissive_TexArr_ID
#define _MetallicGlossMapArr_ID             unity_DOTS_Sampled_MetallicGlossMapArr_ID
#define _lastWearableVertCount              unity_DOTS_Sampled_lastWearableVertCount 
#define _lastAvatarVertCount                unity_DOTS_Sampled_lastAvatarVertCount
#endif

#ifdef _DCL_TEXTURE_ARRAYS
    #define DCL_DECLARE_TEX2DARRAY(tex) Texture2DArray tex; SamplerState sampler##tex
    #define DCL_SAMPLE_TEX2DARRAY(tex,coord) tex.Sample (sampler##tex,coord)
    #define DCL_DECLARE_TEX2DARRAY_DEFAULT_SAMPLER(tex) Texture2DArray tex;
    #define DCL_SAMPLE_TEX2DARRAY_DEFAULT_SAMPLER(tex,coord) tex.Sample (sampler_MainTexArr,coord)
    #define DCL_SAMPLE_TEX2DARRAY_LOD(tex,coord,lod) tex.SampleLevel (sampler##tex,coord,lod)

    DCL_DECLARE_TEX2DARRAY(_BumpMapArr);
    DCL_DECLARE_TEX2DARRAY(_EmissionMapArr);
    DCL_DECLARE_TEX2DARRAY(_MainTexArr);

    DCL_DECLARE_TEX2DARRAY_DEFAULT_SAMPLER(_NormalMapArr);
    DCL_DECLARE_TEX2DARRAY(_MatCap_SamplerArr);
    DCL_DECLARE_TEX2DARRAY(_Emissive_TexArr);

    DCL_DECLARE_TEX2DARRAY(_OcclusionMapArr);
    DCL_DECLARE_TEX2DARRAY(_MetallicGlossMapArr);


    #define SAMPLE_BUMPMAP(uv,texArrayID)                       DCL_SAMPLE_TEX2DARRAY(_BumpMapArr, float3(uv, texArrayID))
    #define SAMPLE_EMISSIONMAP(uv,texArrayID)                   DCL_SAMPLE_TEX2DARRAY(_EmissionMapArr, float3(uv, texArrayID))
    #define SAMPLE_MAINTEX(uv,texArrayID)                       DCL_SAMPLE_TEX2DARRAY_DEFAULT_SAMPLER(_MainTexArr, float3(uv, texArrayID))
    #define SAMPLE_NORMALMAP(uv,texArrayID)                     float4(0.5f, 0.5f, 1.0f, 1.0f)
    // #define SAMPLE_MATCAP(uv,texArrayID,lod)                    DCL_SAMPLE_TEX2DARRAY_LOD(_MatCap_SamplerArr, float3(uv, texArrayID), lod)
    #define SAMPLE_MATCAP(uv,texArrayID,lod)                    float4(0.0f, 0.0f, 0.0f, 0.0f)
    #define SAMPLE_EMISSIVE(uv,texArrayID)                      DCL_SAMPLE_TEX2DARRAY(_Emissive_TexArr, float3(uv, texArrayID))
    #define SAMPLE_OCCLUSIONMAP(uv,texArrayID)                  DCL_SAMPLE_TEX2DARRAY(_OcclusionMapArr, float3(uv, texArrayID))
    #define SAMPLE_METALLICGLOSS(uv,texArrayID)                 DCL_SAMPLE_TEX2DARRAY(_MetallicGlossMapArr, float3(uv, texArrayID))
#else
    TEXTURE2D(_BaseMap);                SAMPLER(sampler_BaseMap);
    TEXTURE2D(_BumpMap);                SAMPLER(sampler_BumpMap);
    TEXTURE2D(_MainTex);                SAMPLER(sampler_MainTex);
    TEXTURE2D(_NormalMap);
    TEXTURE2D(_MetallicGlossMap);       SAMPLER(sampler_MetallicGlossMap);

    sampler2D _MatCap_Sampler;
    sampler2D _Emissive_Tex;    

    #define SAMPLE_BASEMAP(uv,texArrayID)                   SAMPLE_TEXTURE2D(_BaseMap,                  sampler_BaseMap, uv)
    #define SAMPLE_BUMPMAP(uv,texArrayID)                   SAMPLE_TEXTURE2D(_BumpMap,                  sampler_BumpMap, uv)
    #define SAMPLE_EMISSIONMAP(uv,texArrayID)               float4(0.0f, 0.0f, 0.0f, 0.0f)
    #define SAMPLE_MAINTEX(uv,texArrayID)                   SAMPLE_TEXTURE2D(_MainTex,                  sampler_MainTex, uv)
    #define SAMPLE_NORMALMAP(uv,texArrayID)                 SAMPLE_TEXTURE2D(_NormalMap,                sampler_MainTex, uv)
    #define SAMPLE_OCCLUSIONMAP(uv,texArrayID)              float4(0.0f, 0.0f, 0.0f, 0.0f)
    #define SAMPLE_METALLICGLOSS(uv,texArrayID)             SAMPLE_TEXTURE2D(_MetallicGlossMap,         sampler_MetallicGlossMap, uv)
    #define SAMPLE_MATCAP(uv,texArrayID,lod)                tex2Dlod(_MatCap_Sampler,           float4(uv, 0.0f, lod))
    #define SAMPLE_EMISSIVE(uv,texArrayID)                  tex2D(_Emissive_Tex,                uv)
#endif

#include "DCL_Toon_SurfaceInput.hlsl"

half4 SampleMetallicSpecGloss(float2 uv, half albedoAlpha)
{
    half4 specGloss;

    #ifdef _METALLICSPECGLOSSMAP
        int nMetallicGlossMapArrID = _MetallicGlossMapArr_ID;
        specGloss = SAMPLE_METALLICGLOSS(uv, nMetallicGlossMapArrID);
        #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            specGloss.a = albedoAlpha * _Smoothness;
        #else
            specGloss.a *= _Smoothness;
        #endif
    #else // _METALLICSPECGLOSSMAP
        #if _SPECULAR_SETUP
            specGloss.rgb = _SpecColor.rgb;
        #else
            specGloss.rgb = _Metallic.rrr;
        #endif

        #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            specGloss.a = albedoAlpha * _Smoothness;
        #else
            specGloss.a = _Smoothness;
        #endif
    #endif

    return specGloss;
}

half SampleOcclusion(float2 uv)
{
    #ifdef _OCCLUSIONMAP
        // TODO: Controls things like these by exposing SHADER_QUALITY levels (low, medium, high)
        #if defined(SHADER_API_GLES)
            int nOcclusionMapArrID = _OcclusionMapArr_ID;
            return SAMPLE_OCCLUSIONMAP(uv, nOcclusionMapArrID).g;
        #else
            int nOcclusionMapArrID = _OcclusionMapArr_ID;
            half occ = SAMPLE_OCCLUSIONMAP(uv, nOcclusionMapArrID).g;
            return LerpWhiteTo(occ, _OcclusionStrength);
        #endif
    #else
        return 1.0;
    #endif
}

inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    half4 albedoAlpha = SampleAlbedoAlpha(uv);
    outSurfaceData.alpha = Alpha(albedoAlpha.a, _BaseColor, _Cutoff);
    
    half4 specGloss = SampleMetallicSpecGloss(uv, albedoAlpha.a);
    outSurfaceData.albedo = albedoAlpha.rgb * _BaseColor.rgb;

    #if _SPECULAR_SETUP
        outSurfaceData.metallic = 1.0h;
        outSurfaceData.specular = specGloss.rgb;
    #else
        outSurfaceData.metallic = specGloss.r;
        outSurfaceData.specular = half3(0.0h, 0.0h, 0.0h);
    #endif

    outSurfaceData.smoothness = specGloss.a;
    outSurfaceData.normalTS = SampleNormal(uv, _BumpScale);
    outSurfaceData.occlusion = SampleOcclusion(uv);
    outSurfaceData.emission = SampleEmission(uv, _EmissionColor.rgb);
}

// Returns true if AlphaToMask functionality is currently available
// NOTE: This does NOT guarantee that AlphaToMask is enabled for the current draw. It only indicates that AlphaToMask functionality COULD be enabled for it.
//       In cases where AlphaToMask COULD be enabled, we export a specialized alpha value from the shader.
//       When AlphaToMask is enabled:     The specialized alpha value is combined with the sample mask
//       When AlphaToMask is not enabled: The specialized alpha value is either written into the framebuffer or dropped entirely depending on the color write mask
bool IsAlphaToMaskAvailable()
{
    return (_AlphaToMaskAvailable != 0.0);
}

// When AlphaToMask is available:     Returns a modified alpha value that should be exported from the shader so it can be combined with the sample mask
// When AlphaToMask is not available: Terminates the current invocation if the alpha value is below the cutoff and returns the input alpha value otherwise

half AlphaClip(half alpha, half cutoff)
{
    // Produce 0.0 if the input value would be clipped by traditional alpha clipping and produce the original input value otherwise.
    // WORKAROUND: The alpha parameter in this ternary expression MUST be converted to a float in order to work around a known HLSL compiler bug.
    //             See Fogbugz 934464 for more information
    half clippedAlpha = (alpha >= cutoff) ? float(alpha) : 0.0;

    // Calculate a specialized alpha value that should be used when alpha-to-coverage is enabled

    // If the user has specified zero as the cutoff threshold, the expectation is that the shader will function as if alpha-clipping was disabled.
    // Ideally, the user should just turn off the alpha-clipping feature in this case, but in order to make this case work as expected, we force alpha
    // to 1.0 here to ensure that alpha-to-coverage never throws away samples when its active. (This would cause opaque objects to appear transparent)
    half alphaToCoverageAlpha = (cutoff <= 0.0) ? 1.0 : SharpenAlpha(alpha, cutoff);

    // When alpha-to-coverage is available:     Use the specialized value which will be exported from the shader and combined with the MSAA coverage mask.
    // When alpha-to-coverage is not available: Use the "clipped" value. A clipped value will always result in thread termination via the clip() logic below.
    alpha = IsAlphaToMaskAvailable() ? alphaToCoverageAlpha : clippedAlpha;

    // Terminate any threads that have an alpha value of 0.0 since we know they won't contribute anything to the final image
    clip(alpha - 0.0001);

    return alpha;
}

#endif // DCL_TOON_INPUT_INCLUDED
