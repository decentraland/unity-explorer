#ifndef DCL_EMOTES_INPUT_INCLUDED
#define DCL_EMOTES_INPUT_INCLUDED

// Outfit Studio — emote-thumbnail shader inputs.
//
// The CBUFFER mirrors DCL_StylizedPBR_Input.hlsl property-for-property even though this shader
// reads almost none of it. Two reasons, both about the studio's shader switcher reassigning
// material.shader in place:
//   1. SRP-batcher compatibility needs every non-texture material property in UnityPerMaterial.
//   2. A property the ACTIVE shader doesn't declare doesn't survive the swap — it falls back to
//      the next shader's default. That's the bug documented around _IsStylizedMetallic in
//      StudioAvatarShaderSwitcher; declaring the full set means DCL_Emotes is a lossless
//      round-trip and the artist can flip back to Toon/PBR with their look intact.

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
float _OutlineAsMask;
float _Outline_Width;
float _Outline_DetailSuppress;
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

// This shader ignores albedo COLOR, but it must not ignore albedo ALPHA: cutout wearables (hair
// cards, lashes, fringed cloth) are alpha-tested quads, and skipping the clip would render them as
// solid white slabs. Same test the toon and PBR passes use.
// _IS_CLIPPING_* are the toon dynamic-branch keywords; ToonMaterialGenerator enables them on the
// material and the studio shader switcher carries them across shader swaps.
void EmotesAlphaClip(half albedoAlpha)
{
    if (_IS_CLIPPING_MODE || _IS_CLIPPING_TRANSMODE)
    {
        clip(albedoAlpha * _BaseColor.a - max(_Clipping_Level, 0.001));
    }
}

#endif // DCL_EMOTES_INPUT_INCLUDED
