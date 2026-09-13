// Decentraland / Stylized Ocean — shared HLSL for every pass of
// Decentraland/StylizedOcean. StylizedOcean.shader includes this once per
// SubShader; when OCEAN_TESSELLATION is defined it also emits the hull/domain
// stages that subdivide the water grid by camera distance.
//
// Every material property the shipped water materials author is consumed here:
//   * Gerstner-sum waves, two scrolling normal maps, depth gradient, refraction,
//     probe/SSR reflection and shoreline foam (core surface).
//   * Caustics projected onto the refracted floor (_CausticsOn/_CausticsTex …).
//   * Intersection foam where the surface meets geometry (_Intersection* …),
//     sharp or rippled, sourced from scene depth and/or vertex colour.
//   * Sun sparkle glints (_SparkleIntensity/_SparkleSize).
//   * Slope foam on steep water (_SlopeFoam/_SlopeAngle* …).
//   * Distance-based tessellation (_TessValue/_TessMin/_TessMax).
// Textures the materials bind may be absent from the project; each textured
// term multiplies a procedural pattern, so a white default still produces the
// effect and a bound texture shapes it.
#ifndef DCL_STYLIZED_OCEAN_CORE_INCLUDED
#define DCL_STYLIZED_OCEAN_CORE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseColor;
    float4 _ShallowColor;
    float4 _HorizonColor;
    float4 _Color;
    float4 _SpecColor;

    float  _Cutoff;
    float  _Smoothness;
    float  _Metallic;

    float  _BumpScale;
    float4 _NormalTiling;
    float  _NormalSpeed;
    float  _NormalSubSpeed;
    float  _NormalSubTiling;
    float  _NormalStrength;
    float4 _Direction;

    float  _HorizonDistance;
    float  _ReflectionFresnel;
    float  _ReflectionStrength;

    float  _Depth;
    float  _ColorAbsorption;
    float  _EdgeFade;
    float  _RefractionStrength;
    float4 _FoamColor;
    float  _FoamSize;
    float  _FoamSpeed;
    float4 _FoamTiling;
    float  _FoamOn;
    float  _ShoreLineLength;

    float4 _WaveDirection;
    float  _WaveHeight;
    float  _WaveDistance;
    float  _WaveSpeed;
    float  _WaveSteepness;
    float  _WaveCount;
    float  _WaveTint;
    float  _WaveNormalStr;
    float  _Speed;
    float  _AnimationSpeed;

    float  _CausticsOn;
    float  _CausticsBrightness;
    float  _CausticsDistortion;
    float  _CausticsTiling;
    float  _CausticsSpeed;

    float4 _IntersectionColor;
    float  _IntersectionStyle;
    float  _IntersectionSource;
    float  _IntersectionSpeed;
    float  _IntersectionTiling;
    float  _IntersectionLength;
    float  _IntersectionFalloff;
    float  _IntersectionClipping;
    float  _IntersectionRippleDist;
    float  _IntersectionRippleStrength;
    float  _CrossPan_IntersectionOn;

    float  _SparkleIntensity;
    float  _SparkleSize;

    float  _SlopeFoam;
    float  _SlopeAngleThreshold;
    float  _SlopeAngleFalloff;
    float  _SlopeSpeed;
    float  _SlopeStretching;
    float  _SlopeThreshold;

    float  _TessValue;
    float  _TessMin;
    float  _TessMax;
CBUFFER_END

TEXTURE2D(_BaseMap);            SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap);            SAMPLER(sampler_BumpMap);
TEXTURE2D(_BumpMapLarge);       SAMPLER(sampler_BumpMapLarge);
TEXTURE2D(_CausticsTex);        SAMPLER(sampler_CausticsTex);
TEXTURE2D(_IntersectionNoise);  SAMPLER(sampler_IntersectionNoise);

// Globals fed by the Ocean render feature sub-passes (optional). Default
// 0/unbound → the branches below skip, so the base water works with the
// feature disabled.
TEXTURE2D(_OceanDisplacementTex);      SAMPLER(sampler_OceanDisplacementTex);
float4 _OceanDisplacementParams;   // xy = world centre XZ, z = 1/worldSize, w = active
TEXTURE2D(_OceanScreenReflectionTex);  SAMPLER(sampler_OceanScreenReflectionTex);
float4 _OceanScreenReflectionParams; // x = strength (0 = disabled)

