#ifndef DCL_SKY_LUT_INCLUDED
#define DCL_SKY_LUT_INCLUDED

// Phase x elevation sky lookup, used by a Custom Function node in GenesisSky and GenesisSkyBoxFullScreen.
// Lut rows (V): Night, Sunrise, Day, Sunset, Night. Bilinear filtering between rows blends adjacent phases.
// Lut columns (U): elevation, 0 = horizon, 1 = zenith.
#include "Assets/DCL/StylizedSkybox/Shaders/HLSL/SkyboxGlobals.hlsl"

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

// Stars v2 (global shader values set by SkyboxRenderController). Drawn under everything else so clouds and the disc
// cover them. Procedural points on cube-face cells, so they keep one angular size everywhere with no pole pinch.
// Brightness is evaluated per phase in C#, so they come out at dusk and fade at dawn; the drifting dim patches follow
// the Unreal star material and reuse the horizon noise texture.
float4 _DclStarsParams;   // brightness at the current phase, twinkle, patchStrength, shooting-star rate at the current phase
float4 _DclStarsParams2;  // cells per cube face, star radius (radians), rotationSpeed, twinkleSpeed
float4 _DclStarsParams3;  // horizonFadeStart, horizonFadeEnd, unused, unused

float SkyLut_Hash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float2 SkyLut_Hash2(float2 p)
{
    return float2(SkyLut_Hash(p), SkyLut_Hash(p + 19.19));
}

// Cube face the direction falls on, its uv on that face and the face axes, so cells can be turned back into directions.
void SkyLut_CubeFace(float3 d, out float2 faceUv, out float faceIndex, out float3 axisU, out float3 axisV, out float3 axisN)
{
    float3 a = abs(d);

    if (a.y >= a.x && a.y >= a.z)
    {
        faceIndex = d.y > 0.0 ? 0.0 : 1.0;
        axisN = float3(0.0, sign(d.y), 0.0);
        axisU = float3(1.0, 0.0, 0.0);
        axisV = float3(0.0, 0.0, 1.0);
        faceUv = d.xz / a.y;
    }
    else if (a.x >= a.z)
    {
        faceIndex = d.x > 0.0 ? 2.0 : 3.0;
        axisN = float3(sign(d.x), 0.0, 0.0);
        axisU = float3(0.0, 0.0, 1.0);
        axisV = float3(0.0, 1.0, 0.0);
        faceUv = d.zy / a.x;
    }
    else
    {
        faceIndex = d.z > 0.0 ? 4.0 : 5.0;
        axisN = float3(0.0, 0.0, sign(d.z));
        axisU = float3(1.0, 0.0, 0.0);
        axisV = float3(0.0, 1.0, 0.0);
        faceUv = d.xy / a.z;
    }
}

// One hashed star per occupied cell, checked over the 3x3 neighbourhood so a star near a cell edge is whole.
float SkyLut_StarField(float3 d, float time)
{
    float cells = max(_DclStarsParams2.x, 1.0);
    float radius = max(_DclStarsParams2.y, 1e-4);

    float2 faceUv;
    float faceIndex;
    float3 axisU, axisV, axisN;
    SkyLut_CubeFace(d, faceUv, faceIndex, axisU, axisV, axisN);

    float2 baseCell = floor((faceUv * 0.5 + 0.5) * cells);
    float sum = 0.0;

    [unroll]
    for (int j = -1; j <= 1; j++)
    {
        [unroll]
        for (int i = -1; i <= 1; i++)
        {
            float2 cell = baseCell + float2(i, j);
            if (any(cell < 0.0) || any(cell >= cells)) continue;

            float2 seed = cell + faceIndex * 131.0;
            if (SkyLut_Hash(seed + 7.7) < 0.45) continue;

            float2 starUv = (cell + SkyLut_Hash2(seed)) / cells * 2.0 - 1.0;
            float3 starDir = normalize(axisN + axisU * starUv.x + axisV * starUv.y);

            // Chord length ~ angle for small angles, and well conditioned where acos(dot) is not.
            float angle = length(d - starDir);
            float core = smoothstep(radius, radius * 0.25, angle);
            if (core <= 0.0) continue;

            float magnitude = SkyLut_Hash(seed + 3.3);
            float brightness = 0.25 + 0.75 * magnitude * magnitude * magnitude;
            float phase = SkyLut_Hash(seed + 5.5) * 6.2832;
            float twinkle = 1.0 + _DclStarsParams.y * 0.5 * sin(time * _DclStarsParams2.w * (2.0 + 3.0 * magnitude) + phase);
            sum += core * brightness * twinkle;
        }
    }

    return sum;
}

