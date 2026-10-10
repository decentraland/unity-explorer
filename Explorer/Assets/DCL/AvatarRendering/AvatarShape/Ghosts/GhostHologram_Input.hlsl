#ifndef GHOST_HOLOGRAM_INPUT_INCLUDED
#define GHOST_HOLOGRAM_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    half4  _FresnelColor;
    float4 _RevealPosition;        // world-space — needs float
    half4  _RevealNormal;
    half4  _ScanLineSpeed;
    half4  _ScanLineTilling;
    half   _FresnelPower;
    half   _FresnelBandDensity;
    half   _FresnelBandSpeed;
    half   _Emission_Intensity;
    half   _Flicker_Speed;
    half   _Scanlines_Alpha;
    half   _ScanlineNoiseMin;
    half   _GlitchIntensity;
    half   _GlitchDensity;
    half   _GlitchSpeed;
    half   _GlitchThreshold;
CBUFFER_END

#endif
