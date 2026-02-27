using UnityEngine;
using UnityEngine.Rendering;

namespace DCL.Shaders
{
    public static class ShaderUtils
    {
        //Toon shader properties
        public static readonly int LightDir = Shader.PropertyToID("_LightDir");
        public static readonly int LightColor = Shader.PropertyToID("_LightColor");
        public static readonly int TintColor = Shader.PropertyToID("_TintColor");

        public static readonly int GlossMatCap = Shader.PropertyToID("_GlossMatCap");
        public static readonly int FresnelMatCap = Shader.PropertyToID("_FresnelMatCap");
        public static readonly int MatCap = Shader.PropertyToID("_MatCap");
        public static readonly int DitherFade = Shader.PropertyToID("_DitherFade");

        //Avatar specific properties
        public static readonly int EyesTexture = Shader.PropertyToID("_EyesTexture");
        public static readonly int EyeTint = Shader.PropertyToID("_EyeTint");
        public static readonly int IrisMask = Shader.PropertyToID("_IrisMask");
        public static readonly int TintMask = Shader.PropertyToID("_TintMask");
        public static readonly string SSAO_OFF_KEYWORD = "_SSAO_OFF";

        // keywords
        public const string KEYWORD_OCCLUSION = "_OCCLUSION";
        public const string KEYWORD_EMISSION = "_EMISSION";
        public const string KEYWORD_ALPHA_TEST = "_ALPHATEST_ON";
        public const string KEYWORD_SPECGLOSSMAP = "_SPECGLOSSMAP";
        public const string KEYWORD_NORMALMAP = "_NORMALMAP";
        public const string KEYWORD_METALLICSPECGLOSSMAP = "_METALLICSPECGLOSSMAP";
        public const string KEYWORD_SURFACE_TYPE_TRANSPARENT = "_SURFACE_TYPE_TRANSPARENT";
        public const string RENDERER_TYPE = "RenderType";

        // fog
        public const string FOG_LINEAR = "FOG_LINEAR";
        public const string FOG_EXP = "FOG_EXP";
        public const string FOG_EXP2 = "FOG_EXP2";

        // Forward Plus keywords (new shader)
        public const string FW_PLUS = "_FORWARD_PLUS";
        public const string FW_PLUS_LIGHT_SHADOWS = "_ADDITIONAL_LIGHT_SHADOWS";
        public const string FW_PLUS_SHADOWS_CASCADE = "_MAIN_LIGHT_SHADOWS_CASCADE";
        public const string FW_PLUS_SHADOWS_SOFT = "_SHADOWS_SOFT";

        // always inactive keywords
        public const string KEYWORD_ALPHA_BLEND = "_ALPHABLEND_ON";
        public const string KEYWORD_ALPHA_PREMULTIPLY = "_ALPHAPREMULTIPLY_ON";
        public const string KEYWORD_SPECULAR_HIGHLIGHTS_OFF = "_SPECULARHIGHLIGHTS_OFF";
        public const string KEYWORD_VERTEX_COLOR_ON = "VERTEX_COLOR_ON";
        public const string KEYWORD_ENV_REFLECTIONS_OFF = "_ENVIRONMENTREFLECTIONS_OFF";

        //Lit properties
        public static readonly int AlphaTexture = Shader.PropertyToID("_AlphaTexture");

        // NOTE(Kinerius) All those Shader properties that are not supported are there in purpose since they are being
        //                used by GLTFast shaders and might be specs from GLTF format that we have to support in the future
        public static readonly int SpecGlossMap = Shader.PropertyToID("_SpecGlossMap");
        public static readonly int SpecGlossMapScaleTransform = Shader.PropertyToID("_SpecGlossMap_ST"); // we dont support this yet
        public static readonly int SpecGlossMapRotation = Shader.PropertyToID("_SpecGlossMapRotation"); // we dont support this yet
        public static readonly int SpecGlossMapUVChannelPropId = Shader.PropertyToID("_SpecGlossMapUVChannel"); // we dont support this yet

        public static readonly int MetallicRoughnessMapPropId = Shader.PropertyToID("_MetallicGlossMap");
        public static readonly int MetallicRoughnessMapScaleTransformPropId = Shader.PropertyToID("metallicRoughnessTexture_ST"); // we dont support this yet
        public static readonly int MetallicRoughnessMapRotationPropId = Shader.PropertyToID("metallicRoughnessTextureRotation"); // we dont support this yet
        public static readonly int MetallicRoughnessMapUVChannelPropId = Shader.PropertyToID("metallicRoughnessTextureUVChannel"); // we dont support this yet

        public static readonly int SmoothnessTextureChannel = Shader.PropertyToID("_SmoothnessTextureChannel");
        public static readonly int SpecColor = Shader.PropertyToID("_SpecColor");
        public static readonly int GlossMapScale = Shader.PropertyToID("_GlossMapScale");
        public static readonly int Glossiness = Shader.PropertyToID("_Glossiness");
        public static readonly int MetallicGlossMap = Shader.PropertyToID("_MetallicGlossMap");

        public static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        public static readonly int BaseMapScaleTransform = Shader.PropertyToID("_BaseMap_ST"); // we dont support this yet
        public static readonly int BaseMapRotation = Shader.PropertyToID("_BaseMapRotation"); // we dont support this yet
        public static readonly int BaseMapUVs = Shader.PropertyToID("_BaseMapUVs");

        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public static readonly int NormalMapUVs = Shader.PropertyToID("_NormalMapUVs");
        public static readonly int MetallicMapUVs = Shader.PropertyToID("_MetallicMapUVs");
        public static readonly int EmissiveMapUVs = Shader.PropertyToID("_EmissiveMapUVs");