// Sky split into cells; every cell rolls once per period whether it fires a streak this round, most never do.
// Streaks run mostly sideways with a slight downward slant and stay inside their cell. `sky` is (azimuth, elevation) in 0..1.
float SkyLut_ShootingStars(float2 sky, float time, float rate)
{
    const float2 GRID = float2(12.0, 6.0);
    const float PERIOD = 25.0;
    const float STREAK_LIFE = 1.2 / PERIOD;

    float2 cellF = sky * GRID;
    float2 cell = floor(cellF);
    float2 f = cellF - cell;

    float h1 = SkyLut_Hash(cell);
    float h2 = SkyLut_Hash(cell + 17.3);
    float h3 = SkyLut_Hash(cell + 41.7);

    float cycle = frac(time / PERIOD + h1);
    if (cycle > STREAK_LIFE) return 0.0;

    float roundIndex = floor(time / PERIOD + h1);
    float fires = step(1.0 - rate * 0.15, SkyLut_Hash(cell + roundIndex * 3.1));

    float p = cycle / STREAK_LIFE;
    float toRight = h3 > 0.5 ? 1.0 : -1.0;
    float2 dir = normalize(float2(toRight * (0.85 + 0.15 * h3), -(0.15 + 0.3 * h2)));
    float2 start = float2(toRight > 0.0 ? 0.1 + 0.2 * h2 : 0.9 - 0.2 * h2, 0.55 + 0.3 * h3);
    float2 head = start + dir * p * 0.6;
    float2 tail = head - dir * 0.25 * (1.0 - p);

    float2 pa = f - tail;
    float2 ba = head - tail;
    float along = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-5));
    float dist = length(pa - ba * along);

    float streak = smoothstep(0.012, 0.0, dist) * along;
    return streak * sin(p * DCL_SKY_LUT_PI) * fires;
}

float3 SkyLut_Stars(float3 skyDir)
{
    float brightness = _DclStarsParams.x;
    float shootingRate = _DclStarsParams.w;
    if (brightness <= 0.0 && shootingRate <= 0.0) return 0.0;

    float time = _TimeParameters.x;
    float azimuth = atan2(skyDir.x, skyDir.z) / (2.0 * DCL_SKY_LUT_PI);
    float elevation = asin(clamp(skyDir.y, -1.0, 1.0)) / DCL_SKY_LUT_PI + 0.5;

    // The field turns slowly about the zenith.
    float turn = time * _DclStarsParams2.z * 2.0 * DCL_SKY_LUT_PI;
    float s = sin(turn), c = cos(turn);
    float3 turned = float3(skyDir.x * c - skyDir.z * s, skyDir.y, skyDir.x * s + skyDir.z * c);
    float star = SkyLut_StarField(turned, time);

    // Drifting dim patches (Unreal StarNoise) from the horizon noise.
    float patchNoise = SAMPLE_TEXTURE2D(_DclHorizonNoise, sampler_DclHorizonNoise, float2(azimuth * 3.0 + time * 0.002, elevation * 3.0)).r;
    star *= lerp(1.0, lerp(0.02, 1.0, patchNoise * patchNoise * patchNoise), _DclStarsParams.z);

    float horizonFade = smoothstep(_DclStarsParams3.x, _DclStarsParams3.y, skyDir.y);
    float shooting = shootingRate > 0.0 ? SkyLut_ShootingStars(float2(azimuth + 0.5, elevation), time, shootingRate) * 3.0 : 0.0;

    return (star * brightness + shooting) * horizonFade;
}

void SkyLut_float(UnityTexture2D Lut, float Phase, float3 ViewDirectionWS, float4 Fallback, float UseLut, out float4 Out)
{
    // Fed from the same wire as the sky subgraph's Direction input: the per-pixel direction into the sky, valid in the cubemap bake too.
    float3 skyDir = normalize(ViewDirectionWS);
    float elevation = saturate(skyDir.y + SkyLut_HorizonNoise(skyDir, skyDir.y));

    float rowV = Phase * (DCL_SKY_LUT_ROWS - 1.0) / DCL_SKY_LUT_ROWS + 0.5 / DCL_SKY_LUT_ROWS;
    float3 lut = SAMPLE_TEXTURE2D(Lut.tex, Lut.samplerstate, float2(elevation, rowV)).rgb;

    Out = UseLut > 0.5 ? float4(lut, Fallback.a) : Fallback;
    Out.rgb += SkyLut_Stars(skyDir);
}

#endif
