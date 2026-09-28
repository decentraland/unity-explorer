using NUnit.Framework;
using UnityEditor;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace DCL.UIToolkit.Tests
{
    public class DCLTextSettingsShould
    {
        private const string TEXT_SETTINGS_PATH = "Assets/DCL/UIToolkit/DCLTextSettings.asset";
        private const string PRIMARY_FONT_PATH = "Assets/DCL/UIToolkit/Fonts/Inter_18pt-Regular SDF.asset";

        private PanelTextSettings textSettings = null!;
        private FontAsset primaryFont = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            FontEngine.InitializeFontEngine();
        }

        [SetUp]
        public void SetUp()
        {
            textSettings = AssetDatabase.LoadAssetAtPath<PanelTextSettings>(TEXT_SETTINGS_PATH);
            primaryFont = AssetDatabase.LoadAssetAtPath<FontAsset>(PRIMARY_FONT_PATH);

            Assert.That(textSettings, Is.Not.Null, $"Could not load {TEXT_SETTINGS_PATH}");
            Assert.That(primaryFont, Is.Not.Null, $"Could not load {PRIMARY_FONT_PATH}");
        }

        [Test]
        public void DeclareFallbackFonts()
        {
            Assert.That(textSettings.fallbackFontAssets, Is.Not.Null.And.Not.Empty,
                "UI Toolkit text has no fallback fonts. Any glyph the primary font lacks makes Unity build its OS "
                + "fallback list, which opens and parses every font installed on the machine on the main thread.");

            CollectionAssert.DoesNotContain(textSettings.fallbackFontAssets, null,
                "A null entry silently covers nothing and lets those characters reach the OS fallback.");
        }

        [TestCase("Latin", "Az")]
        [TestCase("Latin accents", "ñç")]
        [TestCase("Vietnamese", "ũệ")]
        [TestCase("Cyrillic", "Яд")]
        [TestCase("Greek", "Ωπ")]
        [TestCase("Chinese", "测试")]
        [TestCase("Japanese kana", "ひカ")]
        [TestCase("Korean Hangul", "한글")]
        public void RenderWithoutReachingTheOsFontList(string script, string sample)
        {
            foreach (char character in sample)
                Assert.That(CanRender(character), Is.True,
                    $"No font reachable from DCLTextSettings can draw '{character}' (U+{(int)character:X4}, {script}). "
                    + "Unity will enumerate the OS font list on the main thread the first time it appears.");
        }

        private bool CanRender(char character)
        {
            if (Covers(primaryFont, character))
                return true;

            foreach (FontAsset fallback in textSettings.fallbackFontAssets)
                if (Covers(fallback, character))
                    return true;

            return false;
        }

        private static bool Covers(FontAsset fontAsset, char character)
        {
            if (fontAsset == null)
                return false;

            if (fontAsset.HasCharacter(character))
                return true;

            // A dynamic asset bakes glyphs on demand, so the source font is what decides coverage.
            if (fontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic || fontAsset.sourceFontFile == null)
                return false;

            if (FontEngine.LoadFontFace(fontAsset.sourceFontFile, (int) fontAsset.faceInfo.pointSize) != FontEngineError.Success)
                return false;

            try { return FontEngine.TryGetGlyphIndex(character, out uint glyphIndex) && glyphIndex != 0; }
            finally { FontEngine.UnloadFontFace(); }
        }
    }
}
