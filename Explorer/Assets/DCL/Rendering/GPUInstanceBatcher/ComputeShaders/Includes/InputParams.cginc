#ifndef _DCL_INPUT_PARAMS_
#define _DCL_INPUT_PARAMS_

// instance data
struct PerInstance
{
    float4x4 worldMatrix;
    float3 colour;
};

RWStructuredBuffer<PerInstance> PerInstanceData;
uniform uint nPerInstanceBufferSize;

// bounds
uniform float3 vBoundsCenter;
uniform float3 vBoundsExtents;

// camera data
uniform float4x4 matCamera_MVP;
uniform float3 vCameraPosition;
uniform float fCameraHalfAngle;

// global culling
uniform float minCullingDistance;

// distance culling
uniform float fMaxDistance;

//frustum culling
uniform bool isFrustumCulling;
uniform float frustumOffset;

// occlusion culling
uniform bool isOcclusionCulling;
uniform float occlusionOffset;
uniform uint occlusionAccuracy;
uniform float4 hiZTxtrSize;
uniform Texture2D<float4> hiZMap;
uniform SamplerState sampler_hiZMap; // variable name is recognized by the compiler to reference hiZMap

// shadows
uniform bool cullShadows;
uniform float fShadowDistance;
uniform float4x4 shadowLODMap;

// LOD
uniform float4x4 lodSizes;
uniform float deltaTime;

#endif