// Shadow-caster globals (set by URP for the ShadowCaster pass).
float3 _LightDirection;
float3 _LightPosition;

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float2 uv         : TEXCOORD0;
    float4 color      : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS   : SV_POSITION;
    float2 uv           : TEXCOORD0;
    float3 positionWS   : TEXCOORD1;
    float3 normalWS     : TEXCOORD2;
    float4 tangentWS    : TEXCOORD3;
    float  fogFactor    : TEXCOORD4;
    float3 waveNormalWS : TEXCOORD5;
    float4 color        : TEXCOORD6;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

struct ShadowVaryings
{
    float4 positionCS : SV_POSITION;
};

struct DepthVaryings
{
    float4 positionCS : SV_POSITION;
};

struct DepthNormalsVaryings
{
    float4 positionCS : SV_POSITION;
    float3 normalWS   : TEXCOORD0;
};

// ---------------------------------------------------------------------------
// Waves
// ---------------------------------------------------------------------------

// Sum-of-Gerstner-waves vertex displacement. Accumulates up to _WaveCount
// waves around _WaveDirection, each rotated by a golden angle so the sum stays
// decorrelated. Returns the world-space offset (Y height, XZ trochoidal pinch
// driven by _WaveSteepness) and, through waveNormalWS, the analytic surface
// normal of that same sum.
float3 GerstnerSumWithNormal(float3 positionWS, out float3 waveNormalWS)
{
    float2 baseDir = normalize(_WaveDirection.xz + float2(1e-5, 0));
    int    waveCount = (int)round(clamp(_WaveCount, 1.0, 6.0));
    float  t       = _Time.y * _WaveSpeed * _Speed * _AnimationSpeed;
    float  amp     = _WaveHeight;
    float  freq    = max(_WaveDistance, 1e-3);
    float  Q       = _WaveSteepness;

    float3 offs = 0;
    float3 nrm  = float3(0.0, 1.0, 0.0);
    for (int i = 0; i < 6; ++i)
    {
        if (i >= waveCount) break;

        float ang = (float)i * 2.39996;
        float ca = cos(ang); float sa = sin(ang);
        float2 d = float2(ca * baseDir.x - sa * baseDir.y,
                          sa * baseDir.x + ca * baseDir.y);

        float k = freq * (1.0 + 0.32 * (float)i);
        float a = amp  * pow(0.78, (float)i);

        float phase = dot(d, positionWS.xz) * k + t * (1.0 + 0.13 * (float)i);
        float s = sin(phase);
        float c = cos(phase);

        offs.xz += Q * a * d * c;
        offs.y  += a * s;

        // Analytic partials of the same sum, so the slope the vertices take is
        // also the slope the surface is lit by.
        float ka = k * a;
        nrm.xz -= d * ka * c;
        nrm.y  -= Q * ka * s;
    }
    waveNormalWS = normalize(nrm);
    return offs;
}

float3 GerstnerSum(float3 positionWS)
{
    float3 waveNormalWS;
    return GerstnerSumWithNormal(positionWS, waveNormalWS);
}

// Wide-area displacement contributed by the feature's pre-pass.
float WideDisplacement(float3 positionWS)
{
    if (_OceanDisplacementParams.w < 0.5)
        return 0.0;
    float2 uv = (positionWS.xz - _OceanDisplacementParams.xy) * _OceanDisplacementParams.z + 0.5;
    return SAMPLE_TEXTURE2D_LOD(_OceanDisplacementTex, sampler_OceanDisplacementTex, uv, 0).r;
}

// ---------------------------------------------------------------------------
// Procedural patterns
// ---------------------------------------------------------------------------

// Cell hash for the foam pattern. The cell id is wrapped into a small range
// first: the ocean spans thousands of world units and the foam UV also carries
// accumulated time, magnitudes at which a sin-based hash returns a
// near-constant value instead of noise.
float FoamHash(float2 cell)
{
    cell -= floor(cell * (1.0 / 289.0)) * 289.0;
    float3 h = frac(float3(cell.xyx) * 0.1031);
    h += dot(h, h.yzx + 33.33);
    return frac((h.x + h.y) * h.z);
}

