#ifndef AVATAR_DEPTH_NORMALS_PASS_INCLUDED
#define AVATAR_DEPTH_NORMALS_PASS_INCLUDED

#include "Avatar_CelShading_Core.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif
#include "Avatar_CelShading_RealtimeLights.hlsl"

// Skinning structure
struct VertexInfo
{
    float3 position;
    float3 normal;
    float4 tangent;
};
StructuredBuffer<VertexInfo> _GlobalAvatarBuffer;

struct Attributes
{
    uint   index            : SV_VertexID;
    float4 positionOS       : POSITION;
    float4 tangentOS        : TANGENT;
    float2 texcoord         : TEXCOORD0;
    float3 normal           : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS   : SV_POSITION;
    float2 uv           : TEXCOORD1;
    float4 normalWS     : TEXCOORD2;

    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings DepthNormalsVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    //output.uv         = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.positionCS = TransformObjectToHClip(_GlobalAvatarBuffer[_lastAvatarVertCount + _lastWearableVertCount + input.index].position.xyz);

    VertexNormalInputs normalInput = GetVertexNormalInputs(_GlobalAvatarBuffer[_lastAvatarVertCount + _lastWearableVertCount + input.index].normal.xyz, _GlobalAvatarBuffer[_lastAvatarVertCount + _lastWearableVertCount + input.index].tangent.xyzw);
    
    output.normalWS.xyz = NormalizeNormalPerVertex(normalInput.normalWS);
    output.normalWS.w = -(mul(UNITY_MATRIX_V, mul(unity_ObjectToWorld, float4(input.positionOS.xyz, 1.0))).z * _ProjectionParams.w);

    return output;
}

// Copied from ShaderVariablesFunctions.hlsl
float3 NormalizeNormalPerPixel_Avatar(float3 normalWS)
{
    #if defined(UNITY_NO_DXT5nm) && defined(_NORMALMAP)
        return SafeNormalize(normalWS);
    #else
        return normalize(normalWS);
    #endif
}

void DepthNormalsFragment(
    Varyings input
    , out half4 outNormalWS : SV_Target0
    #ifdef _WRITE_RENDERING_LAYERS
        , out float4 outRenderingLayers : SV_Target1
    #endif
)
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    //Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);

    #ifdef LOD_FADE_CROSSFADE
        LODFadeCrossFade(input.positionCS);
    #endif

    #if defined(_GBUFFER_NORMALS_OCT)
    float3 normalWS = normalize(input.normalWS);
    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);           // values between [-1, +1], must use fp32 on some platforms.
    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);   // values between [ 0,  1]
    half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);      // values between [ 0,  1]
    outNormalWS = half4(packedNormalWS, 0.0);
    #else
    float3 normalWS = (NormalizeNormalPerPixel_Avatar(input.normalWS.xyz) + 1.0f) * 0.5f;
    outNormalWS = half4(normalWS.xyz, input.normalWS.w);
    #endif

    #ifdef _WRITE_RENDERING_LAYERS
        uint renderingLayers = GetMeshRenderingLayer();
        outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
    #endif
}
#endif
