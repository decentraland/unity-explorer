#ifndef SCENE_INPUT_INCLUDED
#define SCENE_INPUT_INCLUDED

#include "Scene_InputData.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ParallaxMapping.hlsl"
#include "Scene_SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"

// NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
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

void SetupDOTSSceneMaterialPropertyCaches()
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
}

#undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
#define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupDOTSSceneMaterialPropertyCaches()

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

#endif

TEXTURE2D(_ParallaxMap);        SAMPLER(sampler_ParallaxMap);
//TEXTURE2D(_OcclusionMap);       SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_MetallicGlossMap);   SAMPLER(sampler_MetallicGlossMap);

#define SAMPLE_METALLICSPECULAR(uv) SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv)

#ifdef _GPU_INSTANCER_BATCHER
struct PerInstanceBuffer
{
    float4x4 instMatrix;
    float4 instColourTint;
    float2 instTiling;
    float2 instOffset;
};
StructuredBuffer<PerInstanceBuffer> _PerInstanceBuffer;

struct PerInstanceLookUpAndDither
{
    uint instanceID;
    uint ditherLevel;
    uint padding0;
    uint padding1;
};
StructuredBuffer<PerInstanceLookUpAndDither> _PerInstanceLookUpAndDitherBuffer;
#endif

half4 SampleMetallicSpecGloss(float2 uv, half albedoAlpha)
{
    half4 specGloss;
	if (_METALLICSPECGLOSSMAP)
	{
	    specGloss = half4(SAMPLE_METALLICSPECULAR(uv));
	    //ARM Texture - Provides Height in R, Metallic in B and Roughness in G
	    specGloss.g = 1.0 - specGloss.g; //Conversion from RoughnessToSmoothness
	    specGloss.b *= _Metallic;
	}
    else // _METALLICSPECGLOSSMAP
    {
        specGloss.r = 0.0;
        specGloss.g = _Smoothness;
        specGloss.b = _Metallic;
        specGloss.a = 0.0;
    }

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
    if (_PARALLAXMAP) // using HRM texture, so RGB == Height, Roughness, Metallic
    {
        uv += ParallaxMapping(TEXTURE2D_ARGS(_MetallicGlossMap, sampler_MetallicGlossMap), viewDirTS, _Parallax, uv);
    }
}

inline void InitializeStandardLitSurfaceData_Scene(float2 uv, float4 _PerInstanceColour, out SurfaceData_Scene outSurfaceData)
{
    half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    outSurfaceData.alpha = Alpha(albedoAlpha.a, _BaseColor * _PerInstanceColour, _Cutoff);
    outSurfaceData.albedo = AlphaModulate(albedoAlpha.rgb * _BaseColor.rgb * _PerInstanceColour.rgb, outSurfaceData.alpha);

    half4 specGloss = SampleMetallicSpecGloss(uv, albedoAlpha.a);
    outSurfaceData.metallic = specGloss.b;
    outSurfaceData.smoothness = specGloss.g;
    
    outSurfaceData.normalTS = SampleNormal_Scene(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    outSurfaceData.occlusion = SampleOcclusion(uv);
    outSurfaceData.emission = SampleEmission_Scene(uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));
    outSurfaceData.height = specGloss.r;
}

#ifdef _GPU_INSTANCER_BATCHER
#define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
#include "UnityIndirect.cginc"
#endif