// Smooth value noise in [0,1] built from FoamHash at the cell corners.
float OceanValueNoise(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = FoamHash(cell);
    float b = FoamHash(cell + float2(1.0, 0.0));
    float c = FoamHash(cell + float2(0.0, 1.0));
    float d = FoamHash(cell + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// Interference web of two sine products, sharpened by a cube.
float OceanCausticWeb(float2 uv, float t)
{
    float c = sin(uv.x + t) * sin(uv.y - t)
            + sin(uv.x * 1.7 - t * 1.3) * sin(uv.y * 1.3 + t * 0.7);
    c = saturate(c * 0.5 + 0.5);
    return pow(c, 3.0);
}

// Two scrolling normal maps blended (RNM) into a tangent-space normal.
float3 SampleOceanNormalTS(float2 worldUV)
{
    float t = _Time.y * _Speed * _AnimationSpeed;
    float2 dirA = _Direction.xy;
    float2 dirB = (length(_Direction.zw) > 1e-4) ? _Direction.zw : -_Direction.xy;

    float2 uvA = worldUV * _NormalTiling.xy + dirA * t * _NormalSpeed;
    float2 uvB = worldUV * (_NormalTiling.zw + _NormalSubTiling.xx)
               + dirB * t * _NormalSubSpeed * 0.1;

    float3 nA = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,      sampler_BumpMap,      uvA), _BumpScale);
    float3 nB = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMapLarge, sampler_BumpMapLarge, uvB), _BumpScale);

    float3 n = normalize(float3(nA.xy * nB.z + nB.xy * nA.z, nA.z * nB.z));
    return n;
}

// ---------------------------------------------------------------------------
// Fragment terms
// ---------------------------------------------------------------------------

// Light bounced off the refracted floor: two scrolling caustic layers (the
// bound texture shaping the procedural web), distorted by the surface ripple
// and fading with the water column above the floor.
float3 OceanCaustics(float3 floorWS, float floorDepth, float2 rippleTS, float3 lightTint)
{
    float  t   = _Time.y * _CausticsSpeed;
    float2 cuv = floorWS.xz * _CausticsTiling + rippleTS * (_CausticsDistortion * 0.1);
    float2 uvA = cuv + t * float2(0.7, 0.4);
    float2 uvB = cuv * 1.37 - t * float2(0.5, 0.8);

    float cA = SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex, uvA).r * OceanCausticWeb(uvA * 6.2831, t);
    float cB = SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex, uvB).r * OceanCausticWeb(uvB * 6.2831 + 1.7, -t);

    float fade = exp(-floorDepth / max(_Depth * 2.0, 0.01));
    return min(cA, cB) * fade * _CausticsBrightness * lightTint;
}

// Foam band where the surface meets geometry. The band is 1 at the contact
// line and 0 at _IntersectionLength (raised to _IntersectionFalloff); the
// bound noise scrolls across it (cross-panned when enabled). Style 0 clips the
// band hard at _IntersectionClipping; style 1 modulates it with rings
// _IntersectionRippleDist apart that travel toward the contact line.
// Source 0 reads the band from scene depth, 1 from vertex colour red, 2 masks
// the depth band by vertex colour red.
float OceanIntersectionFoam(float waterDepth, float vertexRed, float2 worldXZ)
{
    float depthBand = 1.0 - saturate(waterDepth / max(_IntersectionLength, 0.01));
    float band = depthBand;
    if (_IntersectionSource > 1.5)
        band = depthBand * vertexRed;
    else if (_IntersectionSource > 0.5)
        band = vertexRed;
    band = pow(saturate(band), max(_IntersectionFalloff, 0.01));

    float  t   = _Time.y * _IntersectionSpeed;
    float2 iuv = worldXZ * _IntersectionTiling;
    float  noise = SAMPLE_TEXTURE2D(_IntersectionNoise, sampler_IntersectionNoise, iuv + t * float2(1.0, 0.6)).r;
    if (_CrossPan_IntersectionOn > 0.5)
    {
        float cross = SAMPLE_TEXTURE2D(_IntersectionNoise, sampler_IntersectionNoise, iuv * 0.73 - t * float2(0.6, 1.0)).r;
        noise = (noise + cross) * 0.5;
    }
    noise *= 0.6 + 0.4 * OceanValueNoise(worldXZ * 3.0 + t);

    float shape = band * (0.5 + noise);
    float clipT = saturate(1.0 - _IntersectionClipping);
    float foam;
    if (_IntersectionStyle < 0.5)
        foam = step(clipT, shape);
    else
    {
        float rings = 0.5 + 0.5 * sin((waterDepth / max(_IntersectionRippleDist, 0.01)) * 6.2831 + _Time.y * _IntersectionSpeed * 6.2831);
        shape *= lerp(1.0, rings, saturate(_IntersectionRippleStrength));
        foam = smoothstep(clipT, clipT + 0.25, shape);
    }
    return foam * saturate(band * 8.0);
}

