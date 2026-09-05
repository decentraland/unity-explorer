#ifndef GHOST_HOLOGRAM_FORWARD_PASS_INCLUDED
#define GHOST_HOLOGRAM_FORWARD_PASS_INCLUDED

#include "GhostHologram_Input.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionOS : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3  normalWS   : TEXCOORD2;
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings vert(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs posInputs    = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs   normalInputs = GetVertexNormalInputs(input.normalOS);

    float3 displacedWS = posInputs.positionWS;

    // Horizontal glitch jitter — discrete bands that randomly shift in X
    // sin(band * 127.1 ...) * 43758 needs float to avoid half overflow
    float band       = floor(displacedWS.y * _GlitchDensity);
    float glitchSeed = frac(sin(band * 127.1 + floor(_Time.y * _GlitchSpeed)) * 43758.5453);
    half  glitch     = step(_GlitchThreshold, (half)glitchSeed) * ((half)glitchSeed - 0.5h) * _GlitchIntensity;
    displacedWS.x   += glitch;

    output.positionCS = TransformWorldToHClip(displacedWS);
    output.positionOS = input.positionOS.xyz;
    output.positionWS = displacedWS;
    output.normalWS   = (half3)normalInputs.normalWS;

    return output;
}

half4 frag(Varyings input) : SV_Target
{
    // --- Reveal clip (object space) ---
    // Hip is at positionOS.y = 1, so subtract 1 to shift to feet-relative (feet=0, head=2).
    // positionOS is already interpolated from the vertex shader — no matrix multiply needed.
    float3 adjustedPosOS = float3(input.positionOS.x, 1.0 - input.positionOS.y, input.positionOS.z);
    clip((half)dot(_RevealNormal.xyz, (half3)(_RevealPosition.xyz - adjustedPosOS)));

    // --- Fresnel ---
    half3 viewDirWS = (half3)normalize(GetCameraPositionWS() - input.positionWS);
    half3 normalWS  = normalize(input.normalWS);
    half  baseFresnel = pow(1.0h - saturate(dot(viewDirWS, normalWS)), _FresnelPower);

    // --- Banded fresnel brightness variation ---
    // Coarse bands in object space — stable across world elevation and avatar movement
    float brightBand     = floor(input.positionOS.y * _FresnelBandDensity);
    half  bandBrightness = (half)frac(sin(brightBand * 91.7) * 43758.5453);
    half  bandedFresnel  = baseFresnel * bandBrightness * bandBrightness;

    // --- Procedural scanlines with per-line intensity variation ---
    // Object-space Y keeps scanlines stable relative to the avatar regardless of world position
    float scanYCoord  = input.positionOS.y * _ScanLineTilling.y + _Time.y * _ScanLineSpeed.y;
    half  scanline    = (half)smoothstep(0.3, 0.7, frac(scanYCoord));
    // Hash from static object-space Y to avoid brightness jumps as scanlines scroll
    float scanRow     = floor(input.positionOS.y * _ScanLineTilling.y);
    half  scanNoise   = (half)frac(sin(scanRow * 153.7) * 43758.5453);
    half scanNoiseRange = 1.0h - _ScanlineNoiseMin;
    scanline         *= _Scanlines_Alpha * (_ScanlineNoiseMin + scanNoise * scanNoiseRange);

    // --- Procedural flicker (discrete frame hash, single scalar — not per-pixel) ---
    half flicker = (half)frac(sin(floor(_Time.y * _Flicker_Speed) * 127.1) * 43758.5453);

    // --- Combine ---
    // baseFresnel = clean rim glow (always visible), bandedFresnel = hot/cold variation on top
    half  intensity = _Emission_Intensity * (0.9h + flicker * 0.1h);
    half3 col       = _FresnelColor.rgb * (baseFresnel + bandedFresnel + scanline) * intensity;
    half  alpha     = saturate(baseFresnel + bandedFresnel * 0.5h + scanline * 0.5h) * _FresnelColor.a;

    return half4(col, alpha);
}

#endif
