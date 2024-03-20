#ifndef AVATAR_AMBIENT_OCCLUSION_INCLUDED
#define AVATAR_AMBIENT_OCCLUSION_INCLUDED

#include "Avatar_CelShading_Core.hlsl"
#include "Avatar_CelShading_SurfaceData.hlsl"

// Ambient occlusion
TEXTURE2D_X(_ScreenSpaceOcclusionTexture);
SAMPLER(sampler_ScreenSpaceOcclusionTexture);

struct AmbientOcclusionFactor
{
    half indirectAmbientOcclusion;
    half directAmbientOcclusion;
};

half SampleAmbientOcclusion(float2 normalizedScreenSpaceUV)
{
    float2 uv = UnityStereoTransformScreenSpaceTex(normalizedScreenSpaceUV);
    return half(SAMPLE_TEXTURE2D_X(_ScreenSpaceOcclusionTexture, sampler_ScreenSpaceOcclusionTexture, uv).x);
}

AmbientOcclusionFactor GetScreenSpaceAmbientOcclusion(float2 normalizedScreenSpaceUV)
{
    AmbientOcclusionFactor aoFactor;

    #if defined(_SCREEN_SPACE_OCCLUSION) && !defined(_SURFACE_TYPE_TRANSPARENT)
    float ssao = SampleAmbientOcclusion(normalizedScreenSpaceUV);

    aoFactor.indirectAmbientOcclusion = ssao;
    aoFactor.directAmbientOcclusion = lerp(half(1.0), ssao, _AmbientOcclusionParam.w);
    #else
    aoFactor.directAmbientOcclusion = 1;
    aoFactor.indirectAmbientOcclusion = 1;
    #endif

    #if defined(DEBUG_DISPLAY)
    switch(_DebugLightingMode)
    {
        case DEBUGLIGHTINGMODE_LIGHTING_WITHOUT_NORMAL_MAPS:
            aoFactor.directAmbientOcclusion = 0.5;
            aoFactor.indirectAmbientOcclusion = 0.5;
            break;

        case DEBUGLIGHTINGMODE_LIGHTING_WITH_NORMAL_MAPS:
            aoFactor.directAmbientOcclusion *= 0.5;
            aoFactor.indirectAmbientOcclusion *= 0.5;
            break;
    }
    #endif

    return aoFactor;
}

AmbientOcclusionFactor CreateAmbientOcclusionFactor(float2 normalizedScreenSpaceUV, half occlusion)
{
    AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(normalizedScreenSpaceUV);

    aoFactor.indirectAmbientOcclusion = min(aoFactor.indirectAmbientOcclusion, occlusion);
    return aoFactor;
}

AmbientOcclusionFactor CreateAmbientOcclusionFactor(InputData_Avatar inputData, SurfaceData_Avatar surfaceData)
{
    return CreateAmbientOcclusionFactor(inputData.normalizedScreenSpaceUV, surfaceData.occlusion);
}

#endif
