#ifndef DCL_STYLIZED_PBR_INPUT_INCLUDED
#define DCL_STYLIZED_PBR_INPUT_INCLUDED

// Outfit Studio shader — shares DCL_Toon's material property names so the studio's shader
// switcher can reassign material.shader with no value loss. Consumed by all passes.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _MainTex_ST;
float4 _NormalMap_ST;
float4 _Emissive_Tex_ST;
float4 _Emissive_Tex_TexelSize;
half4 _BaseColor;
float4 _Emissive_Color;
float4 _MatCapColor;
float4 _RimLightColor;
float4 _Outline_Color;
float _Clipping_Level;
float _Tweak_transparency;
float _BumpScale;
float _Metallic;
float _Smoothness;
float _Specular;
float _DiffuseWrap;
float _ShadowSharpness;
float _SpecularSoftness;
float _Sheen;
float _SheenTint;
float _Clearcoat;
float _ClearcoatGloss;
float _GI_Intensity;
float _BlurLevelMatcap;
float _MatcapMetalBlend;
float _StylizedMetalStrength;
float _EmissionStrength;
float _RimLight;
float _RimLightIntensity;
float _RimLight_Power;
float _RimLight_InsideMask;
float _RimSharpness;
float _Is_LightColor_RimLight;
float _OutlineEnabled;
float _Outline_Width;
float _Is_BlendBaseColor;
int _MainTexArr_ID;
int _NormalMapArr_ID;
int _MatCap_SamplerArr_ID;
int _Emissive_TexArr_ID;
int _MetallicGlossMapArr_ID;
int _IsStylizedMetallic;
CBUFFER_END

TEXTURE2D(_MainTex);            SAMPLER(sampler_MainTex);
TEXTURE2D(_NormalMap);          SAMPLER(sampler_NormalMap);
TEXTURE2D(_MetallicGlossMap);   SAMPLER(sampler_MetallicGlossMap);
TEXTURE2D(_Emissive_Tex);       SAMPLER(sampler_Emissive_Tex);
TEXTURE2D(_MatCap_Sampler);     SAMPLER(sampler_MatCap_Sampler);

// Same early-out the toon passes use (DCL_Toon: AlphaClip(_MainTex.a * _BaseColor.a, _Clipping_Level)).
// _IS_CLIPPING_* are the toon dynamic-branch keywords; ToonMaterialGenerator enables them on the
// material, and the studio shader switcher restores them across shader swaps.
void StylizedPBRAlphaClip(half albedoAlpha)
{
    if (_IS_CLIPPING_MODE || _IS_CLIPPING_TRANSMODE)
    {
        clip(albedoAlpha * _BaseColor.a - max(_Clipping_Level, 0.001));
    }
}

half4 SampleAlbedo(float2 uv)
{
    return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TRANSFORM_TEX(uv, _MainTex)) * _BaseColor;
}

// Metallic in .b, perceptual roughness in .g (glTF ORM convention; the generator binds the GLB's
// metallicRoughness texture — or a baked 1x1 uniform mask — as _MetallicGlossMap).
void SampleMetallicRoughness(float2 uv, out half metallic, out half perceptualRoughness)
{
    if (_IsStylizedMetallic > 0 && _MetallicGlossMapArr_ID >= 0)
    {
        half4 mr = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, TRANSFORM_TEX(uv, _MainTex));
        metallic = mr.b;
        perceptualRoughness = max(mr.g, 0.045);
    }
    else
    {
        metallic = _Metallic;
        perceptualRoughness = max(1.0 - _Smoothness, 0.045);
    }
}

half3 SampleNormalTS(float2 uv)
{
    if (_NormalMapArr_ID >= 0)
    {
        return UnpackNormalScale(
            SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, TRANSFORM_TEX(uv, _NormalMap)), _BumpScale);
    }
    return half3(0.0, 0.0, 1.0);
}

#endif // DCL_STYLIZED_PBR_INPUT_INCLUDED
