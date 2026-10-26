#ifndef SCENE_SNOW_INCLUDED
#define SCENE_SNOW_INCLUDED

// Must match PlazaSnow.RENDERING_LAYER_MASK on the C# side
#define SNOW_RENDERING_LAYER_MASK (1u << 8)

// Global state, set from C# while the snow event is on. It lives outside UnityPerMaterial so existing
// materials and the SRP batcher are unaffected.
TEXTURE2D(_DCLSnowAlbedoMap);
SAMPLER(sampler_DCLSnowAlbedoMap);
TEXTURE2D(_DCLSnowNormalMap);
SAMPLER(sampler_DCLSnowNormalMap);
half4 _DCLSnowColor;
float4 _DCLSnowParams;  // x: amount, y: coverage start (world normal Y), z: coverage falloff, w: texture tiling per meter
half4 _DCLSnowSurface;  // x: smoothness, y: normal strength, z: edge noise strength, w: edge noise frequency
float4 _DCLSnowDetail;  // x: bump strength, y: bump frequency per meter
float4 _DCLSnowSparkle; // x: intensity, y: density (0-1), z: size in meters, w: sharpness

struct SnowTriplanarUV
{
    float2 uvX, uvY, uvZ;
    float2 dxX, dxY, dxZ;
    float2 dyX, dyY, dyZ;
    float footprintXZ; // world meters covered by one pixel on the top projection
};

float SnowHash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float3 SnowHash3(float2 p)
{
    return frac(sin(float3(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)), dot(p, float2(419.2, 371.9)))) * 43758.5453);
}

// Value noise and its analytic gradient: x = value, yz = d/dp
float3 SnowValueNoiseD(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float2 du = 6.0 * f * (1.0 - f);

    float a = SnowHash(i);
    float b = SnowHash(i + float2(1.0, 0.0));
    float c = SnowHash(i + float2(0.0, 1.0));
    float d = SnowHash(i + float2(1.0, 1.0));
    float k = a - b - c + d;

    float value = a + (b - a) * u.x + (c - a) * u.y + k * u.x * u.y;
    float2 gradient = du * (float2(b - a, c - a) + k * u.yx);
    return float3(value, gradient);
}

// Coverage from the mesh normal rather than the normal-mapped one, so the snow line follows the geometry
half SnowCoverage(float3 positionWS, half3 geometryNormalWS)
{
    half noise = (half(SnowValueNoiseD(positionWS.xz * _DCLSnowSurface.w).x) - 0.5h) * _DCLSnowSurface.z;
    half coverage = saturate((geometryNormalWS.y - _DCLSnowParams.y + noise) / max(_DCLSnowParams.z, 0.001));
    return coverage * _DCLSnowParams.x;
}

// Gradients are taken here, outside the per-pixel coverage branch, so sampling and filtering inside it stay correct
SnowTriplanarUV GetSnowTriplanarUV(float3 positionWS)
{
    float tiling = _DCLSnowParams.w;
    float3 p = positionWS * tiling;
    float3 dpdx = ddx(p);
    float3 dpdy = ddy(p);

    SnowTriplanarUV uv;
    uv.uvX = p.zy; uv.dxX = dpdx.zy; uv.dyX = dpdy.zy;
    uv.uvY = p.xz; uv.dxY = dpdx.xz; uv.dyY = dpdy.xz;
    uv.uvZ = p.xy; uv.dxZ = dpdx.xy; uv.dyZ = dpdy.xy;

    float2 dWorldX = ddx(positionWS.xz);
    float2 dWorldY = ddy(positionWS.xz);
    uv.footprintXZ = max(length(dWorldX), length(dWorldY));
    return uv;
}

half3 SampleSnowAlbedo(SnowTriplanarUV uv, half3 weights)
{
    return SAMPLE_TEXTURE2D_GRAD(_DCLSnowAlbedoMap, sampler_DCLSnowAlbedoMap, uv.uvX, uv.dxX, uv.dyX).rgb * weights.x
         + SAMPLE_TEXTURE2D_GRAD(_DCLSnowAlbedoMap, sampler_DCLSnowAlbedoMap, uv.uvY, uv.dxY, uv.dyY).rgb * weights.y
         + SAMPLE_TEXTURE2D_GRAD(_DCLSnowAlbedoMap, sampler_DCLSnowAlbedoMap, uv.uvZ, uv.dxZ, uv.dyZ).rgb * weights.z;
}

