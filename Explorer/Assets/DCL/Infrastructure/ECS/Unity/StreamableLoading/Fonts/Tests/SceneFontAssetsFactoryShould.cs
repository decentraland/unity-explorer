using ECS.TestSuite;
using NUnit.Framework;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;
using Object = UnityEngine.Object;

namespace ECS.StreamableLoading.Fonts.Tests
{
    [TestFixture]
    public class SceneFontAssetsFactoryShould
    {
        private const string ASSET_NAME = "Liberation";

        // Small enough for a handful of glyphs to fill a page
        private const int BUNDLED_ATLAS_SIZE = 256;

        // More glyphs than the pages up to the cap can hold at the bundled atlas size
        private const string GLYPHS = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        private TMP_FontAsset referenceFont = null!;
        private TMP_FontAsset bundledTextMeshPro = null!;
        private FontAsset bundledUIToolkit = null!;
        private Material generatedMaterial = null!;
        private SceneFontAssetsFactory factory = null!;
        private SceneFontAssets? assets;

        [SetUp]
        public void SetUp()
        {
            referenceFont = TestFonts.CreateTextMeshProFont();
            bundledTextMeshPro = TestFonts.CreateTextMeshProFont(BUNDLED_ATLAS_SIZE);
            bundledUIToolkit = TestFonts.CreateUIToolkitFont(BUNDLED_ATLAS_SIZE);
            generatedMaterial = bundledTextMeshPro.material;
            factory = new SceneFontAssetsFactory(referenceFont);
        }

        [TearDown]
        public void TearDown()
        {
            assets?.Destroy();
            Object.DestroyImmediate(generatedMaterial);
            Object.DestroyImmediate(bundledTextMeshPro);
            Object.DestroyImmediate(bundledUIToolkit);
            Object.DestroyImmediate(referenceFont);
        }

        [Test]
        public void TakeTheMaterialAndFallbackFromTheReferenceFont()
        {
            // Act
            assets = factory.AdoptBundled(ASSET_NAME, bundledTextMeshPro, bundledUIToolkit);

            // Assert
            TMP_FontAsset font = assets.TextMeshProFont;
            Assert.That(font, Is.SameAs(bundledTextMeshPro));
            Assert.That(assets.UIToolkitFont, Is.SameAs(bundledUIToolkit));
            Assert.That(font.material, Is.Not.SameAs(referenceFont.material));
            Assert.That(font.material.name, Is.EqualTo($"{ASSET_NAME} Material"));
            Assert.That(font.material.shader, Is.EqualTo(referenceFont.material.shader));
            Assert.That(font.material.mainTexture, Is.EqualTo(font.atlasTexture));
            Assert.That(font.fallbackFontAssetTable, Is.EqualTo(new[] { referenceFont }));
        }

        [Test]
        public void SizeTheMaterialToTheBundledAtlas()
        {
            // Act
            assets = factory.AdoptBundled(ASSET_NAME, bundledTextMeshPro, bundledUIToolkit);

            // Assert
            Material material = assets.TextMeshProFont.material;
            Assert.That(material.GetFloat(ShaderUtilities.ID_TextureWidth), Is.EqualTo(BUNDLED_ATLAS_SIZE));
            Assert.That(material.GetFloat(ShaderUtilities.ID_TextureHeight), Is.EqualTo(BUNDLED_ATLAS_SIZE));
        }

        [Test]
        public void LetBothFontsAddAtlasPages()
        {
            // Arrange
            bundledTextMeshPro.isMultiAtlasTexturesEnabled = false;
            bundledUIToolkit.isMultiAtlasTexturesEnabled = false;

            // Act
            assets = factory.AdoptBundled(ASSET_NAME, bundledTextMeshPro, bundledUIToolkit);

            // Assert
            Assert.That(bundledTextMeshPro.isMultiAtlasTexturesEnabled, Is.True);
            Assert.That(bundledUIToolkit.isMultiAtlasTexturesEnabled, Is.True);
        }

