using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using Utility;

namespace ECS.StreamableLoading.Fonts
{
    public class RuntimeFontAssetFactory
    {
        private const int SAMPLING_POINT_SIZE = 90;

        private const int ATLAS_PADDING = 9;

        private const int ATLAS_SIZE = 1024;
        private const GlyphRenderMode RENDER_MODE = GlyphRenderMode.SDFAA;

        private const int SDF_PACKING_MODIFIER = 1;

        private readonly TMP_FontAsset referenceFont;

        public RuntimeFontAssetFactory(TMP_FontAsset referenceFont)
        {
            this.referenceFont = referenceFont;
        }

        public FontFamilyAssets? Create(string assetName, string filePath)
        {
            TMP_FontAsset? textMeshProRegular = CreateTextMeshProAsset(assetName, filePath);
            FontAsset? uiToolkitRegular = CreateUIToolkitAsset(assetName, filePath);

            if (textMeshProRegular == null || uiToolkitRegular == null)
            {
                FontFamilyAssets.Destroy(textMeshProRegular, uiToolkitRegular);
                return null;
            }

            return new FontFamilyAssets(textMeshProRegular, uiToolkitRegular);
        }

        private TMP_FontAsset? CreateTextMeshProAsset(string name, string filePath)
        {
            TMP_FontAsset? asset = TMP_FontAsset.CreateFontAsset(filePath, 0, SAMPLING_POINT_SIZE, ATLAS_PADDING, RENDER_MODE, ATLAS_SIZE, ATLAS_SIZE);

            if (asset == null)
                return null;

            asset.name = name;

            // CreateFontAsset looks up "TextMeshPro/Mobile/Distance Field" by name: that shader is in the always-included list
            Material generated = asset.material;
            ShaderUtilities.GetShaderPropertyIDs();

            var material = new Material(referenceFont.material) { name = $"{name} Material" };
            material.SetTexture(ShaderUtilities.ID_MainTex, asset.atlasTexture);
            material.SetFloat(ShaderUtilities.ID_TextureWidth, ATLAS_SIZE);
            material.SetFloat(ShaderUtilities.ID_TextureHeight, ATLAS_SIZE);
            material.SetFloat(ShaderUtilities.ID_GradientScale, ATLAS_PADDING + SDF_PACKING_MODIFIER);
            material.SetFloat(ShaderUtilities.ID_WeightNormal, asset.normalStyle);
            material.SetFloat(ShaderUtilities.ID_WeightBold, asset.boldStyle);

            asset.material = material;
            UnityObjectUtils.SafeDestroy(generated);

            asset.fallbackFontAssetTable = new List<TMP_FontAsset> { referenceFont };

            return asset;
        }

        private static FontAsset? CreateUIToolkitAsset(string name, string filePath)
        {
            FontAsset? asset = FontAsset.CreateFontAsset(filePath, 0, SAMPLING_POINT_SIZE, ATLAS_PADDING, RENDER_MODE, ATLAS_SIZE, ATLAS_SIZE);

            if (asset == null)
                return null;

            asset.name = name;
            return asset;
        }
    }
}
