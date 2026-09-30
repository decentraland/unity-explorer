#ifndef DCL_CLOUDS_V2_INCLUDED
#define DCL_CLOUDS_V2_INCLUDED

// Layered cloud strips composited over the sky. Replaces the Screen Blend that composited the legacy cloud cubemap,
// and provides the occlusion the sun/moon layer multiplies by.
//
// Strip channels (authored per cloud, wide U-wrapping strip, V clamped):
//   R = shaded cloud (ramp index in the normal state)  G = backlit look (dark interior, bright rim)
//   B = growth order (low B appears first)             A = mask
//
// All parameters are global shader values set by SkyboxRenderController (Shader.SetGlobal*), so the main sky and the
// reflection-cubemap bake read the same data and the graphs need no extra properties.
//
// The legacy variant (no _DCL_SKY_STYLIZED keyword) reproduces Shader Graph's Blend node in Screen mode exactly and
// passes the legacy cloud colour through as occlusion, so the old look is untouched. In the stylized variant the graph
// feeds zero fallbacks and _DclCloudsMode.w (use clouds v2) can still switch the strips off.

#include "Assets/DCL/StylizedSkybox/Shaders/HLSL/SkyboxGlobals.hlsl"

#define DCL_CLOUDS_V2_PI 3.14159265

TEXTURE2D(_DclCloudStrip0); SAMPLER(sampler_DclCloudStrip0);
TEXTURE2D(_DclCloudStrip1); SAMPLER(sampler_DclCloudStrip1);
TEXTURE2D(_DclCloudStrip2); SAMPLER(sampler_DclCloudStrip2);

float4 _DclCloudLayer0;        // stretchV, offsetV, speed, strength
float4 _DclCloudLayer1;
float4 _DclCloudLayer2;
float4 _DclCloudLayerOpacity;  // opacity 0..2, layer count
float4 _DclCloudLayerFlow;     // per-layer visibility at the current phase
float4 _DclCloudLayerTiling;   // per-layer horizontal repeats around the horizon
float4 _DclCloudLayerOffsetU;  // per-layer horizontal offset (fraction of a full turn)
float4 _DclCloudShadowColor;   // current phase, HDR
float4 _DclCloudLitColor;      // current phase, HDR
float4 _DclCloudsParams;       // rampKnee, highlightStrength, highlightThreshold, highlightFalloff
float4 _DclCloudsMode;         // useRamp, useBacklight, useFlow, useCloudsV2
float4 _DclCloudsParams2;      // zenithFadeStart, zenithFadeEnd, occlusionStart, occlusionEnd

// Per-cloud cycle: grow in over the first 15 %, hold, dissolve top-first, stay absent for the last 10 %.
float CloudsV2_FlowCurve(float t)
{
    float grow = smoothstep(0.0, 0.15, t);
    float dissolve = 1.0 - smoothstep(0.7, 0.9, t);
    return min(grow, dissolve);
}

// Smooth cycle offset along the strip, so clouds at different positions form and dissolve at different times without
// any hard boundary cutting through a cloud.
float CloudsV2_CloudOffset(float u)
{
    // Integer frequency: the offset repeats with the texture, so a cloud crossing the strip seam stays whole.
    return 0.5 + 0.5 * sin(u * 2.0 * DCL_CLOUDS_V2_PI * 2.0);
}

// Samples the strip with derivatives that ignore the azimuth wrap: u jumps by exactly `period` (the repeat count)
// there, so removing whole periods from the derivative keeps the mip level continuous across the seam column.
float4 CloudsV2_Sample(TEXTURE2D_PARAM(strip, samp), float2 uv, float period)
{
    float2 dx = ddx(uv), dy = ddy(uv);
    dx.x -= round(dx.x / period) * period;
    dy.x -= round(dy.x / period) * period;
    return SAMPLE_TEXTURE2D_GRAD(strip, samp, uv, dx, dy);
}

float3 CloudsV2_Ramp(float t, float3 shadow, float3 lit, float knee)
{
    float3 mid = lerp(shadow, lit, 0.35);
    float lower = saturate(t / max(knee, 1e-3));
    float upper = saturate((t - knee) / max(1.0 - knee, 1e-3));
    return t < knee ? lerp(shadow, mid, lower) : lerp(mid, lit, upper);
}

