#ifndef SCENE_AMBIENT_OCCLUSION_INCLUDED
#define SCENE_AMBIENT_OCCLUSION_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/AmbientOcclusion.hlsl"

#include "Scene_InputData.hlsl"
#include "Scene_SurfaceData.hlsl"

AmbientOcclusionFactor CreateAmbientOcclusionFactor_Scene(InputData_Scene inputData, SurfaceData_Scene surfaceData)
{
    return CreateAmbientOcclusionFactor(inputData.normalizedScreenSpaceUV, surfaceData.occlusion);
}

#endif
