#ifndef DCL_SKY_LUT_INCLUDED
#define DCL_SKY_LUT_INCLUDED

// Phase x elevation sky lookup, used by a Custom Function node in GenesisSky and GenesisSkyBoxFullScreen.
// Lut rows (V): Night, Sunrise, Day, Sunset, Night. Bilinear filtering between rows blends adjacent phases.
// Lut columns (U): elevation, 0 = horizon, 1 = zenith.
#define DCL_SKY_LUT_ROWS 5.0
#define DCL_SKY_LUT_PI 3.14159265
#define DCL_SKY_LUT_DOME_SPAN_DEG 113.6

// Horizon noise (global shader values set by SkyboxRenderController): pushes the elevation coordinate up near the
// horizon with a tiling noise, so the bright band shows soft silhouettes of distant clouds, as in the Unreal sky dome.
TEXTURE2D(_DclHorizonNoise); SAMPLER(sampler_DclHorizonNoise);
float4 _DclHorizonNoiseParams; // strength, tilingU, tilingV, speed (0 strength = off)

float SkyLut_HorizonNoise(float3 skyDir, float elevation)
{
    float strength = _DclHorizonNoiseParams.x;
    if (strength <= 0.0) return 0.0;

    float azimuth = atan2(skyDir.x, skyDir.z) / (2.0 * DCL_SKY_LUT_PI);
    float elevationDeg = degrees(asin(clamp(skyDir.y, -1.0, 1.0)));
    float vDome = (90.0 - elevationDeg) / DCL_SKY_LUT_DOME_SPAN_DEG;

    float2 uv = float2(azimuth * _DclHorizonNoiseParams.y + _TimeParameters.x * _DclHorizonNoiseParams.w, vDome * _DclHorizonNoiseParams.z);
    float2 uvAlt = float2(frac(uv.x + 0.5), uv.y);
    float2 dx = ddx(uv), dy = ddy(uv);
    float2 dxAlt = ddx(uvAlt), dyAlt = ddy(uvAlt);
    if (abs(dxAlt.x) < abs(dx.x)) dx.x = dxAlt.x;
    if (abs(dyAlt.x) < abs(dy.x)) dy.x = dyAlt.x;
    float noise = SAMPLE_TEXTURE2D_GRAD(_DclHorizonNoise, sampler_DclHorizonNoise, uv, dx, dy).r;

    // Only below ~22 degrees of elevation, full at the horizon (Unreal: remap dome V 0.6..0.9).
    return noise * strength * saturate((vDome - 0.6) / 0.3);
}

void SkyLut_float(UnityTexture2D Lut, float Phase, float3 ViewDirectionWS, float4 Fallback, float UseLut, out float4 Out)
{
    // Fed from the same wire as the sky subgraph's Direction input: the per-pixel direction into the sky, valid in the cubemap bake too.
    float3 skyDir = normalize(ViewDirectionWS);
    float elevation = saturate(skyDir.y + SkyLut_HorizonNoise(skyDir, skyDir.y));

    float rowV = Phase * (DCL_SKY_LUT_ROWS - 1.0) / DCL_SKY_LUT_ROWS + 0.5 / DCL_SKY_LUT_ROWS;
    float3 lut = SAMPLE_TEXTURE2D(Lut.tex, Lut.samplerstate, float2(elevation, rowV)).rgb;

    Out = UseLut > 0.5 ? float4(lut, Fallback.a) : Fallback;
}

#endif
