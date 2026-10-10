#ifndef SCENE_BRDF_INCLUDED
#define SCENE_BRDF_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"

#include "Scene_SurfaceData.hlsl"

inline void InitializeBRDFData_Scene(inout SurfaceData_Scene surfaceData, out BRDFData brdfData)
{
    InitializeBRDFData(surfaceData.albedo, surfaceData.metallic, half3(0,0,0), surfaceData.smoothness, surfaceData.alpha, brdfData);
}

BRDFData CreateClearCoatBRDFData_Scene(SurfaceData_Scene surfaceData, inout BRDFData brdfData)
{
    BRDFData brdfDataClearCoat = (BRDFData)0;

    #if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
    // base brdfData is modified here, rely on the compiler to eliminate dead computation by InitializeBRDFData()
    InitializeBRDFDataClearCoat(surfaceData.clearCoatMask, surfaceData.clearCoatSmoothness, brdfData, brdfDataClearCoat);
    #endif

    return brdfDataClearCoat;
}

#endif