// `hlAlpha` is the backlight amount, the same for every layer, computed once by CloudsV2Layers_float.
void CloudsV2_Layer(float4 tex, float u, float4 layer, float opacity, float phaseFlow, float layerIndex,
    float3 skyDir, float hlAlpha, inout float3 color, inout float alpha)
{
    float time = _TimeParameters.x;

    // The strips fade to black where the mask fades (premultiplied edges); undo that so soft edges keep the cloud
    // colour instead of dropping to the shadow colour and drawing a dark outline.
    float invMask = 1.0 / max(tex.a, 1e-3);
    tex.rgb = saturate(tex.rgb * invMask);

    // Backlight: the body behind the cloud drives R toward the G look and adds a rim.
    float t = lerp(tex.r, tex.g, hlAlpha);

    float3 shadow = _DclCloudShadowColor.rgb;
    float3 lit = _DclCloudLitColor.rgb;
    float3 col = _DclCloudsMode.x > 0.5 ? CloudsV2_Ramp(t, shadow, lit, _DclCloudsParams.x) : lit * t;
    col *= layer.w;
    col += tex.g * hlAlpha * _DclCloudsParams.y * lit;

    // Growth / dissolve: pixels whose growth order is below the flow value are visible.
    float cycle = frac(time * layer.z * 0.003 + layerIndex * 0.37 + CloudsV2_CloudOffset(u));
    float flow = _DclCloudsMode.z > 0.5 ? min(phaseFlow, CloudsV2_FlowCurve(cycle)) : phaseFlow;
    float a = tex.a * saturate((flow * 1.3 - tex.b) / 0.3);

    // Fade into the horizon haze, hide below the horizon, and fade out toward the zenith where the strip pinches.
    a *= smoothstep(-0.35, -0.05, skyDir.y) * (1.0 - smoothstep(_DclCloudsParams2.x, _DclCloudsParams2.y, skyDir.y)) * opacity;

    // Premultiplied "over": colour carries its own alpha, so soft edges keep full cloud colour instead of darkening.
    color = color * (1.0 - a) + col * a;
    alpha = alpha + a * (1.0 - alpha);
}

// Reproduces the Unreal sky dome: a three-quarter sphere whose UV0 is cylindrical, V = 0 at the zenith and V = 1 at
// the bottom rim about 24 degrees below the horizon, linear in latitude. The layer then applies
// Unreal's MF_CloudLayer transform: V + OffsetV, scaled about 0.5 by StretchV. Texture V is top-down there, so it
// is flipped for Unity. StretchV below 1 compresses the strip band onto a smaller slice of the dome (verified visually
// against the Unreal Classic layout: mid layer ~4-28 deg, overhead layer ~46-70 deg).
#define DCL_CLOUDS_V2_DOME_SPAN_DEG 113.6

// `vDome` is the dome V (0 zenith .. 1 bottom rim) and `azimuth` is in turns; both computed once per pixel by the caller.
float2 CloudsV2_Uv(float4 layer, float tiling, float offsetU, float vDome, float azimuth)
{
    float vTex = ((vDome + layer.y) - 0.5) / max(layer.x, 1e-3) + 0.5;
    float u = azimuth * tiling + offsetU - _TimeParameters.x * layer.z * 0.0004;
    return float2(u, saturate(1.0 - vTex));
}

