using DCL.MapRenderer.MapLayers.Atlas.SatelliteAtlas;
using NUnit.Framework;
using UnityEngine;

namespace DCL.MapRenderer.Tests.SatelliteAtlas
{
    public class SatelliteDetailTilesShould
    {
        private const float BUNDLED_PIXELS_PER_UNIT = 512f / 800f;
        private const float BUNDLED_CHUNK_SIZE = 800f;
        private const float LEVEL_4_TILE_SIZE = BUNDLED_CHUNK_SIZE / 2f;

        [TestCase(0.5f, 3)]
        [TestCase(BUNDLED_PIXELS_PER_UNIT, 3)]
        [TestCase(0.65f, 4)]
        [TestCase(BUNDLED_PIXELS_PER_UNIT * 2f, 4)]
        [TestCase(1.3f, 5)]
        [TestCase(BUNDLED_PIXELS_PER_UNIT * 32f, 8)]
        [TestCase(100f, 8)]
        public void PickCoarsestLevelAtLeastAsSharpAsTheScreen(float screenPixelsPerUnit, int expectedLevel)
        {
            // Act
            int level = SatelliteDetailTiles.LevelFor(screenPixelsPerUnit, BUNDLED_PIXELS_PER_UNIT);

            // Assert
            Assert.AreEqual(expectedLevel, level);
        }

        [Test]
        public void CoverOnlyTheTilesTheRectOverlaps()
        {
            // Arrange: inside level-4 tile (2, 3), which spans x 800..1200 and y -1200..-1600 below a top-left corner at the origin
            var rect = new Rect((2 * LEVEL_4_TILE_SIZE) + 10, -(4 * LEVEL_4_TILE_SIZE) + 10, LEVEL_4_TILE_SIZE - 20, LEVEL_4_TILE_SIZE - 20);

            // Act
            RectInt range = SatelliteDetailTiles.TileRange(rect, 4, Vector2.zero, BUNDLED_CHUNK_SIZE);

            // Assert: bounds are inclusive, so a single tile has zero width and height
            Assert.AreEqual(new RectInt(2, 3, 0, 0), range);
        }

        [Test]
        public void ClampTheRangeToTheGrid()
        {
            // Arrange
            var rect = new Rect(-10000, -10000, 20000, 20000);

            // Act
            RectInt range = SatelliteDetailTiles.TileRange(rect, 4, Vector2.zero, BUNDLED_CHUNK_SIZE);

            // Assert
            Assert.AreEqual(new RectInt(0, 0, 15, 15), range);
        }
    }
}
