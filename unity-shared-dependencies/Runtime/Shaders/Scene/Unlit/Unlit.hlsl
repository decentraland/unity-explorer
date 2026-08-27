
#ifndef UNLIT_INCLUDED
#define UNLIT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging3D.hlsl"
#include "Input.hlsl"
#include "SurfaceData.hlsl"

half4 UniversalFragmentUnlit(InputData inputData, SurfaceData surfaceData)
{
    half3 albedo = surfaceData.albedo;

    #if defined(DEBUG_DISPLAY)
    half4 debugColor;

    if (CanDebugOverrideOutputColor(inputData, surfaceData, debugColor))
    {
        return debugColor;
    }
    #endif

    half4 finalColor = half4(albedo + surfaceData.emission, surfaceData.alpha);

    return finalColor;
}

#endif
