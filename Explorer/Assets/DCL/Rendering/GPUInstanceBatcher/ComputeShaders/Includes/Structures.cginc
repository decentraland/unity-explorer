#ifndef _DCL_STRUCTURES_
#define _DCL_STRUCTURES_

struct PerInstance
{
    float4x4 worldMatrix;
    float4 colour;
    float2 tiling;
    float2 offset;
};

struct PerInstanceLODLevels
{
    uint LOD_A;
    uint LOD_B;
    uint LOD_Dither;
    uint LOD_Shadow;
};

// matLODSizes
// first 8 are the screenspace size start of each LOD
// second 8 are the screenspace size end of each LOD
// LOD Number reference
// [0, 1, 2, 3]
// [4, 5, 6, 7]
// [0, 1, 2, 3]
// [4, 5, 6, 7]

// Packed size 192 with 4 byte group
struct GroupData
{
    float4x4 matLODSizes;
    float4x4 matCamera_MVP;
    float3 vCameraPosition;
    float fShadowDistance;
    float3 vBoundsCenter;
    float fFrustumOffset;
    float3 vBoundsExtents;
    float fCameraHalfAngle;
    float fMaxDistance;
    float fMinCullingDistance;
    uint nInstBufferSize;
    uint nLODCount;
};

struct InstanceLookUpAndDither
{
    uint nInstanceLookUp;
    uint nDither;
    uint nPadding0;
    uint nPadding1;
};

// TODO: Move into a read only structured buffer
// Occlusion
// uniform bool isOcclusionCulling;
// uniform float occlusionOffset;
// uniform uint occlusionAccuracy;
// uniform float4 hiZTxtrSize;
// uniform Texture2D<float4> hiZMap;
// uniform SamplerState sampler_hiZMap;

#endif