        [Test]
        public void StopAddingAtlasPagesOnceAFontReachesTheCap()
        {
            // Arrange
            assets = factory.AdoptBundled(ASSET_NAME, bundledTextMeshPro, bundledUIToolkit);
            int textMeshProAdded = GrowToTheCap(c => bundledTextMeshPro.TryAddCharacters(c), () => bundledTextMeshPro.atlasTextureCount);
            int uiToolkitAdded = GrowToTheCap(c => bundledUIToolkit.TryAddCharacters(c), () => bundledUIToolkit.atlasTextureCount);

            // Act
            assets.LimitAtlasPages();
            bundledTextMeshPro.TryAddCharacters(GLYPHS.Substring(textMeshProAdded));
            bundledUIToolkit.TryAddCharacters(GLYPHS.Substring(uiToolkitAdded));

            // Assert
            Assert.That(bundledTextMeshPro.isMultiAtlasTexturesEnabled, Is.False);
            Assert.That(bundledUIToolkit.isMultiAtlasTexturesEnabled, Is.False);
            Assert.That(bundledTextMeshPro.atlasTextureCount, Is.EqualTo(SceneFontAssets.MAX_ATLAS_PAGES));
            Assert.That(bundledUIToolkit.atlasTextureCount, Is.EqualTo(SceneFontAssets.MAX_ATLAS_PAGES));
        }

        [Test]
        public void DestroyOnlyTheMaterialItMade()
        {
            // Arrange
            assets = factory.AdoptBundled(ASSET_NAME, bundledTextMeshPro, bundledUIToolkit);
            Material material = assets.TextMeshProFont.material;
            bundledTextMeshPro.TryAddCharacters("A");
            bundledUIToolkit.TryAddCharacters("A");

            // Act
            assets.Destroy();
            assets = null;

            // Assert
            Assert.That(material == null, Is.True);
            Assert.That(bundledTextMeshPro != null, Is.True);
            Assert.That(bundledUIToolkit != null, Is.True);
            Assert.That(bundledTextMeshPro.HasCharacter('A'), Is.True);
            Assert.That(bundledUIToolkit.HasCharacter('A'), Is.True);
        }

        [Test]
        public void DestroyTheAtlasPagesAddedAtRuntime()
        {
            // Arrange
            assets = factory.AdoptBundled(ASSET_NAME, bundledTextMeshPro, bundledUIToolkit);
            GrowToTheCap(c => bundledTextMeshPro.TryAddCharacters(c), () => bundledTextMeshPro.atlasTextureCount);
            GrowToTheCap(c => bundledUIToolkit.TryAddCharacters(c), () => bundledUIToolkit.atlasTextureCount);
            Texture2D[] textMeshProPages = (Texture2D[])bundledTextMeshPro.atlasTextures.Clone();
            Texture2D[] uiToolkitPages = (Texture2D[])bundledUIToolkit.atlasTextures.Clone();

            // Act
            assets.Destroy();
            assets = null;

            // Assert
            AssertOnlyTheFirstPageIsLeft(textMeshProPages, bundledTextMeshPro.atlasTextures, bundledTextMeshPro.atlasTextureCount);
            AssertOnlyTheFirstPageIsLeft(uiToolkitPages, bundledUIToolkit.atlasTextures, bundledUIToolkit.atlasTextureCount);

            Assert.That(LoadFontSystem.FindDefect(bundledTextMeshPro.atlasTextures, bundledTextMeshPro.atlasWidth, bundledTextMeshPro.atlasHeight, bundledTextMeshPro.sourceFontFile), Is.Null);
            Assert.That(LoadFontSystem.FindDefect(bundledUIToolkit.atlasTextures, bundledUIToolkit.atlasWidth, bundledUIToolkit.atlasHeight, bundledUIToolkit.sourceFontFile), Is.Null);
        }

        /// <summary>
        ///     Adds the glyphs one at a time, so that each addition opens at most one page, until the font holds
        ///     <see cref="SceneFontAssets.MAX_ATLAS_PAGES" /> pages. Returns how many glyphs it added.
        /// </summary>
        private static int GrowToTheCap(Func<string, bool> tryAddCharacters, Func<int> atlasTextureCount)
        {
            var added = 0;

            while (atlasTextureCount() < SceneFontAssets.MAX_ATLAS_PAGES)
            {
                Assert.That(added, Is.LessThan(GLYPHS.Length), "The glyphs ran out before the font reached the page cap");
                tryAddCharacters(GLYPHS[added].ToString());
                added++;
            }

            return added;
        }

        private static void AssertOnlyTheFirstPageIsLeft(Texture2D[] pagesBefore, Texture2D[] pagesAfter, int atlasTextureCount)
        {
            Assert.That(atlasTextureCount, Is.EqualTo(1));
            Assert.That(pagesAfter, Has.Length.EqualTo(1));
            Assert.That(pagesAfter[0], Is.SameAs(pagesBefore[0]));
            Assert.That(pagesAfter[0] != null, Is.True);

            for (var i = 1; i < pagesBefore.Length; i++)
                Assert.That(pagesBefore[i] == null, Is.True, $"Page {i} survived");
        }
    }
}
