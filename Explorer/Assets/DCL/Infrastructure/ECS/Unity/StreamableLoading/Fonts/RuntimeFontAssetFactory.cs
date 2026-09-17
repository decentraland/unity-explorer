using DCL.Diagnostics;
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

        private const int REGULAR_WEIGHT_INDEX = 4;
        private const int BOLD_WEIGHT_INDEX = 7;

        private readonly TMP_FontAsset referenceFont;

        public RuntimeFontAssetFactory(TMP_FontAsset referenceFont)
        {
            this.referenceFont = referenceFont;
        }

        public FontFamilyAssets? Create(string assetName, string regularFilePath, string? boldFilePath = null, string? italicFilePath = null, string? boldItalicFilePath = null)
        {
            var textMeshProAssets = new List<TMP_FontAsset>(4);
            var uiToolkitAssets = new List<FontAsset>(4);

            TMP_FontAsset? textMeshProRegular = CreateTextMeshProAsset(assetName, regularFilePath, textMeshProAssets);
            FontAsset? uiToolkitRegular = CreateUIToolkitAsset(assetName, regularFilePath, uiToolkitAssets);

            if (textMeshProRegular == null || uiToolkitRegular == null)
            {
                FontFamilyAssets.Destroy(textMeshProAssets, uiToolkitAssets);
                return null;
            }

            WireVariant(assetName, boldFilePath, FontVariant.Bold, textMeshProRegular, uiToolkitRegular, textMeshProAssets, uiToolkitAssets);
            WireVariant(assetName, italicFilePath, FontVariant.Italic, textMeshProRegular, uiToolkitRegular, textMeshProAssets, uiToolkitAssets);
            WireVariant(assetName, boldItalicFilePath, FontVariant.BoldItalic, textMeshProRegular, uiToolkitRegular, textMeshProAssets, uiToolkitAssets);

            return new FontFamilyAssets(textMeshProRegular, uiToolkitRegular, textMeshProAssets, uiToolkitAssets);
        }

        private void WireVariant(string assetName, string? filePath, FontVariant variant, TMP_FontAsset textMeshProRegular, FontAsset uiToolkitRegular,
            List<TMP_FontAsset> textMeshProAssets, List<FontAsset> uiToolkitAssets)
        {
            if (filePath == null)
                return;

            string variantName = $"{assetName} {variant}";
            TMP_FontAsset? textMeshProVariant = CreateTextMeshProAsset(variantName, filePath, textMeshProAssets);
            FontAsset? uiToolkitVariant = CreateUIToolkitAsset(variantName, filePath, uiToolkitAssets);

            if (textMeshProVariant == null || uiToolkitVariant == null)
            {
                ReportHub.LogWarning(ReportCategory.SDK_FONTS, $"The {variant} face of the scene font \"{assetName}\" could not be read, the regular face stands in");
                return;
            }

            TMP_FontWeightPair[] textMeshProTable = textMeshProRegular.fontWeightTable;
            FontWeightPair[] uiToolkitTable = uiToolkitRegular.fontWeightTable;

            switch (variant)
            {
                case FontVariant.Bold:
                    textMeshProTable[BOLD_WEIGHT_INDEX].regularTypeface = textMeshProVariant;
                    uiToolkitTable[BOLD_WEIGHT_INDEX].regularTypeface = uiToolkitVariant;
                    break;
                case FontVariant.Italic:
                    textMeshProTable[REGULAR_WEIGHT_INDEX].italicTypeface = textMeshProVariant;
                    uiToolkitTable[REGULAR_WEIGHT_INDEX].italicTypeface = uiToolkitVariant;
                    break;
                case FontVariant.BoldItalic:
                    textMeshProTable[BOLD_WEIGHT_INDEX].italicTypeface = textMeshProVariant;
                    uiToolkitTable[BOLD_WEIGHT_INDEX].italicTypeface = uiToolkitVariant;
                    break;
            }
        }

        private TMP_FontAsset? CreateTextMeshProAsset(string name, string filePath, List<TMP_FontAsset> owned)
        {
            TMP_FontAsset? asset = TMP_FontAsset.CreateFontAsset(filePath, 0, SAMPLING_POINT_SIZE, ATLAS_PADDING, RENDER_MODE, ATLAS_SIZE, ATLAS_SIZE);

            if (asset == null)
                return null;

            asset.name = name;
            owned.Add(asset);

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

        private static FontAsset? CreateUIToolkitAsset(string name, string filePath, List<FontAsset> owned)
        {
            FontAsset? asset = FontAsset.CreateFontAsset(filePath, 0, SAMPLING_POINT_SIZE, ATLAS_PADDING, RENDER_MODE, ATLAS_SIZE, ATLAS_SIZE);

            if (asset == null)
                return null;

            asset.name = name;
            owned.Add(asset);
            return asset;
        }
    }
}
