using NUnit.Framework;
using UnityEngine;

namespace ECS.StreamableLoading.Fonts.Tests
{
    [TestFixture]
    public class FontBundleDefectShould
    {
        private const int ATLAS_SIZE = 1024;

        private Texture2D atlas = null!;
        private Font sourceFont = null!;

        [SetUp]
        public void SetUp()
        {
            atlas = new Texture2D(4, 4);
            sourceFont = new Font();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(atlas);
            Object.DestroyImmediate(sourceFont);
        }

        [Test]
        public void BeAbsentForAnAtlasWithASourceFont()
        {
            // Act
            string? defect = LoadFontSystem.FindDefect(new[] { atlas }, ATLAS_SIZE, ATLAS_SIZE, sourceFont);

            // Assert
            Assert.That(defect, Is.Null);
        }

        [Test]
        public void ReportAMissingAtlas()
        {
            Assert.That(LoadFontSystem.FindDefect(null, ATLAS_SIZE, ATLAS_SIZE, sourceFont), Is.EqualTo("no atlas texture"));
            Assert.That(LoadFontSystem.FindDefect(System.Array.Empty<Texture2D>(), ATLAS_SIZE, ATLAS_SIZE, sourceFont), Is.EqualTo("no atlas texture"));
            Assert.That(LoadFontSystem.FindDefect(new Texture2D[1], ATLAS_SIZE, ATLAS_SIZE, sourceFont), Is.EqualTo("no atlas texture"));
        }

        [TestCase(0, ATLAS_SIZE)]
        [TestCase(ATLAS_SIZE, 0)]
        [TestCase(8192, ATLAS_SIZE)]
        [TestCase(ATLAS_SIZE, 8192)]
        public void ReportAnUnsupportedAtlasSize(int width, int height)
        {
            // Act
            string? defect = LoadFontSystem.FindDefect(new[] { atlas }, width, height, sourceFont);

            // Assert
            Assert.That(defect, Does.StartWith("atlas size"));
        }

        [Test]
        public void ReportAMissingSourceFont()
        {
            // Act
            string? defect = LoadFontSystem.FindDefect(new[] { atlas }, ATLAS_SIZE, ATLAS_SIZE, null);

            // Assert
            Assert.That(defect, Is.EqualTo("no source font"));
        }
    }
}
