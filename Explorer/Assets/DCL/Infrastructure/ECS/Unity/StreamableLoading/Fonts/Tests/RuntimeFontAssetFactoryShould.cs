using ECS.TestSuite;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace ECS.StreamableLoading.Fonts.Tests
{
    [TestFixture]
    public class RuntimeFontAssetFactoryShould
    {
        private const string ASSET_NAME = "Liberation";
        private const int BUNDLED_ATLAS_SIZE = 512;

        private TMP_FontAsset referenceFont = null!;
        private TMP_FontAsset bundledTextMeshPro = null!;
        private FontAsset bundledUIToolkit = null!;
        private Material generatedMaterial = null!;
        private RuntimeFontAssetFactory factory = null!;
        private FontFamilyAssets? assets;

        [SetUp]
        public void SetUp()
        {
            referenceFont = TestFonts.CreateTextMeshProFont();
            bundledTextMeshPro = TestFonts.CreateTextMeshProFont(BUNDLED_ATLAS_SIZE);
            bundledUIToolkit = TestFonts.CreateUIToolkitFont();
            generatedMaterial = bundledTextMeshPro.material;
            factory = new RuntimeFontAssetFactory(referenceFont);
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
        public void DestroyOnlyTheMaterialItMade()
        {
            // Arrange
            assets = factory.AdoptBundled(ASSET_NAME, bundledTextMeshPro, bundledUIToolkit);
            Material material = assets.TextMeshProFont.material;

            // Act
            assets.Destroy();
            assets = null;

            // Assert
            Assert.That(material == null, Is.True);
            Assert.That(bundledTextMeshPro != null, Is.True);
            Assert.That(bundledUIToolkit != null, Is.True);
        }
    }
}
