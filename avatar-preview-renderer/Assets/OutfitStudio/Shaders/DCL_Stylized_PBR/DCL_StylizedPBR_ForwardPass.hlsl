#ifndef DCL_STYLIZED_PBR_FORWARD_PASS_INCLUDED
#define DCL_STYLIZED_PBR_FORWARD_PASS_INCLUDED

// Disney-principled stylized PBR (Burley 2012 / the parameterization Fortnite's UE shading uses)
// with a stylization layer: wrapped+sharpened diffuse, softened GGX, sheen, clearcoat, artist rim,
// and a matcap standing in for environment reflections on metals.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "DCL_StylizedPBR_Input.hlsl"

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv           : TEXCOORD0;
    float3 positionWS   : TEXCOORD1;
    float3 normalWS     : TEXCOORD2;
    float4 tangentWS    : TEXCOORD3; // xyz tangent, w sign
    float fogFactor     : TEXCOORD4;
    float4 positionCS   : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings StylizedPBRVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    output.uv = input.texcoord;
    output.positionWS = vertexInput.positionWS;
    output.normalWS = normalInput.normalWS;
    output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
    output.positionCS = vertexInput.positionCS;
    return output;
}

// ---------------------------------------------------------------------------------------------
// BRDF helpers (Disney / Burley 2012, real-time simplifications)

half SchlickWeight(half u)
{
    half m = saturate(1.0 - u);
    half m2 = m * m;
    return m2 * m2 * m; // (1-u)^5
}

half D_GGX_Stylized(half NoH, half roughness)
{
    half a2 = roughness * roughness;
    half d = (NoH * a2 - NoH) * NoH + 1.0;
    return a2 / max(PI * d * d, 1e-4);
}

half V_SmithApprox(half NoV, half NoL, half roughness)
{
    // Height-correlated Smith, Karis approximation
    half a = roughness;
    half lambdaV = NoL * (NoV * (1.0 - a) + a);
    half lambdaL = NoV * (NoL * (1.0 - a) + a);
    return 0.5 / max(lambdaV + lambdaL, 1e-4);
}

// GTR1 lobe for the clearcoat (Disney: fixed IOR 1.5 -> F0 0.04)
half D_GTR1(half NoH, half a)
{
    half a2 = a * a;
    half t = 1.0 + (a2 - 1.0) * NoH * NoH;
    return (a2 - 1.0) / max(PI * log(a2) * t, 1e-4);
}

// One light's full contribution (main and additional lights share this)
half3 EvaluateStylizedLight(
    half3 albedo, half metallic, half perceptualRoughness,
    half3 f0, half3 sheenColor,
    half3 normalWS, half3 viewDirWS,
    half3 lightDir, half3 lightColor, half lightAtten)
{
    half roughness = perceptualRoughness * perceptualRoughness;
    half3 halfDir = SafeNormalize(lightDir + viewDirWS);
    half NoL = dot(normalWS, lightDir);
    half NoV = saturate(abs(dot(normalWS, viewDirWS)) + 1e-5);
    half NoH = saturate(dot(normalWS, halfDir));
    half LoH = saturate(dot(lightDir, halfDir));

    // Stylized diffuse falloff: wrapped, then sharpened toward a clean two-tone break
    half wrapped = saturate((NoL + _DiffuseWrap) / (1.0 + _DiffuseWrap));
    half feather = max(1.0 - _ShadowSharpness, 0.001) * 0.5;
    half diffTerm = smoothstep(0.5 - feather, 0.5 + feather, wrapped);
    // Blend back toward plain wrapped lambert so sharpness 0 = smooth gradient
    diffTerm = lerp(wrapped, diffTerm, _ShadowSharpness);

    // Burley retro-reflection factor (evaluated with the analytic NoL, kept subtle)
    half FD90 = 0.5 + 2.0 * roughness * LoH * LoH;
    half burley = lerp(1.0, FD90, SchlickWeight(saturate(NoL))) * lerp(1.0, FD90, SchlickWeight(NoV));

    half3 radiance = lightColor * lightAtten;
    half3 diffuse = albedo * (1.0 - metallic) * diffTerm * burley;

    // Sheen (cloth edge gleam), Disney: Schlick on LoH tinted toward the albedo hue
    diffuse += sheenColor * _Sheen * SchlickWeight(LoH) * diffTerm;

    // Specular: GGX with a softness compression for the broad stylized gleam
    half specNoL = saturate(NoL);
    half D = D_GGX_Stylized(NoH, roughness);
    half V = V_SmithApprox(NoV, specNoL, roughness);
    half3 F = f0 + (half3(1, 1, 1) - f0) * SchlickWeight(LoH);
    half3 specular = D * V * F * specNoL;
    specular = specular / (1.0 + _SpecularSoftness * specular);

    // Clearcoat lobe (Disney: GTR1, fixed F0 0.04, 0.25 weight)
    if (_Clearcoat > 0.001)
    {
        half ccRough = lerp(0.4, 0.04, _ClearcoatGloss);
        half Dr = D_GTR1(NoH, ccRough);
        half Fr = 0.04 + 0.96 * SchlickWeight(LoH);
        half Vr = V_SmithApprox(NoV, specNoL, 0.25);
        specular += 0.25 * _Clearcoat * Dr * Fr * Vr * specNoL;
    }

    return (diffuse + specular) * radiance;
}

