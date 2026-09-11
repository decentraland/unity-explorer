#ifndef DCL_SKY_LUT_INCLUDED
#define DCL_SKY_LUT_INCLUDED

// Phase x elevation sky lookup, used by a Custom Function node in GenesisSky and GenesisSkyBoxFullScreen.
// Lut rows (V): Night, Sunrise, Day, Sunset, Night. Bilinear filtering between rows blends adjacent phases.
// Lut columns (U): elevation, 0 = horizon, 1 = zenith.
#define DCL_SKY_LUT_ROWS 5.0

void SkyLut_float(UnityTexture2D Lut, float Phase, float3 ViewDirectionWS, float4 Fallback, float UseLut, out float4 Out)
{
    // Fed from the same wire as the sky subgraph's Direction input: the per-pixel direction into the sky, valid in the cubemap bake too.
    float3 skyDir = normalize(ViewDirectionWS);
    float elevation = saturate(skyDir.y);

    float rowV = Phase * (DCL_SKY_LUT_ROWS - 1.0) / DCL_SKY_LUT_ROWS + 0.5 / DCL_SKY_LUT_ROWS;
    float3 lut = SAMPLE_TEXTURE2D(Lut.tex, Lut.samplerstate, float2(elevation, rowV)).rgb;

    Out = UseLut > 0.5 ? float4(lut, Fallback.a) : Fallback;
}

#endif