#ifdef _GPU_INSTANCER_BATCHER
float3 TransformObjectToWorld_PerInstance(float3 positionOS, uint _instanceID)
{
    #if defined(SHADER_STAGE_RAY_TRACING)
    return mul(ObjectToWorld3x4(), float4(positionOS, 1.0)).xyz;
    #else
    uint instID = _PerInstanceLookUpAndDitherBuffer[_instanceID].instanceID;
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

float4 TransformObjectToHClip_Scene(float3 _positionOS, uint _svInstanceID)
{
    #ifdef _GPU_INSTANCER_BATCHER
    uint cmdID = GetCommandID(0);
    uint instanceID = GetIndirectInstanceID_Base(_svInstanceID);
    uint instID = _PerInstanceLookUpAndDitherBuffer[instanceID].instanceID;
    return mul(GetWorldToHClipMatrix(), mul(_PerInstanceBuffer[instID].instMatrix, float4(_positionOS, 1.0)));
    #else
    return TransformObjectToHClip(_positionOS);
    #endif
}

float3 TransformObjectToWorld_Scene(float3 _positionOS, uint _svInstanceID)
{
    #ifdef _GPU_INSTANCER_BATCHER
    uint cmdID = GetCommandID(0);
    uint instanceID = GetIndirectInstanceID_Base(_svInstanceID);
    uint instID = _PerInstanceLookUpAndDitherBuffer[instanceID].instanceID;
    return mul(_PerInstanceBuffer[instID].instMatrix, float4(_positionOS, 1.0)).xyz;
    #else
    return TransformObjectToWorld(_positionOS);
    #endif
}

float3 TransformObjectToWorldDir_Scene(float3 dirOS, uint _svInstanceID, bool doNormalize = true)
{
    #ifdef _GPU_INSTANCER_BATCHER
        uint cmdID = GetCommandID(0);
        uint instanceID = GetIndirectInstanceID_Base(_svInstanceID);
        uint instID = _PerInstanceLookUpAndDitherBuffer[instanceID].instanceID;
        float4x4 ObjToWorldMatrix = _PerInstanceBuffer[instID].instMatrix;
        float3 dirWS = mul((float3x3)ObjToWorldMatrix, dirOS);
    #else
        #ifndef SHADER_STAGE_RAY_TRACING
            float3 dirWS = mul((float3x3)GetObjectToWorldMatrix(), dirOS);
        #else
            float3 dirWS = mul((float3x3)ObjectToWorld3x4(), dirOS);
        #endif
    #endif
    
    if (doNormalize)
        return SafeNormalize(dirWS);
    return dirWS;
}

float4x4 inverse(float4x4 m) {
    float n11 = m[0][0], n12 = m[1][0], n13 = m[2][0], n14 = m[3][0];
    float n21 = m[0][1], n22 = m[1][1], n23 = m[2][1], n24 = m[3][1];
    float n31 = m[0][2], n32 = m[1][2], n33 = m[2][2], n34 = m[3][2];
    float n41 = m[0][3], n42 = m[1][3], n43 = m[2][3], n44 = m[3][3];

    float t11 = n23 * n34 * n42 - n24 * n33 * n42 + n24 * n32 * n43 - n22 * n34 * n43 - n23 * n32 * n44 + n22 * n33 * n44;
    float t12 = n14 * n33 * n42 - n13 * n34 * n42 - n14 * n32 * n43 + n12 * n34 * n43 + n13 * n32 * n44 - n12 * n33 * n44;
    float t13 = n13 * n24 * n42 - n14 * n23 * n42 + n14 * n22 * n43 - n12 * n24 * n43 - n13 * n22 * n44 + n12 * n23 * n44;
    float t14 = n14 * n23 * n32 - n13 * n24 * n32 - n14 * n22 * n33 + n12 * n24 * n33 + n13 * n22 * n34 - n12 * n23 * n34;

    float det = n11 * t11 + n21 * t12 + n31 * t13 + n41 * t14;
    float idet = 1.0f / det;

    float4x4 ret;

    ret[0][0] = t11 * idet;
    ret[0][1] = (n24 * n33 * n41 - n23 * n34 * n41 - n24 * n31 * n43 + n21 * n34 * n43 + n23 * n31 * n44 - n21 * n33 * n44) * idet;
    ret[0][2] = (n22 * n34 * n41 - n24 * n32 * n41 + n24 * n31 * n42 - n21 * n34 * n42 - n22 * n31 * n44 + n21 * n32 * n44) * idet;
    ret[0][3] = (n23 * n32 * n41 - n22 * n33 * n41 - n23 * n31 * n42 + n21 * n33 * n42 + n22 * n31 * n43 - n21 * n32 * n43) * idet;

    ret[1][0] = t12 * idet;
    ret[1][1] = (n13 * n34 * n41 - n14 * n33 * n41 + n14 * n31 * n43 - n11 * n34 * n43 - n13 * n31 * n44 + n11 * n33 * n44) * idet;
    ret[1][2] = (n14 * n32 * n41 - n12 * n34 * n41 - n14 * n31 * n42 + n11 * n34 * n42 + n12 * n31 * n44 - n11 * n32 * n44) * idet;
    ret[1][3] = (n12 * n33 * n41 - n13 * n32 * n41 + n13 * n31 * n42 - n11 * n33 * n42 - n12 * n31 * n43 + n11 * n32 * n43) * idet;

    ret[2][0] = t13 * idet;
    ret[2][1] = (n14 * n23 * n41 - n13 * n24 * n41 - n14 * n21 * n43 + n11 * n24 * n43 + n13 * n21 * n44 - n11 * n23 * n44) * idet;
    ret[2][2] = (n12 * n24 * n41 - n14 * n22 * n41 + n14 * n21 * n42 - n11 * n24 * n42 - n12 * n21 * n44 + n11 * n22 * n44) * idet;
    ret[2][3] = (n13 * n22 * n41 - n12 * n23 * n41 - n13 * n21 * n42 + n11 * n23 * n42 + n12 * n21 * n43 - n11 * n22 * n43) * idet;

    ret[3][0] = t14 * idet;
    ret[3][1] = (n13 * n24 * n31 - n14 * n23 * n31 + n14 * n21 * n33 - n11 * n24 * n33 - n13 * n21 * n34 + n11 * n23 * n34) * idet;
    ret[3][2] = (n14 * n22 * n31 - n12 * n24 * n31 - n14 * n21 * n32 + n11 * n24 * n32 + n12 * n21 * n34 - n11 * n22 * n34) * idet;
    ret[3][3] = (n12 * n23 * n31 - n13 * n22 * n31 + n13 * n21 * n32 - n11 * n23 * n32 - n12 * n21 * n33 + n11 * n22 * n33) * idet;

    return ret;
}


float3 TransformObjectToWorldNormal_Scene(float3 normalOS, uint _svInstanceID, bool doNormalize = true)
{
    #ifdef UNITY_ASSUME_UNIFORM_SCALING
        return TransformObjectToWorldDir_Scene(normalOS, doNormalize);
    #else
        // Normal need to be multiply by inverse transpose
        #ifdef _GPU_INSTANCER_BATCHER
            uint cmdID = GetCommandID(0);
            uint instanceID = GetIndirectInstanceID_Base(_svInstanceID);
            uint instID = _PerInstanceLookUpAndDitherBuffer[instanceID].instanceID;
            float4x4 ObjToWorldMatrix = _PerInstanceBuffer[instID].instMatrix;
            float3 normalWS = mul(normalOS, (float3x3)inverse(ObjToWorldMatrix));
        #else
            float3 normalWS = mul(normalOS, (float3x3)GetWorldToObjectMatrix());
        #endif
        if (doNormalize)
            return SafeNormalize(normalWS);

        return normalWS;
    #endif
}

VertexPositionInputs GetVertexPositionInputs_Scene(float3 _positionOS, uint _svInstanceID)
{
    #ifdef _GPU_INSTANCER_BATCHER
    uint cmdID = GetCommandID(0);
    uint instanceID = GetIndirectInstanceID_Base(_svInstanceID);
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

float2 TransformTex_PerInstance(float2 uv, uint _svInstanceID)
{
    float2 uv_trans = uv;
    #ifdef _GPU_INSTANCER_BATCHER
        uint instID = _PerInstanceLookUpAndDitherBuffer[_svInstanceID].instanceID;
        float2 uv_tiling = _PerInstanceBuffer[instID].instTiling;
        float2 uv_offset = _PerInstanceBuffer[instID].instOffset;
        uv_trans = (uv_trans * uv_tiling) + uv_offset;
    #endif
    return uv_trans;
}

#endif // UNIVERSAL_INPUT_SURFACE_PBR_INCLUDED