// Sun glints: per-cell jittered normals catch the half vector only for some
// cells, re-rolled over time so the glints twinkle. Size widens each glint and
// spaces the cells out; intensity scales the added light.
float3 OceanSparkle(float3 positionWS, float3 nWS, float3 halfDir, float3 lightTint)
{
    float size = saturate(_SparkleSize);
    float cellsPerMeter = lerp(12.0, 2.0, size);
    float2 cell = floor(positionWS.xz * cellsPerMeter);
    float phase = floor(_Time.y * _Speed * _AnimationSpeed * 4.0);

    float h0 = FoamHash(cell + phase * 0.37);
    float h1 = FoamHash(cell + 19.19 + phase * 0.11);
    float h2 = FoamHash(cell + 73.7 + phase * 0.53);

    float3 glintN = normalize(nWS + float3(h0 - 0.5, 0.0, h1 - 0.5) * 0.6);
    float  glint  = pow(saturate(dot(glintN, halfDir)), lerp(2048.0, 96.0, size));
    glint *= step(0.55, h2);
    return glint * _SparkleIntensity * lightTint;
}

// Foam on steep water: the geometric slope angle (mesh normal + wave normal)
// past _SlopeAngleThreshold ramps in over _SlopeAngleFalloff degrees; the
// pattern streaks downhill, stretched along the flow and scrolling at
// _SlopeSpeed, clipped at _SlopeThreshold.
float OceanSlopeFoam(float3 positionWS, float3 geoN)
{
    float angle = degrees(acos(saturate(geoN.y)));
    float mask  = smoothstep(_SlopeAngleThreshold, _SlopeAngleThreshold + max(_SlopeAngleFalloff, 0.01), angle);
    if (mask <= 0.0)
        return 0.0;

    float2 downhill = normalize(float2(-geoN.x, -geoN.z) + float2(1e-5, 0.0));
    float2 across   = float2(-downhill.y, downhill.x);
    float  t = _Time.y * _Speed * _AnimationSpeed * _SlopeSpeed;
    float2 suv = float2(dot(positionWS.xz, across),
                        dot(positionWS.xz, downhill) * (1.0 - 0.9 * saturate(_SlopeStretching)) - t);

    float noise = OceanValueNoise(suv * 4.0);
    return smoothstep(_SlopeThreshold, _SlopeThreshold + 0.25, noise) * mask;
}

// ---------------------------------------------------------------------------
// Vertex stages
// ---------------------------------------------------------------------------

Varyings OceanVertex(Attributes IN)
{
    Varyings OUT = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

    VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
    VertexNormalInputs   vni = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

    float3 waveNormalWS;
    float3 displaced = vpi.positionWS + GerstnerSumWithNormal(vpi.positionWS, waveNormalWS);
    displaced.y += WideDisplacement(vpi.positionWS);

    OUT.positionWS   = displaced;
    OUT.positionCS   = TransformWorldToHClip(displaced);
    OUT.uv           = TRANSFORM_TEX(IN.uv, _BaseMap);
    OUT.normalWS     = vni.normalWS;
    OUT.waveNormalWS = waveNormalWS;
    OUT.tangentWS    = float4(vni.tangentWS, IN.tangentOS.w);
    OUT.fogFactor    = ComputeFogFactor(OUT.positionCS.z);
    OUT.color        = IN.color;
    return OUT;
}