half4 StylizedPBRFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TRANSFORM_TEX(input.uv, _MainTex));
    half4 albedoAlpha = texColor * _BaseColor;
    StylizedPBRAlphaClip(texColor.a);

    half metallic, perceptualRoughness;
    SampleMetallicRoughness(input.uv, metallic, perceptualRoughness);

    // Normal mapping
    half3 normalTS = SampleNormalTS(input.uv);
    float sgn = input.tangentWS.w;
    float3 bitangent = sgn * cross(input.normalWS, input.tangentWS.xyz);
    half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent, input.normalWS);
    half3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tangentToWorld));

    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

    // Disney specular parameterization: F0 = lerp(0.08 * _Specular, albedo, metallic)
    half3 f0 = lerp(half3(0.08, 0.08, 0.08) * _Specular, albedoAlpha.rgb, metallic);

    // Sheen tint: white -> albedo hue
    half albedoLum = max(dot(albedoAlpha.rgb, half3(0.3, 0.6, 0.1)), 1e-3);
    half3 sheenColor = lerp(half3(1, 1, 1), albedoAlpha.rgb / albedoLum, _SheenTint);

    // InputData: only what the light loop and shadows need
    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = viewDirWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);

    Light mainLight = GetMainLight(inputData.shadowCoord);
    // Stylized shadow: lift and sharpen the received shadow so it bands instead of graying out
    half mainShadow = smoothstep(0.0, 0.75, mainLight.shadowAttenuation) * mainLight.distanceAttenuation;

    half3 color = EvaluateStylizedLight(
        albedoAlpha.rgb, metallic, perceptualRoughness, f0, sheenColor,
        normalWS, viewDirWS, mainLight.direction, mainLight.color, mainShadow);

    // Additional lights (per-pixel; both Forward and Forward+/cluster paths)
#ifdef _ADDITIONAL_LIGHTS
    uint pixelLightCount = GetAdditionalLightsCount();
#if USE_FORWARD_PLUS
    for (uint fpLightIndex = 0; fpLightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); fpLightIndex++)
    {
        Light light = GetAdditionalLight(fpLightIndex, input.positionWS, half4(1, 1, 1, 1));
        color += EvaluateStylizedLight(
            albedoAlpha.rgb, metallic, perceptualRoughness, f0, sheenColor,
            normalWS, viewDirWS, light.direction, light.color,
            light.distanceAttenuation * light.shadowAttenuation);
    }
#endif
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
        color += EvaluateStylizedLight(
            albedoAlpha.rgb, metallic, perceptualRoughness, f0, sheenColor,
            normalWS, viewDirWS, light.direction, light.color,
            light.distanceAttenuation * light.shadowAttenuation);
    LIGHT_LOOP_END
