#ifndef UNIVERSAL_UNLIT_INPUT_INCLUDED
#define UNIVERSAL_UNLIT_INPUT_INCLUDED

#include "SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half _Cutoff;
    half _Surface;
    float4 _AlphaTexture_ST;
    float4 _PlaneClipping;
    float4 _VerticalClipping;
    UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

#ifdef UNITY_DOTS_INSTANCING_ENABLED
UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DOTS_INSTANCED_PROP(float , _Cutoff)
    UNITY_DOTS_INSTANCED_PROP(float , _Surface)
    UNITY_DOTS_INSTANCED_PROP(float4, _AlphaTexture_ST)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

UNITY_DOTS_INSTANCING_START(UserPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP(float4, _PlaneClipping)
    UNITY_DOTS_INSTANCED_PROP(float4, _VerticalClipping)
UNITY_DOTS_INSTANCING_END(UserPropertyMetadata)

static float4 unity_DOTS_Sampled_BaseColor;
static float  unity_DOTS_Sampled_Cutoff;
static float  unity_DOTS_Sampled_Surface;
static float4  unity_DOTS_Sampled_AlphaTexture_ST;
static float4 unity_DOTS_Sampled_PlaneClipping;
static float4 unity_DOTS_Sampled_VerticalClipping;

void SetupDOTSUnlitMaterialPropertyCaches()
{
    unity_DOTS_Sampled_BaseColor                = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor);
    unity_DOTS_Sampled_Cutoff                   = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Cutoff);
    unity_DOTS_Sampled_Surface                  = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Surface);
    unity_DOTS_Sampled_AlphaTexture_ST          = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4 , _AlphaTexture_ST);
    unity_DOTS_Sampled_PlaneClipping            = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4 , _PlaneClipping);
    unity_DOTS_Sampled_VerticalClipping         = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4 , _VerticalClipping);
}

#undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
#define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupDOTSUnlitMaterialPropertyCaches()

#define _BaseColor                  unity_DOTS_Sampled_BaseColor
#define _Cutoff                     unity_DOTS_Sampled_Cutoff
#define _Surface                    unity_DOTS_Sampled_Surface
#define _AlphaTexture_ST            unity_DOTS_Sampled_AlphaTexture_ST
#define _PlaneClipping              unity_DOTS_Sampled_PlaneClipping
#define _VerticalClipping           unity_DOTS_Sampled_VerticalClipping

#endif

#endif
