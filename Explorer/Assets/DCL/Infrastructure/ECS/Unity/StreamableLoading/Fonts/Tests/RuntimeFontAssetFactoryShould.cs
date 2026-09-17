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
        private const int REGULAR_WEIGHT_INDEX = 4;
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
            assets = factory.Create(ASSET_NAME, TestFonts.PATH, null, null, null);

            Assert.That(assets, Is.Not.Null);
            Assert.That(assets!.TextMeshProFont.name, Is.EqualTo(ASSET_NAME));
            Assert.That(assets.TextMeshProFont.faceInfo.familyName, Is.EqualTo(FAMILY_NAME));
            Assert.That(assets.UIToolkitFont.faceInfo.familyName, Is.EqualTo(FAMILY_NAME));
            Assert.That(assets.TextMeshProFont.fontWeightTable[BOLD_WEIGHT_INDEX].regularTypeface, Is.Null);
        }

        [Test]
        public void BuildBothAssetsFromAnOpenTypeFace()
        {
            string path = Path.Combine(Application.dataPath, "TextMesh Pro/Fonts & Materials/Asian fallbacks/NotoSansJP-SemiBold.otf");
            Assert.That(File.ReadAllBytes(path)[0..4], Is.EqualTo(new byte[] { 0x4F, 0x54, 0x54, 0x4F }));

            assets = factory.Create("OpenType", path);

            Assert.That(assets, Is.Not.Null);
            Assert.That(assets!.TextMeshProFont.faceInfo.familyName, Is.Not.Empty);
            Assert.That(assets.UIToolkitFont.faceInfo.familyName, Is.EqualTo(assets.TextMeshProFont.faceInfo.familyName));
        }

        [Test]
        public void WireTheVariantsIntoTheWeightTables()
        {
            assets = factory.Create(ASSET_NAME, TestFonts.PATH, TestFonts.PATH, TestFonts.PATH, TestFonts.PATH);

            TMP_FontWeightPair[] textMeshProTable = assets!.TextMeshProFont.fontWeightTable;
            Assert.That(textMeshProTable[BOLD_WEIGHT_INDEX].regularTypeface.name, Is.EqualTo($"{ASSET_NAME} {FontVariant.Bold}"));
            Assert.That(textMeshProTable[REGULAR_WEIGHT_INDEX].italicTypeface.name, Is.EqualTo($"{ASSET_NAME} {FontVariant.Italic}"));
            Assert.That(textMeshProTable[BOLD_WEIGHT_INDEX].italicTypeface.name, Is.EqualTo($"{ASSET_NAME} {FontVariant.BoldItalic}"));

            FontWeightPair[] uiToolkitTable = assets.UIToolkitFont.fontWeightTable;
            Assert.That(uiToolkitTable[BOLD_WEIGHT_INDEX].regularTypeface.name, Is.EqualTo($"{ASSET_NAME} {FontVariant.Bold}"));
            Assert.That(uiToolkitTable[REGULAR_WEIGHT_INDEX].italicTypeface.name, Is.EqualTo($"{ASSET_NAME} {FontVariant.Italic}"));
            Assert.That(uiToolkitTable[BOLD_WEIGHT_INDEX].italicTypeface.name, Is.EqualTo($"{ASSET_NAME} {FontVariant.BoldItalic}"));
        }

        [Test]
        public void BuildNothingWhenTheRegularFaceIsNotAFont()
        {
            assets = factory.Create(ASSET_NAME, NOT_A_FONT_PATH, null, null, null);

            Assert.That(assets, Is.Null);
        }

        [Test]
        public void KeepTheRegularFaceWhenAVariantIsNotAFont()
        {
            assets = factory.Create(ASSET_NAME, TestFonts.PATH, NOT_A_FONT_PATH, null, null);

            Assert.That(assets, Is.Not.Null);
            Assert.That(assets!.TextMeshProFont.fontWeightTable[BOLD_WEIGHT_INDEX].regularTypeface, Is.Null);
        }

        [Test]
        public void TakeTheMaterialAndFallbackFromTheReferenceFont()
        {
            assets = factory.Create(ASSET_NAME, TestFonts.PATH, null, null, null);

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
            assets = factory.Create(ASSET_NAME, TestFonts.PATH, TestFonts.PATH, null, null);
            TMP_FontAsset regular = assets!.TextMeshProFont;
            TMP_FontAsset bold = regular.fontWeightTable[BOLD_WEIGHT_INDEX].regularTypeface;
            Material material = regular.material;
            FontAsset uiToolkitRegular = assets.UIToolkitFont;

            assets.Destroy();
            assets = null;

            Assert.That(regular == null, Is.True);
            Assert.That(bold == null, Is.True);
            Assert.That(material == null, Is.True);
            Assert.That(uiToolkitRegular == null, Is.True);
        }
    }
}
