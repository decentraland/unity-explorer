#ifndef SCENE_DEPTH_ONLY_PASS_INCLUDED
#define SCENE_DEPTH_ONLY_PASS_INCLUDED

#include "Scene_Dither.hlsl"
#include "Scene_InputData.hlsl"

#include "Scene_InputData.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Scene_PlaneClipping.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

#ifdef _GPU_INSTANCER_BATCHER
#define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
#include "UnityIndirect.cginc"
#endif

struct Attributes
{
    float4 position     : POSITION;
    float2 texcoord     : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS   : SV_POSITION;
    float3 positionWS   : TEXCOORD0;
    float2 uv           : TEXCOORD1;
    uint nDither        : TEXCOORD2;

    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings DepthOnlyVertex(Attributes input, uint svInstanceID : SV_InstanceID)
{
    #ifdef _GPU_INSTANCER_BATCHER
    InitIndirectDrawArgs(0);
    #endif
    
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    #ifdef _GPU_INSTANCER_BATCHER
    uint instanceID = GetIndirectInstanceID_Base(svInstanceID);
    output.nDither = _PerInstanceLookUpAndDitherBuffer[instanceID].ditherLevel;
    #else
    output.nDither = 0;
    #endif

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.positionCS = TransformObjectToHClip_Scene(input.position.xyz, svInstanceID);
    output.positionWS = TransformObjectToWorld_Scene(input.position.xyz, svInstanceID);
    return output;
}

half DepthOnlyFragment(Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    Dithering( input.positionCS, input.nDither);

    ClipFragmentViaPlaneTests(input.positionWS, _PlaneClipping.x, _PlaneClipping.y, _PlaneClipping.z, _PlaneClipping.w, _VerticalClipping.x, _VerticalClipping.y);

    Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif

    return input.positionCS.z;
}
#endif
