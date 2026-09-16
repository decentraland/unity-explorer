using ECS.TestSuite;
using NUnit.Framework;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace ECS.StreamableLoading.Fonts.Tests
{
    [TestFixture]
    public class RuntimeFontAssetFactoryShould
    {
        private const string ASSET_NAME = "Liberation";
        private const string FAMILY_NAME = "Liberation Sans";
        private const int BOLD_WEIGHT_INDEX = 7;

        private static readonly string NOT_A_FONT_PATH = Path.Combine(Application.dataPath, "../TestResources/CRDT/arraybuffer.test");

        private TMP_FontAsset referenceFont = null!;
        private RuntimeFontAssetFactory factory = null!;
        private FontFamilyAssets? assets;

        [SetUp]
        public void SetUp()
        {
            referenceFont = TestFonts.CreateTextMeshProFont();
            factory = new RuntimeFontAssetFactory(referenceFont);
        }

        [TearDown]
        public void TearDown()
        {
            assets?.Destroy();
            Object.DestroyImmediate(referenceFont);
        }

        [Test]
        public void BuildBothAssetsFromTheRegularFace()
        {
            assets = factory.Create(ASSET_NAME, TestFonts.PATH);

            Assert.That(assets, Is.Not.Null);
            Assert.That(assets!.TextMeshProFont.name, Is.EqualTo(ASSET_NAME));
            Assert.That(assets.TextMeshProFont.faceInfo.familyName, Is.EqualTo(FAMILY_NAME));
            Assert.That(assets.UIToolkitFont.faceInfo.familyName, Is.EqualTo(FAMILY_NAME));
            Assert.That(assets.TextMeshProFont.fontWeightTable[BOLD_WEIGHT_INDEX].regularTypeface, Is.Null);
        }

        [Test]
        public void BuildNothingWhenTheRegularFaceIsNotAFont()
        {
            assets = factory.Create(ASSET_NAME, NOT_A_FONT_PATH);

            Assert.That(assets, Is.Null);
        }

        [Test]
        public void TakeTheMaterialAndFallbackFromTheReferenceFont()
        {
            assets = factory.Create(ASSET_NAME, TestFonts.PATH);

            TMP_FontAsset font = assets!.TextMeshProFont;
            Assert.That(font.material, Is.Not.SameAs(referenceFont.material));
            Assert.That(font.material.name, Is.EqualTo($"{ASSET_NAME} Material"));
            Assert.That(font.material.shader, Is.EqualTo(referenceFont.material.shader));
            Assert.That(font.material.mainTexture, Is.EqualTo(font.atlasTexture));
            Assert.That(font.fallbackFontAssetTable, Is.EqualTo(new[] { referenceFont }));
        }

        [Test]
        public void DestroyEveryAssetItOwns()
        {
            assets = factory.Create(ASSET_NAME, TestFonts.PATH);
            TMP_FontAsset regular = assets!.TextMeshProFont;
            Material material = regular.material;
            FontAsset uiToolkitRegular = assets.UIToolkitFont;

            assets.Destroy();
            assets = null;

            Assert.That(regular == null, Is.True);
            Assert.That(material == null, Is.True);
            Assert.That(uiToolkitRegular == null, Is.True);
        }
    }
}
