using Data;
using GLTFast;
using GLTFast.Logging;
using GLTFast.Materials;
using UnityEngine;
using UnityEngine.Rendering;

namespace Loading
{
    public class ToonMaterialGenerator : IMaterialGenerator
    {
        private const float EMISSIVE_MAGIC_NUMBER = 5f;

        private static readonly int MAIN_TEX_ID = Shader.PropertyToID("_MainTex");
        private static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
        private static readonly int EMISSIVE_TEX_ID = Shader.PropertyToID("_Emissive_Tex");
        private static readonly int EMISSIVE_COLOR_ID = Shader.PropertyToID("_Emissive_Color");

        private static readonly int TWEAK_TRANSPARENCY_ID = Shader.PropertyToID("_Tweak_transparency");
        private static readonly int CLIPPING_LEVEL_ID = Shader.PropertyToID("_Clipping_Level");
        private static readonly int Z_WRITE_MODE_ID = Shader.PropertyToID("_ZWriteMode");
        private static readonly int SRC_BLEND_ID = Shader.PropertyToID("_SrcBlend");
        private static readonly int DST_BLEND_ID = Shader.PropertyToID("_DstBlend");
        private static readonly int CULL_MODE_ID = Shader.PropertyToID("_CullMode");

        //private readonly AvatarColors _avatarColors;

        private Material m_DefaultMaterial;

        public ToonMaterialGenerator()
        {
            
        }
        
        public ToonMaterialGenerator(AvatarColors avatarColors)
        {
            //_avatarColors = avatarColors;
        }

        public Material GenerateMaterial(int materialIndex, GLTFast.Schema.Material gltfMaterial, IGltfReadable gltf,
            bool pointsSupport = false)
        {
            var isFacialFeature = IsFacialFeature(gltfMaterial.name);
            var mat = new Material(isFacialFeature ? CommonAssets.FacialFeaturesMaterial : CommonAssets.AvatarMaterial)
                { name = gltfMaterial.name };

            // Base color and texture
            var baseColor = gltfMaterial.pbrMetallicRoughness.BaseColor;
            mat.SetColor(BASE_COLOR_ID, baseColor);

            if (gltfMaterial.pbrMetallicRoughness.baseColorTexture.index != -1)
            {
                mat.SetTexture(MAIN_TEX_ID, gltf.GetTexture(gltfMaterial.pbrMetallicRoughness.baseColorTexture.index));
            }

            // Emission
            mat.SetColor(EMISSIVE_COLOR_ID, gltfMaterial.Emissive * EMISSIVE_MAGIC_NUMBER);

            if (gltfMaterial.emissiveTexture.index != -1)
            {
                mat.SetTexture(EMISSIVE_TEX_ID, gltf.GetTexture(gltfMaterial.emissiveTexture.index));
            }

            // Alpha
            if (isFacialFeature)
            {
                mat.SetInt(Z_WRITE_MODE_ID, 0);
                mat.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else if (gltfMaterial.GetAlphaMode() == GLTFast.Schema.Material.AlphaMode.Blend)
            {
                mat.DisableKeyword("_IS_CLIPPING_MODE");
                mat.EnableKeyword("_IS_CLIPPING_TRANSMODE");
                mat.SetFloat(TWEAK_TRANSPARENCY_ID, 0.0f - (1.0f - baseColor.a));
                mat.SetFloat(CLIPPING_LEVEL_ID, 0);
                mat.SetInt(Z_WRITE_MODE_ID, 0);

                mat.SetFloat(SRC_BLEND_ID, (int)BlendMode.SrcAlpha);
                mat.SetFloat(DST_BLEND_ID, (int)BlendMode.OneMinusSrcAlpha);

                // I don't think we need to set this but if some transparency stuff is messed up maybe we do
                // mat.SetFloat(ALPHA_SRC_BLEND_TARGET, originalMaterial.GetFloat(ALPHA_SRC_BLEND_ORIGINAL));
                // mat.SetFloat(ALPHA_DST_BLEND_TARGET, originalMaterial.GetFloat(ALPHA_DST_BLEND_ORIGINAL));
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            else if (gltfMaterial.GetAlphaMode() == GLTFast.Schema.Material.AlphaMode.Mask)
            {
                mat.EnableKeyword("_IS_CLIPPING_MODE");
                mat.DisableKeyword("_IS_CLIPPING_TRANSMODE");
                mat.SetFloat(TWEAK_TRANSPARENCY_ID, 0.0f - (1.0f - baseColor.a));
                mat.SetFloat(CLIPPING_LEVEL_ID, gltfMaterial.alphaCutoff);
                mat.SetInt(Z_WRITE_MODE_ID, 1);
                mat.renderQueue = (int)RenderQueue.AlphaTest;
            }

            // Backface culling
            mat.SetInt(CULL_MODE_ID, (int)CullMode.Back);

            return mat;
        }


        private static bool IsFacialFeature(string gltfMaterialName)
        {
            return gltfMaterialName is "AvatarEyebrows_MAT" or "AvatarEyes_MAT" or "AvatarMouth_MAT"
                or "AvatarMaskEyebrows_MAT" or "AvatarMaskEyes_MAT" or "AvatarMaskMouth_MAT";
        }

        // private bool TryGetColorOverride(string materialName, out Color color)
        // {
        //     if (materialName.Contains("skin", StringComparison.OrdinalIgnoreCase))
        //     {
        //         color = _avatarColors.Skin;
        //         return true;
        //     }
        //
        //     if (materialName.Contains("hair", StringComparison.OrdinalIgnoreCase))
        //     {
        //         color = _avatarColors.Hair;
        //         return true;
        //     }
        //
        //     color = default;
        //     return false;
        // }

        public Material GetDefaultMaterial(bool pointsSupport = false)
        {
            // Called for primitives with no material. The Avatar_Toon template carries an
            // authored HDR _Emissive_Color, so return a copy with it zeroed to match the
            // glTF default material (and the explorer), instead of the glowing shared asset.
            // Cached so multiple material-less primitives in the same GLB share one instance.
            if (m_DefaultMaterial == null)
            {
                m_DefaultMaterial = new Material(CommonAssets.AvatarMaterial) { name = "Default_MAT" };
                m_DefaultMaterial.SetColor(EMISSIVE_COLOR_ID, Color.black);
                m_DefaultMaterial.SetColor(BASE_COLOR_ID, Color.white);
                m_DefaultMaterial.SetInt(CULL_MODE_ID, (int)CullMode.Back);
            }
            return m_DefaultMaterial;
        }

        public void SetLogger(ICodeLogger logger)
        {
            // We don't need a logger
        }
    }
}