// Node A (before the sun composite): samples the layers. Legacy passes the cubemap colour/opacity through and uses the
// cloud colour as occlusion, exactly like the old graph.
void CloudsV2Layers_float(float3 SkyDir, float4 FallbackColor, float FallbackOpacity,
    out float4 CloudColor, out float CloudAlpha, out float4 Occlusion)
{
#ifndef _DCL_SKY_STYLIZED
    CloudColor = FallbackColor;
    CloudAlpha = FallbackOpacity;
    Occlusion = FallbackColor;
#else
    if (_DclCloudsMode.w < 0.5)
    {
        CloudColor = 0;
        CloudAlpha = 0;
        Occlusion = 0;
        return;
    }

    float3 d = normalize(SkyDir);
    float azimuth = atan2(d.x, d.z) / (2.0 * DCL_CLOUDS_V2_PI);
    float vDome = (90.0 - degrees(asin(clamp(d.y, -1.0, 1.0)))) / DCL_CLOUDS_V2_DOME_SPAN_DEG;

    // Backlight amount, shared by the layers. With the computed celestial path the direction is the real body, so only
    // its own side lights; the legacy clip shares one light for both bodies and keeps the symmetric term. The weight
    // fades the rim out while the light crosses from sun to moon. The direction arrives unit length from C#.
    float facing = dot(d, _DclSunDirection.xyz);
    float hl = _DclCelestialParams.x > 0.5 ? facing : max(facing, -facing);
    float threshold = _DclCloudsParams.z;
    float hlAlpha = pow(saturate((hl - threshold) / max(1.0 - threshold, 1e-3)), max(_DclCloudsParams.w, 1e-3)) * _DclCloudsMode.y * _DclCelestialParams.y;

    float3 color = 0;
    float alpha = 0;
    float layerCount = _DclCloudLayerOpacity.w;

    // Back to front: layer 0 is the highest / farthest.
    if (layerCount > 0.5)
    {
        float2 uv = CloudsV2_Uv(_DclCloudLayer0, _DclCloudLayerTiling.x, _DclCloudLayerOffsetU.x, vDome, azimuth);
        float4 tex = CloudsV2_Sample(TEXTURE2D_ARGS(_DclCloudStrip0, sampler_DclCloudStrip0), uv, max(_DclCloudLayerTiling.x, 1.0));
        CloudsV2_Layer(tex, uv.x, _DclCloudLayer0, _DclCloudLayerOpacity.x, _DclCloudLayerFlow.x, 0.0, d, hlAlpha, color, alpha);
    }

    if (layerCount > 1.5)
    {
        float2 uv = CloudsV2_Uv(_DclCloudLayer1, _DclCloudLayerTiling.y, _DclCloudLayerOffsetU.y, vDome, azimuth);
        float4 tex = CloudsV2_Sample(TEXTURE2D_ARGS(_DclCloudStrip1, sampler_DclCloudStrip1), uv, max(_DclCloudLayerTiling.y, 1.0));
        CloudsV2_Layer(tex, uv.x, _DclCloudLayer1, _DclCloudLayerOpacity.y, _DclCloudLayerFlow.y, 1.0, d, hlAlpha, color, alpha);
    }

    if (layerCount > 2.5)
    {
        float2 uv = CloudsV2_Uv(_DclCloudLayer2, _DclCloudLayerTiling.z, _DclCloudLayerOffsetU.z, vDome, azimuth);
        float4 tex = CloudsV2_Sample(TEXTURE2D_ARGS(_DclCloudStrip2, sampler_DclCloudStrip2), uv, max(_DclCloudLayerTiling.z, 1.0));
        CloudsV2_Layer(tex, uv.x, _DclCloudLayer2, _DclCloudLayerOpacity.z, _DclCloudLayerFlow.z, 2.0, d, hlAlpha, color, alpha);
    }

    // CloudColor is premultiplied by CloudAlpha.
    CloudColor = float4(color, 1.0);
    CloudAlpha = alpha;

    // Only mostly opaque cloud hides the sun and its halo; occluding at soft edges paints a dark ring around clouds.
    // Edges kept apart: smoothstep with equal edges divides by zero.
    float occlusion = smoothstep(_DclCloudsParams2.z, max(_DclCloudsParams2.w, _DclCloudsParams2.z + 1e-4), alpha);
    Occlusion = float4(occlusion, occlusion, occlusion, occlusion);
#endif
}

// Node B (after the sun composite): draws the clouds over the sky. Legacy is Shader Graph's Blend (Screen) exactly.
void CloudsV2Composite_float(float4 Base, float4 CloudColor, float CloudAlpha, out float4 Composited)
{
#ifndef _DCL_SKY_STYLIZED
    float4 screen = 1.0 - (1.0 - CloudColor) * (1.0 - Base);
    Composited = lerp(Base, screen, CloudAlpha);
#else
    Composited = float4(Base.rgb * (1.0 - CloudAlpha) + CloudColor.rgb, Base.a);
#endif
}

#endif