// Triplanar normal with the whiteout blend, returned in world space
half3 SampleSnowNormalWS(SnowTriplanarUV uv, half3 geometryNormalWS, half3 weights)
{
    half strength = _DCLSnowSurface.y;
    half3 tnX = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(_DCLSnowNormalMap, sampler_DCLSnowNormalMap, uv.uvX, uv.dxX, uv.dyX), strength);
    half3 tnY = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(_DCLSnowNormalMap, sampler_DCLSnowNormalMap, uv.uvY, uv.dxY, uv.dyY), strength);
    half3 tnZ = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(_DCLSnowNormalMap, sampler_DCLSnowNormalMap, uv.uvZ, uv.dxZ, uv.dyZ), strength);

    half3 absNormal = abs(geometryNormalWS);
    half3 axisSign = sign(geometryNormalWS);
    tnX = half3(tnX.xy + geometryNormalWS.zy, absNormal.x * tnX.z * axisSign.x);
    tnY = half3(tnY.xy + geometryNormalWS.xz, absNormal.y * tnY.z * axisSign.y);
    tnZ = half3(tnZ.xy + geometryNormalWS.xy, absNormal.z * tnZ.z * axisSign.z);

    return normalize(tnX.zyx * weights.x + tnY.xzy * weights.y + tnZ.xyz * weights.z);
}

// Soft drifts: two octaves of world-space noise on the top projection, faded out once they get smaller than a pixel
half3 ApplySnowBump(half3 normalWS, float3 positionWS, float footprintXZ)
{
    float frequency = _DCLSnowDetail.y;
    float3 octave1 = SnowValueNoiseD(positionWS.xz * frequency);
    float3 octave2 = SnowValueNoiseD(positionWS.xz * frequency * 3.1 + 17.0);
    float2 gradient = octave1.yz * frequency + octave2.yz * frequency * 3.1 * 0.35;

    half fade = saturate(1.0 - footprintXZ * frequency * 3.1);
    float3 gradientWS = float3(gradient.x, 0.0, gradient.y) * _DCLSnowDetail.x * fade;

    // Keep only the part of the gradient that lies on the surface
    gradientWS -= normalWS * dot(normalWS, gradientWS);
    return normalize(normalWS - gradientWS);
}

// Tiny random ice facets on the top projection that flash when they mirror the main light into the view
half SnowSparkle(float3 positionWS, half3 normalWS, half3 viewDirWS, half3 lightDirWS, float footprintXZ)
{
    float cellSize = max(_DCLSnowSparkle.z, 0.001);
    float2 cellPos = positionWS.xz / cellSize;
    float2 cell = floor(cellPos);

    float3 random = SnowHash3(cell);
    float3 facetJitter = SnowHash3(cell + 71.3) * 2.0 - 1.0;

    half present = step(1.0 - _DCLSnowSparkle.y, random.z);
    half pinpoint = saturate(1.0 - length(frac(cellPos) - random.xy) * 3.0);

    half3 facet = normalize(normalWS + half3(facetJitter) * 0.35h);
    half3 halfDir = SafeNormalize(lightDirWS + viewDirWS);
    half flash = pow(saturate(dot(facet, halfDir)), _DCLSnowSparkle.w);

    // Cells smaller than about a pixel would only shimmer
    half fade = saturate(1.5 - footprintXZ / cellSize);
    return flash * present * pinpoint * fade;
}

void ApplySnow(float3 positionWS, half3 geometryNormalWS, inout SurfaceData_Scene surfaceData, inout InputData_Scene inputData)
{
    half3 normalWS = normalize(geometryNormalWS);
    SnowTriplanarUV uv = GetSnowTriplanarUV(positionWS);
    half coverage = SnowCoverage(positionWS, normalWS);

    UNITY_BRANCH
    if (coverage <= 0.0h)
        return;

    half3 weights = pow(abs(normalWS), 4.0h);
    weights /= max(dot(weights, half3(1.0h, 1.0h, 1.0h)), 0.0001h);

    half3 snowAlbedo = SampleSnowAlbedo(uv, weights) * _DCLSnowColor.rgb;
    half3 snowNormalWS = SampleSnowNormalWS(uv, normalWS, weights);
    snowNormalWS = ApplySnowBump(snowNormalWS, positionWS, uv.footprintXZ);

    Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, half4(1.0h, 1.0h, 1.0h, 1.0h));
    half sparkle = SnowSparkle(positionWS, snowNormalWS, inputData.viewDirectionWS, mainLight.direction, uv.footprintXZ);
    half3 sparkleColor = mainLight.color * (mainLight.shadowAttenuation * mainLight.distanceAttenuation * sparkle * _DCLSnowSparkle.x);

    surfaceData.albedo = lerp(surfaceData.albedo, snowAlbedo, coverage);
    surfaceData.metallic = lerp(surfaceData.metallic, 0.0h, coverage);
    surfaceData.smoothness = lerp(surfaceData.smoothness, _DCLSnowSurface.x, coverage);
    surfaceData.emission = lerp(surfaceData.emission, sparkleColor, coverage);
    inputData.normalWS = normalize(lerp(inputData.normalWS, snowNormalWS, coverage));
}

#endif
