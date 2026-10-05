using System.Collections.Generic;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace ECS.StreamableLoading.Fonts
{
    public class RuntimeFontAssetFactory
    {
        private const int SDF_PACKING_MODIFIER = 1;

        private static readonly ProfilerMarker ADOPT_BUNDLED_MARKER = new ($"{nameof(RuntimeFontAssetFactory)}.{nameof(AdoptBundled)}");

        private readonly TMP_FontAsset referenceFont;

        public RuntimeFontAssetFactory(TMP_FontAsset referenceFont)
        {
            this.referenceFont = referenceFont;
        }

        /// <summary>
        ///     Creates the TMP material for a bundled font asset and sets the built-in font as its fallback.
        /// </summary>
        public SceneFontAssets AdoptBundled(string assetName, TMP_FontAsset textMeshPro, FontAsset uiToolkit)
        {
            using ProfilerMarker.AutoScope _ = ADOPT_BUNDLED_MARKER.Auto();

            Material material = CreateMaterial(assetName, textMeshPro);
            textMeshPro.material = material;
            textMeshPro.fallbackFontAssetTable = new List<TMP_FontAsset> { referenceFont };
            return new SceneFontAssets(textMeshPro, uiToolkit, material);
        }

        private Material CreateMaterial(string name, TMP_FontAsset asset)
        {
            ShaderUtilities.GetShaderPropertyIDs();

            var material = new Material(referenceFont.material);
            material.name = $"{name} Material";
            material.SetTexture(ShaderUtilities.ID_MainTex, asset.atlasTexture);
            material.SetFloat(ShaderUtilities.ID_TextureWidth, asset.atlasWidth);
            material.SetFloat(ShaderUtilities.ID_TextureHeight, asset.atlasHeight);
            material.SetFloat(ShaderUtilities.ID_GradientScale, asset.atlasPadding + SDF_PACKING_MODIFIER);
            material.SetFloat(ShaderUtilities.ID_WeightNormal, asset.normalStyle);
            material.SetFloat(ShaderUtilities.ID_WeightBold, asset.boldStyle);
            return material;
        }
    }
}
