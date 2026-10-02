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
        public static readonly string PATH = Path.Combine(Application.dataPath, "DCL/SDKComponents/Fonts/LiberationSans-Regular.ttf");

        private const int SAMPLING_POINT_SIZE = 90;
        private const int ATLAS_PADDING = 9;
        private const int ATLAS_SIZE = 1024;

        public static TMP_FontAsset CreateTextMeshProFont(int atlasSize = ATLAS_SIZE) =>
            TMP_FontAsset.CreateFontAsset(PATH, 0, SAMPLING_POINT_SIZE, ATLAS_PADDING, GlyphRenderMode.SDFAA, atlasSize, atlasSize);

        public static FontAsset CreateUIToolkitFont() =>
            FontAsset.CreateFontAsset(PATH, 0, SAMPLING_POINT_SIZE, ATLAS_PADDING, GlyphRenderMode.SDFAA, ATLAS_SIZE, ATLAS_SIZE);

        /// <summary>
        ///     Creates a font the way <see cref="LoadFontSystem" /> loads it from a converted font bundle, with a stand-in
        ///     bundle holding both font assets. Destroy it with <see cref="DestroyBundledFont" />.
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

        /// <summary>
        ///     Disposes the font and destroys the font assets its stand-in bundle holds.
        /// </summary>
        public static void DestroyBundledFont(FontData font)
        {
            SceneFontAssets assets = font.Asset;
            font.Dispose(force: true);
            Object.DestroyImmediate(assets.TextMeshProFont);
            Object.DestroyImmediate(assets.UIToolkitFont);
        }
    }
}
