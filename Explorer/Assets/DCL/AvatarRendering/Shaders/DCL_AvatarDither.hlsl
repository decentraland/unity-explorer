#ifndef DCL_TOON_DITHER_INCLUDED
#define DCL_TOON_DITHER_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"

void Dithering( float3 positionWS, float4 positionCS, float fEndFadeDistance, float fStartFadeDistance)
{
    float4 ndc = positionCS * 0.5f;
    float4 positionNDC; // Homogeneous normalized device coordinates
    positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
    positionNDC.zw = positionCS.zw;

    float dist = length(positionWS - _WorldSpaceCameraPos);
    
    if (dist >= fStartFadeDistance)
        return;

    float hideAmount = (dist - fEndFadeDistance) / (fStartFadeDistance - fEndFadeDistance);
    
    // Screen-door transparency: Discard pixel if below threshold.
    const float4x4 thresholdMatrix =
    {
        1.0 / 17.0, 9.0 / 17.0, 3.0 / 17.0, 11.0 / 17.0,
        13.0 / 17.0, 5.0 / 17.0, 15.0 / 17.0, 7.0 / 17.0,
        4.0 / 17.0, 12.0 / 17.0, 2.0 / 17.0, 10.0 / 17.0,
        16.0 / 17.0, 8.0 / 17.0, 14.0 / 17.0, 6.0 / 17.0
    };

    const float4x4 _RowAccess = { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
    float2 pos = positionNDC.xy / positionNDC.w;
    pos *= _ScreenParams.xy; // pixel position
    clip(hideAmount - thresholdMatrix[fmod(pos.x, 4)] * _RowAccess[fmod(pos.y, 4)]);
}

#endif // DCL_TOON_DITHER_INCLUDED