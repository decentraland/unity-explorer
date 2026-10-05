using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Fonts;
using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using Object = UnityEngine.Object;

namespace ECS.TestSuite
{
    public static class TestFonts
    {
        private const int SAMPLING_POINT_SIZE = 90;
        private const int ATLAS_PADDING = 9;
        private const int ATLAS_SIZE = 1024;

        public static readonly string PATH = Path.Combine(Application.dataPath, "DCL/SDKComponents/Fonts/LiberationSans-Regular.ttf");

        public static TMP_FontAsset CreateTextMeshProFont(int atlasSize = ATLAS_SIZE) =>
            TMP_FontAsset.CreateFontAsset(PATH, 0, SAMPLING_POINT_SIZE, ATLAS_PADDING, GlyphRenderMode.SDFAA, atlasSize, atlasSize);

        public static FontAsset CreateUIToolkitFont() =>
            FontAsset.CreateFontAsset(PATH, 0, SAMPLING_POINT_SIZE, ATLAS_PADDING, GlyphRenderMode.SDFAA, ATLAS_SIZE, ATLAS_SIZE);

        /// <summary>
        ///     Release the font with <see cref="DestroyBundledFont" />, because disposing the font does not destroy its font assets.
        /// </summary>
        public static FontData CreateBundledFont(TMP_FontAsset referenceFont)
        {
            TMP_FontAsset textMeshPro = CreateTextMeshProFont();
            FontAsset uiToolkit = CreateUIToolkitFont();
            Material generatedMaterial = textMeshPro.material;
            var bundle = new AssetBundleData(null!, new Object[] { textMeshPro, uiToolkit }, null, Array.Empty<AssetBundleData>());
            bundle.AcquireRef();

            var font = new FontData(new RuntimeFontAssetFactory(referenceFont).AdoptBundled("Custom", textMeshPro, uiToolkit), bundle);
            Object.DestroyImmediate(generatedMaterial);
            return font;
        }

        public static void DestroyBundledFont(FontData font)
        {
            SceneFontAssets assets = font.Asset;
            font.Dispose(force: true);
            Object.DestroyImmediate(assets.TextMeshProFont);
            Object.DestroyImmediate(assets.UIToolkitFont);
        }
    }
}