float4 GetShadowPositionHClip(float3 positionWS, float3 normalWS)
{
    #if _CASTING_PUNCTUAL_LIGHT_SHADOW
        float3 lightDirectionWS = normalize(_LightPosition - positionWS);
    #else
        float3 lightDirectionWS = _LightDirection;
    #endif

    float4 positionCS = TransformWorldToHClip(
        ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

    #if UNITY_REVERSED_Z
        positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #else
        positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #endif
    return positionCS;
}

ShadowVaryings ShadowVertex(Attributes IN)
{
    ShadowVaryings OUT;
    UNITY_SETUP_INSTANCE_ID(IN);
    VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
    VertexNormalInputs   vni = GetVertexNormalInputs(IN.normalOS);
    float3 displaced = vpi.positionWS + GerstnerSum(vpi.positionWS);
    OUT.positionCS = GetShadowPositionHClip(displaced, vni.normalWS);
    return OUT;
}

DepthVaryings DepthVertex(Attributes IN)
{
    DepthVaryings OUT;
    UNITY_SETUP_INSTANCE_ID(IN);
    VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
    float3 displaced = vpi.positionWS + GerstnerSum(vpi.positionWS);
    OUT.positionCS = TransformWorldToHClip(displaced);
    return OUT;
}

DepthNormalsVaryings DepthNormalsVertex(Attributes IN)
{
    DepthNormalsVaryings OUT;
    UNITY_SETUP_INSTANCE_ID(IN);
    VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
    VertexNormalInputs   vni = GetVertexNormalInputs(IN.normalOS);
    float3 displaced = vpi.positionWS + GerstnerSum(vpi.positionWS);
    OUT.positionCS = TransformWorldToHClip(displaced);
    OUT.normalWS   = vni.normalWS;
    return OUT;
}

// ---------------------------------------------------------------------------
// Tessellation (hull/domain), emitted only for the SubShader that opts in.
// Each edge's factor falls from _TessValue at _TessMin to 1 at _TessMax by
// the edge midpoint's camera distance; the domain stage interpolates the
// control points back into Attributes and runs the pass's vertex function.
// ---------------------------------------------------------------------------
#if defined(OCEAN_TESSELLATION)

struct TessControlPoint
{
    float4 positionOS : INTERNALTESSPOS;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float2 uv         : TEXCOORD0;
    float4 color      : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct TessFactors
{
    float edge[3] : SV_TessFactor;
    float inside  : SV_InsideTessFactor;
};

TessControlPoint OceanTessVertex(Attributes IN)
{
    TessControlPoint OUT;
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
    OUT.positionOS = IN.positionOS;
    OUT.normalOS   = IN.normalOS;
    OUT.tangentOS  = IN.tangentOS;
    OUT.uv         = IN.uv;
    OUT.color      = IN.color;
    return OUT;
}

float OceanEdgeTessFactor(float3 aWS, float3 bWS)
{
    float d = distance((aWS + bWS) * 0.5, _WorldSpaceCameraPos);
    float t = saturate((d - _TessMin) / max(_TessMax - _TessMin, 1e-3));
    return clamp(lerp(_TessValue, 1.0, t), 1.0, 64.0);
}

TessFactors OceanPatchConstant(InputPatch<TessControlPoint, 3> patch)
{
    UNITY_SETUP_INSTANCE_ID(patch[0]);
    float3 p0 = TransformObjectToWorld(patch[0].positionOS.xyz);
    float3 p1 = TransformObjectToWorld(patch[1].positionOS.xyz);
    float3 p2 = TransformObjectToWorld(patch[2].positionOS.xyz);

    TessFactors f;
    f.edge[0] = OceanEdgeTessFactor(p1, p2);
    f.edge[1] = OceanEdgeTessFactor(p2, p0);
    f.edge[2] = OceanEdgeTessFactor(p0, p1);
    f.inside  = (f.edge[0] + f.edge[1] + f.edge[2]) / 3.0;
    return f;
}

[domain("tri")]
[partitioning("fractional_odd")]
[outputtopology("triangle_cw")]
[patchconstantfunc("OceanPatchConstant")]
[outputcontrolpoints(3)]
TessControlPoint OceanHull(InputPatch<TessControlPoint, 3> patch, uint id : SV_OutputControlPointID)
{
    return patch[id];
}

#define OCEAN_INTERPOLATE(field) (patch[0].field * bary.x + patch[1].field * bary.y + patch[2].field * bary.z)

Attributes OceanInterpolatePatch(OutputPatch<TessControlPoint, 3> patch, float3 bary)
{
    Attributes a = (Attributes)0;
    UNITY_TRANSFER_INSTANCE_ID(patch[0], a);
    a.positionOS = OCEAN_INTERPOLATE(positionOS);
    a.normalOS   = OCEAN_INTERPOLATE(normalOS);
    a.tangentOS  = OCEAN_INTERPOLATE(tangentOS);
    a.uv         = OCEAN_INTERPOLATE(uv);
    a.color      = OCEAN_INTERPOLATE(color);
    return a;
}

[domain("tri")]
Varyings OceanDomainForward(TessFactors f, OutputPatch<TessControlPoint, 3> patch, float3 bary : SV_DomainLocation)
{
    return OceanVertex(OceanInterpolatePatch(patch, bary));
}

[domain("tri")]
ShadowVaryings OceanDomainShadow(TessFactors f, OutputPatch<TessControlPoint, 3> patch, float3 bary : SV_DomainLocation)
{
    return ShadowVertex(OceanInterpolatePatch(patch, bary));
}

[domain("tri")]
DepthVaryings OceanDomainDepth(TessFactors f, OutputPatch<TessControlPoint, 3> patch, float3 bary : SV_DomainLocation)
{
    return DepthVertex(OceanInterpolatePatch(patch, bary));
}

[domain("tri")]
DepthNormalsVaryings OceanDomainDepthNormals(TessFactors f, OutputPatch<TessControlPoint, 3> patch, float3 bary : SV_DomainLocation)
{
    return DepthNormalsVertex(OceanInterpolatePatch(patch, bary));
}

#endif // OCEAN_TESSELLATION

// ---------------------------------------------------------------------------
// Fragment stages
// ---------------------------------------------------------------------------

half4 OceanFragment(Varyings IN) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

    float2 screenUV = GetNormalizedScreenSpaceUV(IN.positionCS);

    // Surface normal (tangent-space ripple → world). The mesh normal is
    // flat-up, so the wave slope has to be folded in here or the swell only
    // ever moves vertices and never changes how they are lit.
    float3 meshN = normalize(IN.normalWS);
    float3 waveN = normalize(IN.waveNormalWS);
    float3 N = normalize(meshN + (waveN - meshN) * _WaveNormalStr);
    float3 T = normalize(IN.tangentWS.xyz);
    T = normalize(T - N * dot(N, T));
    float3 B = normalize(cross(N, T) * IN.tangentWS.w);
    float3 nTS = SampleOceanNormalTS(IN.positionWS.xz);
    float3 nWS = normalize(nTS.x * T + nTS.y * B + nTS.z * N);

    float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));
    float  NdotV = saturate(dot(nWS, V));
    float  fres = pow(1.0 - NdotV, max(_ReflectionFresnel, 1e-3));

    float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
    Light  mainLight   = GetMainLight(shadowCoord);
    float3 lightTint   = mainLight.color * mainLight.shadowAttenuation;

    // --- Water column depth from the scene depth texture -------------------
    float sceneRaw = SampleSceneDepth(screenUV);
    float sceneEye = LinearEyeDepth(sceneRaw, _ZBufferParams);
    float surfEye  = LinearEyeDepth(IN.positionCS.z, _ZBufferParams);
    float waterDepth = max(sceneEye - surfEye, 0.0);

    // Depth-based shallow → deep colour gradient.
    float depthT = 1.0 - exp(-waterDepth / max(_Depth, 0.01));
    float3 bodyCol = lerp(_ShallowColor.rgb, _BaseColor.rgb, depthT);

    // --- Refraction: sample the opaque scene behind the water --------------
    // Fade distortion with depth so shallow edges stay crisp.
    float2 distort = nWS.xz * _RefractionStrength * saturate(waterDepth);
    float2 refrUV = screenUV + distort;
    // Guard: if the refracted sample is a surface in FRONT of the water, drop
    // the distortion (avoids smearing foreground edges).
    float refrRaw = SampleSceneDepth(refrUV);
    float refrSceneEye = LinearEyeDepth(refrRaw, _ZBufferParams);
    if (refrSceneEye < surfEye)
    {
        refrUV = screenUV;
        refrRaw = sceneRaw;
        refrSceneEye = sceneEye;
    }
    float3 refrCol = SampleSceneColor(refrUV);

    // --- Caustics on the refracted floor -----------------------------------
    if (_CausticsOn > 0.5)
    {
        float3 floorWS = ComputeWorldSpacePosition(refrUV, refrRaw, UNITY_MATRIX_I_VP);
        float  floorDepth = max(refrSceneEye - surfEye, 0.0);
        refrCol += OceanCaustics(floorWS, floorDepth, nTS.xy, lightTint);
    }

    // Absorb the refracted colour toward the water body colour with depth.
    float absorb = saturate(depthT + _ColorAbsorption * depthT);
    float3 underwater = lerp(refrCol, bodyCol, absorb);

    // --- Reflection: environment probe (skybox) + optional SSR -------------
    float3 reflVec = reflect(-V, nWS);
    half perceptualRoughness = saturate(1.0 - _Smoothness);
    half3 skyRefl = GlossyEnvironmentReflection(reflVec, perceptualRoughness, 1.0h);

    half3 reflCol = skyRefl;
    if (_OceanScreenReflectionParams.x > 0.0)
    {
        float2 ssrUV = screenUV + distort;
        half4 ssr = SAMPLE_TEXTURE2D(_OceanScreenReflectionTex, sampler_OceanScreenReflectionTex, ssrUV);
        reflCol = lerp(skyRefl, ssr.rgb, saturate(ssr.a * _OceanScreenReflectionParams.x));
    }

    // Fresnel blend between the refracted/underwater term and the sky.
    float reflAmount = saturate(fres * _ReflectionStrength);
    float3 color = lerp(underwater, reflCol, reflAmount);

    // Crest darkening (cheap sub-surface stand-in).
    float crest = saturate(nWS.y * 0.5 + 0.5);
    color *= lerp(1.0, 0.7, _WaveTint * (1.0 - crest));

    // --- Sun specular + sparkle --------------------------------------------
    float3 H = normalize(mainLight.direction + V);
    float  NdotH = saturate(dot(nWS, H));
    float  specPow = exp2(_Smoothness * 10.0 + 1.0);
    float  spec = pow(NdotH, specPow);
    color += _SpecColor.rgb * spec * lightTint;

    if (_SparkleIntensity > 0.0 && _SparkleSize > 0.0)
        color += OceanSparkle(IN.positionWS, nWS, H, lightTint);

    // --- Shoreline + crest foam --------------------------------------------
    float shoreFoam = 1.0 - saturate(waterDepth / max(_ShoreLineLength, 0.01));
    float2 foamUV = IN.positionWS.xz * (_FoamTiling.xy + 1e-4) + _Time.y * _FoamSpeed;
    float foamNoise = FoamHash(floor(foamUV * 8.0));
    foamNoise = smoothstep(1.0 - _FoamSize, 1.0, saturate(0.5 + 0.5 * sin(foamUV.x * 6.2831) * cos(foamUV.y * 6.2831) + foamNoise * 0.25));
    float foam = saturate(shoreFoam * shoreFoam) * (0.5 + 0.5 * foamNoise) * _FoamOn;
    color = lerp(color, _FoamColor.rgb, saturate(foam));

    // --- Intersection foam -------------------------------------------------
    float interFoam = OceanIntersectionFoam(waterDepth, IN.color.r, IN.positionWS.xz);
    color = lerp(color, _IntersectionColor.rgb, saturate(interFoam * _IntersectionColor.a));

    // --- Slope foam --------------------------------------------------------
    if (_SlopeFoam > 0.5)
    {
        float3 geoN = normalize(meshN + waveN - float3(0.0, 1.0, 0.0));
        float slopeFoam = OceanSlopeFoam(IN.positionWS, geoN);
        color = lerp(color, _FoamColor.rgb, saturate(slopeFoam));
    }

    color = MixFog(color, IN.fogFactor);

    // The material draws opaque (One/Zero) after the opaque pass and
    // composites refraction itself, so alpha is effectively 1.
    return half4(color, 1.0);
}

half4 ShadowFragment(ShadowVaryings IN) : SV_Target { return 0; }

half4 DepthFragment(DepthVaryings IN) : SV_Target { return 0; }

half4 DepthNormalsFragment(DepthNormalsVaryings IN) : SV_Target
{
    // URP's forward DepthNormals target holds raw signed world normals (the
    // 0..1 remap belongs to the deferred octahedral packing path), so anything
    // sampling _CameraNormalsTexture reads this directly.
    float3 n = normalize(IN.normalWS);
    return half4(n, 0);
}

#endif // DCL_STYLIZED_OCEAN_CORE_INCLUDED
