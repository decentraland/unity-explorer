// GPU Instancer Pro — clean-room replacement.
//
// Shaders that have `#pragma instancing_options procedural:setupGPUI` end up
// calling our setupGPUI() once per vertex (and once per fragment where the
// pass transfers the instance id), and we need to populate
// unity_ObjectToWorld / unity_WorldToObject from a per-instance buffer.
//
// The C# side (TreeRendererService) binds, for every draw:
//   gpuiTransformBuffer        — float4x4, instance world matrix
//   gpuiInverseTransformBuffer — float4x4, inverse of the above
//   gpuiLODFadeBuffer          — float, LOD cross-fade factor per instance;
//     read only by LOD_FADE_CROSSFADE variants, which are used for the
//     cross-fading draws alone. Positive for the LOD fading out, negative for
//     the LOD fading in, matching what URP's LODFadeCrossFade expects in
//     unity_LODFade.x.
//   gpuiInstanceOffset — first element of this draw's range inside those
//     buffers; every draw of a prototype shares one flat upload.
//   gpuiPrototypeLocalTransform / gpuiPrototypeLocalInverseTransform —
//     the drawn renderer's local-to-prototype-root matrix (and inverse).
//     Prototype prefabs may scale/offset their LOD renderers relative to the
//     root (e.g. rock FBXs authored in centimetres carry a 100x child scale),
//     and the instance matrices place the ROOT, so the mesh needs both.
// Buffers are indexed by unity_InstanceID plus the draw's offset; the
// inverses are precomputed on the C# side alongside the transforms, so no 4x4
// inverse runs per vertex.
//
// All bodies are guarded with UNITY_PROCEDURAL_INSTANCING_ENABLED so a shader
// using this header can still compile and render the non-instanced path.

#ifndef GPUI_INSTANCER_SETUP_INCLUDED
#define GPUI_INSTANCER_SETUP_INCLUDED

#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
StructuredBuffer<float4x4> gpuiTransformBuffer;
StructuredBuffer<float4x4> gpuiInverseTransformBuffer;
StructuredBuffer<float> gpuiLODFadeBuffer;
float4x4 gpuiPrototypeLocalTransform;
float4x4 gpuiPrototypeLocalInverseTransform;
int gpuiInstanceOffset;
#endif

void setupGPUI()
{
#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
    // unity_InstanceID includes the batch's base instance (UnitySetupInstanceID adds
    // unity_BaseInstanceID so Unity's own per-draw arrays, suballocated at that base in
    // a shared buffer, index correctly). Our buffers are indexed from the draw's own
    // offset, so the base must be stripped back out — with it left in, every non-first
    // batched draw reads past its range into another draw's instances.
    uint gpuiIndex = (uint)(unity_InstanceID - unity_BaseInstanceID + gpuiInstanceOffset);
    unity_ObjectToWorld = mul(gpuiTransformBuffer[gpuiIndex], gpuiPrototypeLocalTransform);
    unity_WorldToObject = mul(gpuiPrototypeLocalInverseTransform, gpuiInverseTransformBuffer[gpuiIndex]);
    #if defined(LOD_FADE_CROSSFADE) && !defined(UNITY_DOTS_INSTANCING_ENABLED)
    float gpuiFade = gpuiLODFadeBuffer[gpuiIndex];
    unity_LODFade = float4(gpuiFade, sign(gpuiFade) * floor(abs(gpuiFade) * 16.0) / 16.0, 0.0, 0.0);
    #endif
#endif
}

#endif // GPUI_INSTANCER_SETUP_INCLUDED
