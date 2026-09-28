#ifndef DCL_SUN_DISC_INCLUDED
#define DCL_SUN_DISC_INCLUDED

#include "Assets/DCL/StylizedSkybox/Shaders/HLSL/SkyboxGlobals.hlsl"

// Shader Graph's Rotate About Axis node in radians, reproduced so the legacy second sun orbits exactly as before.
float3 SunDisc_RotateAboutAxis(float3 In, float3 Axis, float Rotation)
{
    float s = sin(Rotation);
    float c = cos(Rotation);
    float one_minus_c = 1.0 - c;
    Axis = normalize(Axis);
    float3x3 rot_mat =
    {
        one_minus_c * Axis.x * Axis.x + c, one_minus_c * Axis.x * Axis.y - Axis.z * s, one_minus_c * Axis.z * Axis.x + Axis.y * s,
        one_minus_c * Axis.x * Axis.y + Axis.z * s, one_minus_c * Axis.y * Axis.y + c, one_minus_c * Axis.y * Axis.z - Axis.x * s,
        one_minus_c * Axis.z * Axis.x - Axis.y * s, one_minus_c * Axis.y * Axis.z + Axis.x * s, one_minus_c * Axis.z * Axis.z + c
    };
    return mul(rot_mat, In);
}

// Right and up axes of a body's own frame, so offsets keep one shape wherever the body is.
void SunDisc_Frame(float3 bodyDir, out float3 right, out float3 up)
{
    float3 upHint = abs(bodyDir.y) > 0.999 ? float3(0.0, 0.0, 1.0) : float3(0.0, 1.0, 0.0);
    right = normalize(cross(upHint, bodyDir));
    up = cross(bodyDir, right);
}

// The sun disc while the haze is up: drawn in the sun's own frame so it can grow, flatten, soften and take a ragged
// rim, and coloured top-to-bottom by the haze gradient instead of the flat sun colour. `factor` is 0 above the haze
// window and 1 at the horizon; everything eases with it so the disc is continuous with the legacy step disc.
float SunDisc_Haze(float3 dir, float3 lightDirection, float discRadius, float factor, out float3 hazeColor)
{
    float3 sunDir = normalize(lightDirection);
    float3 right, up;
    SunDisc_Frame(sunDir, right, up);

    float depth = dot(dir, sunDir);
    float2 p = float2(dot(dir, right), dot(dir, up)) / max(depth, 1e-4);

    float radius = tan(discRadius) * lerp(1.0, _DclSunHazeParams.y, factor);
    float squash = lerp(1.0, _DclSunHazeParams.z, factor);
    float2 q = float2(p.x, p.y / squash);

    // Ragged rim: integer harmonics around the disc so the noise wraps without a seam; the mirage wobble is slow.
    float theta = atan2(q.y, q.x);
    float t = _TimeParameters.x * _DclSunHazeParams2.z;
    float k1 = max(1.0, round(_DclSunHazeParams2.y));
    float rim = sin(theta * k1 + t) * 0.5 + sin(theta * (k1 * 2.0 + 1.0) - t * 1.7) * 0.3 + sin(theta * (k1 * 5.0 + 3.0) + t * 0.9) * 0.2;
    float raggedRadius = radius * (1.0 + rim * _DclSunHazeParams2.x * factor);

    float edge = max(radius * _DclSunHazeParams.w * factor, 1e-5);
    float disc = depth > 0.0 ? 1.0 - smoothstep(raggedRadius - edge, raggedRadius + edge, length(q)) : 0.0;

    // 0 at the bottom of the disc, 1 at the top; the power biases where the red band sits.
    float v = pow(saturate(q.y / radius * 0.5 + 0.5), _DclSunHazeParams2.w);
    hazeColor = lerp(_DclSunHazeBottom.rgb, _DclSunHazeTop.rgb, v);

    return disc;
}

// Draws the sun (or moon) disc: the main disc, the crescent mask and the small orbiting second sun, then colour and
// opacity. Replaces the node chain in the Genesis Sky Celestial sub-graph; with the haze factor at 0 the maths is the
// legacy chain node for node, so the Legacy preset renders unchanged.
void SunDisc_float(float3 Direction, float3 LightDirection, float SunSize, float4 SunColor, float SunOpacity,
    float MoonMaskSize, float2 MoonMaskPosition, bool InvertDirection, float SecondSunSizeFactor,
    float SecondSunRotationSpeed, float SecondSunOrbitSize, out float4 SunAndMoon)
{
    float3 dir = normalize(Direction);
    float discRadius = SunSize * 0.3;

    float4 color = SunColor;
    float sun;
    float hazeFactor = _DclSunHazeParams.x;

    if (hazeFactor > 0.0)
    {
        float3 hazeColor;
        sun = SunDisc_Haze(dir, LightDirection, discRadius, hazeFactor, hazeColor);
        color.rgb = lerp(SunColor.rgb, hazeColor, hazeFactor);
    }
    else
        sun = step(acos(dot(LightDirection, dir)), discRadius);

    // Crescent: a second circle off the disc centre cuts a hole; size 0 leaves the disc whole. The computed path
    // offsets it in the moon's own frame so the crescent keeps one shape all night; legacy nudges it in world space.
    float3 maskCenter;

    if (_DclMoonMaskOffset.z > 0.5)
    {
        float3 bodyDir = normalize(LightDirection);
        float3 right, up;
        SunDisc_Frame(bodyDir, right, up);
        maskCenter = normalize(bodyDir + right * _DclMoonMaskOffset.x + up * _DclMoonMaskOffset.y);
    }
    else
    {
        float3 maskNudge = float3(MoonMaskPosition.x, MoonMaskPosition.y, 0.0);
        maskNudge = InvertDirection ? -maskNudge : maskNudge;
        maskCenter = normalize(LightDirection + maskNudge);
    }

    float mask = step(MoonMaskSize * MoonMaskSize, acos(dot(maskCenter, dir)));

    // Second sun: a small body orbiting the main disc, pulsing in size with its orbit.
    float3 orbit = float3(1.0, 0.0, 1.0);
    orbit = InvertDirection ? -orbit : orbit;
    float rotation = SecondSunRotationSpeed * _TimeParameters.x;
    orbit = SunDisc_RotateAboutAxis(orbit, float3(0.0, 1.0, 0.0), rotation) * SecondSunOrbitSize;
    float secondRadius = discRadius * SecondSunSizeFactor;
    float secondSun = step(acos(dot(normalize(LightDirection + orbit), dir)), secondRadius + secondRadius * ((cos(rotation) + 1.0) / 2.0));

    SunAndMoon = clamp(sun + secondSun, 0.0, 1.0) * color * SunOpacity * SunOpacity * mask;
}

// Half-precision entry point, which the sub-graph preview shader asks for; the maths stays in float.
void SunDisc_half(half3 Direction, half3 LightDirection, half SunSize, half4 SunColor, half SunOpacity,
    half MoonMaskSize, half2 MoonMaskPosition, bool InvertDirection, half SecondSunSizeFactor,
    half SecondSunRotationSpeed, half SecondSunOrbitSize, out half4 SunAndMoon)
{
    float4 result;
    SunDisc_float(Direction, LightDirection, SunSize, SunColor, SunOpacity, MoonMaskSize, MoonMaskPosition,
        InvertDirection, SecondSunSizeFactor, SecondSunRotationSpeed, SecondSunOrbitSize, result);
    SunAndMoon = result;
}

#endif