#endif

    // Ambient (SH), scaled by the shared _GI_Intensity knob
    half3 ambient = SampleSH(normalWS) * _GI_Intensity;
    color += ambient * albedoAlpha.rgb * (1.0 - metallic * 0.5);

    // Environment reflection for metals: matcap when the generator bound one, SH fallback otherwise
    half NoV = saturate(dot(normalWS, viewDirWS));
    if (metallic > 0.001)
    {
        half3 envRefl;
        if (_MatCap_SamplerArr_ID >= 0)
        {
            half3 viewNormal = mul((float3x3)UNITY_MATRIX_V, normalWS);
            float2 matcapUV = viewNormal.xy * 0.5 + 0.5;
            envRefl = SAMPLE_TEXTURE2D_LOD(_MatCap_Sampler, sampler_MatCap_Sampler, matcapUV, _BlurLevelMatcap).rgb
                      * _MatCapColor.rgb;
        }
        else
        {
            envRefl = SampleSH(reflect(-viewDirWS, normalWS));
        }
        // Two ways to apply the metal reflection, dialed by _MatcapMetalBlend:
        //   physical (0) — Fresnel/F0-weighted (envF): bright only at grazing edges, tinted by the
        //                  (often dark) albedo, so the front reads dark. Physically correct.
        //   flat     (1) — reflection weight = 1 everywhere: the matcap fills the whole surface
        //                  uniformly, matching DCL_Toon_Studio's flat chrome look.
        // We then REPLACE the (metal-diffuse-free, dark) base toward that reflection by
        // metallic * _StylizedMetalStrength — a lerp, not an add, so it doesn't just layer over a
        // dark surface. Strength 1 = full replace (matches toon); >1 over-drives brighter.
        half3 envF = f0 + (max(half3(1, 1, 1) * (1.0 - perceptualRoughness), f0) - f0) * SchlickWeight(NoV);
        half3 reflWeight = lerp(envF, half3(1, 1, 1), _MatcapMetalBlend);
        color = lerp(color, envRefl * reflWeight, saturate(metallic) * _StylizedMetalStrength);
    }

    // Artist rim: fresnel band, same exponent mapping as DCL_Toon so carried values feel familiar
    if (_RimLight > 0.5)
    {
        half rimArea = abs(1.0 - NoV);
        half rimPow = pow(rimArea, exp2(lerp(3.0, 0.0, _RimLight_Power)));
        half rimFeathered = saturate((rimPow - _RimLight_InsideMask) / max(1.0 - _RimLight_InsideMask, 1e-4));
        half rim = lerp(rimFeathered, step(_RimLight_InsideMask, rimPow), _RimSharpness);
        half3 rimColor = _RimLightColor.rgb * lerp(half3(1, 1, 1), mainLight.color, _Is_LightColor_RimLight);
        color += rim * rimColor * _RimLightIntensity;
    }

    // Emission: generator bakes x5 into _Emissive_Color; DCL_Toon multiplies a further x2.5 — match it.
    // _EmissionStrength (studio knob, default 1) lets the artist pull emissive back down: PBR's
    // emissive sits on a brighter additive base (ambient + rim) than toon's, so with bloom on it reads
    // hotter for the same texel — lower this (~0.4-0.6) to match the DCL_Toon look without touching post.
    // Flat-color-only emissives (no emissiveTexture in the glTF) never get a texture bound, so they
    // sample the shader's fallback "white" 1x1 — Unity still reports its real size via TexelSize, so a
    // tiny texel size means "no authored map" (any real emissive map is well over 16px). Those read a
    // touch weaker than their toon counterpart, so nudge them up without touching masked/mapped ones.
    half isFlatEmissive = step(_Emissive_Tex_TexelSize.z, 16.0) * step(_Emissive_Tex_TexelSize.w, 16.0);
    half emissiveBoost = lerp(1.0, 3.0, isFlatEmissive);
    half3 emissive = SAMPLE_TEXTURE2D(_Emissive_Tex, sampler_Emissive_Tex, TRANSFORM_TEX(input.uv, _Emissive_Tex)).rgb
                     * _Emissive_Color.rgb * 2.5 * _EmissionStrength * emissiveBoost;
    color += emissive;

    color = MixFog(color, input.fogFactor);

    // Alpha: same semantics as DCL_Toon (opaque unless a clipping mode is active)
    half alpha = 1.0;
    if (_IS_CLIPPING_MODE || _IS_CLIPPING_TRANSMODE)
    {
        alpha = saturate(texColor.a + _Tweak_transparency);
    }

    return half4(color, alpha);
}

#endif // DCL_STYLIZED_PBR_FORWARD_PASS_INCLUDED
