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
    float3 positionWS : TEXCOORD0;
    half3  normalWS   : TEXCOORD1;
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
    output.positionWS = displacedWS;
    output.normalWS   = (half3)normalInputs.normalWS;

    return output;
}

half4 frag(Varyings input) : SV_Target
{
    // --- Reveal clip (general plane equation) ---
    // Passes vertices on the _RevealNormal side of the plane through _RevealPosition.
    // AvatarGhostSystem animates _RevealPosition.y from -0.05 -> 3 with normal (0,+/-1,0).
    // Uses float for world-space position subtraction, half for the dot
    clip((half)dot(_RevealNormal.xyz, (half3)(_RevealPosition.xyz - input.positionWS)));

    // --- Fresnel ---
    half3 viewDirWS = (half3)normalize(GetCameraPositionWS() - input.positionWS);
    half3 normalWS  = normalize(input.normalWS);
    half  baseFresnel = pow(1.0h - saturate(dot(viewDirWS, normalWS)), _FresnelPower);

    // --- Banded fresnel brightness variation ---
    // Coarse bands — independent of scanline density, re-rolls slowly over time
    float brightBand     = floor(input.positionWS.y * _FresnelBandDensity);
    half  bandBrightness = (half)frac(sin(brightBand * 91.7 + floor(_Time.y * _FresnelBandSpeed) * 43.1) * 43758.5453);
    half  bandedFresnel  = baseFresnel * bandBrightness * bandBrightness;

    // --- Procedural scanlines with per-line intensity variation ---
    // Position * tiling accumulates in float, frac() brings to [0,1] then half
    float scanYCoord  = input.positionWS.y * _ScanLineTilling.y + _Time.y * _ScanLineSpeed.y;
    half  scanline    = (half)smoothstep(0.3, 0.7, frac(scanYCoord));
    // Hash each scanline row to vary brightness — some lines brighter, some dimmer
    float scanRow     = floor(scanYCoord);
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