        public static readonly int Metallic = Shader.PropertyToID("_Metallic");
        public static readonly int Smoothness = Shader.PropertyToID("_Smoothness");

        public static readonly int Cutoff = Shader.PropertyToID("_Cutoff");
        public static readonly int BumpMap = Shader.PropertyToID("_BumpMap");
        public static readonly int BumpScale = Shader.PropertyToID("_BumpScale");
        public static readonly int BumpMapRotationPropId = Shader.PropertyToID("_BumpMapRotation"); // we dont support this yet
        public static readonly int BumpMapScaleTransformPropId = Shader.PropertyToID("_BumpMap_ST"); // we dont support this yet
        public static readonly int BumpMapUVChannelPropId = Shader.PropertyToID("_BumpMapUVChannel"); // we dont support this yet

        public static readonly int OcclusionMap = Shader.PropertyToID("_OcclusionMap");
        public static readonly int OcclusionStrength = Shader.PropertyToID("_OcclusionStrength");
        public static readonly int OcclusionMapRotation = Shader.PropertyToID("_OcclusionMapRotation"); // we dont support this yet
        public static readonly int OcclusionMapScaleTransform = Shader.PropertyToID("_OcclusionMap_ST"); // we dont support this yet
        public static readonly int OcclusionMapUVChannel = Shader.PropertyToID("_OcclusionMapUVChannel"); // we dont support this yet

        public static readonly int EmissionMap = Shader.PropertyToID("_EmissionMap");
        public static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        public static readonly int EmissionMapRotation = Shader.PropertyToID("_EmissionMapRotation"); // we dont support this yet
        public static readonly int EmissionMapScaleTransform = Shader.PropertyToID("_EmissionMap_ST"); // we dont support this yet
        public static readonly int EmissionMapUVChannel = Shader.PropertyToID("_EmissionMapUVChannel"); // we dont support this yet

        public static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
        public static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
        public static readonly int SrcBlendAlpha = Shader.PropertyToID("_SrcBlendAlpha");
        public static readonly int DstBlendAlpha = Shader.PropertyToID("_DstBlendAlpha");
        public static readonly int BlendOp = Shader.PropertyToID("_BlendOp");
        public static readonly int BlendOpAlpha = Shader.PropertyToID("_BlendOpAlpha");
        public static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
        public static readonly int AlphaClip = Shader.PropertyToID("_AlphaClip");
        public static readonly int Cull = Shader.PropertyToID("_Cull");

        public static readonly int SpecularHighlights = Shader.PropertyToID("_SpecularHighlights");
        public static readonly int EnvironmentReflections = Shader.PropertyToID("_EnvironmentReflections");
        public static readonly int Surface = Shader.PropertyToID("_Surface");

        //Lit keywords
        public static readonly string NormalMapKeyword = "_NORMALMAP";
        public static readonly string MetallicSpecGlossmapKeyword = "_METALLICSPECGLOSSMAP";
        public static readonly string AlphaTestOnKeyword = "_ALPHATEST_ON";
        public static readonly string AlphaPremultiplyOnKeyword = "_ALPHAPREMULTIPLY_ON";

        //Lit rendertypes
        public static readonly string RenderType = "RenderType";
        public static readonly string Transparent = "Transparent";
        public static readonly string TransparentCutout = "TransparentCutout";
        public static readonly string Opaque = "Opaque";

        /// <summary>
        ///     Upgrades an AB Material from 2020 to 2021
        /// </summary>
        /// <param name="material"></param>
        public static void UpgradeMaterial_2020_To_2021(Material material)
        {
            int originalRenderQueue = material.renderQueue;
            material.shader = Shader.Find("DCL/Universal Render Pipeline/Lit");
            material.renderQueue = originalRenderQueue;
            float cutoff = material.GetFloat(Cutoff);

            if (material.HasTexture(BumpMap) && material.GetTexture(BumpMap) != null)
                material.EnableKeyword(NormalMapKeyword);

            if (material.HasTexture(MetallicGlossMap) && material.GetTexture(MetallicGlossMap) != null)
                material.EnableKeyword(MetallicSpecGlossmapKeyword);

            if (originalRenderQueue == (int)RenderQueue.Transparent)
            {
                material.SetOverrideTag(RenderType, Transparent);
                material.SetInt(SrcBlend, (int)BlendMode.SrcAlpha);
                material.SetInt(DstBlend, (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt(ZWrite, 0);
                material.DisableKeyword(AlphaTestOnKeyword);
                material.DisableKeyword(AlphaPremultiplyOnKeyword);
                return;
            }

            if (originalRenderQueue > 2600 || cutoff > 0)
            {
                material.SetOverrideTag(RenderType, TransparentCutout);
                material.SetInt(SrcBlend, (int)BlendMode.One);
                material.SetInt(DstBlend, (int)BlendMode.Zero);
                material.SetInt(ZWrite, 1);
                material.SetFloat(AlphaClip, 1);
                material.EnableKeyword(AlphaTestOnKeyword);
                material.DisableKeyword(AlphaPremultiplyOnKeyword);
                return;
            }

            material.SetOverrideTag(RenderType, Opaque);
            material.SetInt(SrcBlend, (int)BlendMode.One);
            material.SetInt(DstBlend, (int)BlendMode.Zero);
            material.SetInt(ZWrite, 1);
            material.DisableKeyword(AlphaTestOnKeyword);
            material.DisableKeyword(AlphaPremultiplyOnKeyword);
        }
    }
}
