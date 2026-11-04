using ECS.TestSuite;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;

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
        private FontAsset bundledUiToolkit = null!;
        private Material generatedMaterial = null!;
        private SceneFontAssetsFactory factory = null!;
        private SceneFontAssets? assets;

        [SetUp]
        public void SetUp()
        {
            referenceFont = TestFonts.CreateTextMeshProFont();
            bundledTextMeshPro = TestFonts.CreateTextMeshProFont(BUNDLED_ATLAS_SIZE);
            bundledUiToolkit = TestFonts.CreateUiToolkitFont(BUNDLED_ATLAS_SIZE);
            generatedMaterial = bundledTextMeshPro.material;
            factory = new SceneFontAssetsFactory(referenceFont);
        }

        [TearDown]
        public void TearDown()
        {
            assets?.Destroy();
            Object.DestroyImmediate(generatedMaterial);
            Object.DestroyImmediate(bundledTextMeshPro);
            Object.DestroyImmediate(bundledUiToolkit);
            Object.DestroyImmediate(referenceFont);
        }

        [Test]
        public void TakeTheMaterialAndFallbackFromTheReferenceFont()
        {
            // Act
            assets = factory.AdoptBundled(ASSET_NAME, bundledTextMeshPro, bundledUiToolkit);

            // Assert
            TMP_FontAsset font = assets.TextMeshProFont;
            Assert.That(font, Is.SameAs(bundledTextMeshPro));
            Assert.That(assets.UIToolkitFont, Is.SameAs(bundledUiToolkit));
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
            assets = factory.AdoptBundled(ASSET_NAME, bundledTextMeshPro, bundledUiToolkit);

            // Assert
            Material material = assets.TextMeshProFont.material;
            Assert.That(material.GetFloat(ShaderUtilities.ID_TextureWidth), Is.EqualTo(BUNDLED_ATLAS_SIZE));
            Assert.That(material.GetFloat(ShaderUtilities.ID_TextureHeight), Is.EqualTo(BUNDLED_ATLAS_SIZE));
        }

        [Test]
        public void KeepMultiAtlasOffAtRest()
        {
            // Arrange
            bundledTextMeshPro.isMultiAtlasTexturesEnabled = true;
            bundledUiToolkit.isMultiAtlasTexturesEnabled = true;

            // Act
            assets = factory.AdoptBundled(ASSET_NAME, bundledTextMeshPro, bundledUiToolkit);
            assets.AddTextMeshProGlyphs(GLYPHS);
            assets.AddUiToolkitGlyphs(GLYPHS);

            // Assert
            Assert.That(bundledTextMeshPro.isMultiAtlasTexturesEnabled, Is.False);
            Assert.That(bundledUiToolkit.isMultiAtlasTexturesEnabled, Is.False);
        }

        [Test]
        public void AddAtlasPagesForTheGlyphsOfATextUpToTheCap()
        {
            // Arrange
            assets = factory.AdoptBundled(ASSET_NAME, bundledTextMeshPro, bundledUiToolkit);

            // Act
            assets.AddTextMeshProGlyphs(GLYPHS);
            assets.AddUiToolkitGlyphs(GLYPHS);

            // Assert
            Assert.That(bundledTextMeshPro.atlasTextureCount, Is.EqualTo(SceneFontAssets.MAX_ATLAS_PAGES));
            Assert.That(bundledUiToolkit.atlasTextureCount, Is.EqualTo(SceneFontAssets.MAX_ATLAS_PAGES));
            Assert.That(bundledTextMeshPro.HasCharacter(GLYPHS[0]), Is.True);
            Assert.That(bundledUiToolkit.HasCharacter(GLYPHS[0]), Is.True);
        }

        [Test]
        public void NotAddAtlasPagesWhenTheTextIsLaidOut()
        {
            // Arrange
            assets = factory.AdoptBundled(ASSET_NAME, bundledTextMeshPro, bundledUiToolkit);

            // Act: layout adds all the missing glyphs of a text in one batch
            bundledTextMeshPro.TryAddCharacters(GLYPHS);
            bundledUiToolkit.TryAddCharacters(GLYPHS);

            // Assert
            Assert.That(bundledTextMeshPro.atlasTextureCount, Is.EqualTo(1));
            Assert.That(bundledUiToolkit.atlasTextureCount, Is.EqualTo(1));
        }

        [Test]
        public void DestroyOnlyTheMaterialItMade()
        {
            // Arrange
            assets = factory.AdoptBundled(ASSET_NAME, bundledTextMeshPro, bundledUiToolkit);
            Material material = assets.TextMeshProFont.material;
            assets.AddTextMeshProGlyphs("A");
            assets.AddUiToolkitGlyphs("A");

            // Act
            assets.Destroy();
            assets = null;

            // Assert
            Assert.That(bundledTextMeshPro.HasCharacter('A'), Is.True);
            Assert.That(bundledUiToolkit.HasCharacter('A'), Is.True);
            Assert.That(material == null, Is.True);
            Assert.That(bundledTextMeshPro != null, Is.True);
            Assert.That(bundledUiToolkit != null, Is.True);
        }

        [Test]
        public void DestroyTheAtlasPagesAddedAtRuntime()
        {
            // Arrange
            assets = factory.AdoptBundled(ASSET_NAME, bundledTextMeshPro, bundledUiToolkit);
            assets.AddTextMeshProGlyphs(GLYPHS);
            assets.AddUiToolkitGlyphs(GLYPHS);
            Texture2D[] textMeshProPages = (Texture2D[])bundledTextMeshPro.atlasTextures.Clone();
            Texture2D[] uiToolkitPages = (Texture2D[])bundledUiToolkit.atlasTextures.Clone();

            // Act
            assets.Destroy();
            assets = null;

            // Assert
            AssertOnlyTheFirstPageIsLeft(textMeshProPages, bundledTextMeshPro.atlasTextures, bundledTextMeshPro.atlasTextureCount);
            AssertOnlyTheFirstPageIsLeft(uiToolkitPages, bundledUiToolkit.atlasTextures, bundledUiToolkit.atlasTextureCount);
            Assert.That(bundledTextMeshPro.atlasWidth, Is.EqualTo(BUNDLED_ATLAS_SIZE));
            Assert.That(bundledUiToolkit.atlasWidth, Is.EqualTo(BUNDLED_ATLAS_SIZE));
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
