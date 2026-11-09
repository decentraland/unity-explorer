using DCL.MapRenderer.MapLayers.Atlas.SatelliteAtlas;
using NUnit.Framework;
using UnityEngine;

namespace DCL.MapRenderer.Tests.SatelliteAtlas
{
    public class SatelliteTileGridShould
    {
        private const float BASE_LEVEL_PIXELS_PER_UNIT = 1024f / 800f;
        private const float BUNDLED_CHUNK_SIZE = 800f;
        private const float LEVEL_4_TILE_SIZE = BUNDLED_CHUNK_SIZE / 2f;
        private const float LEVEL_8_TILE_SIZE = BUNDLED_CHUNK_SIZE / 32f;

        private static readonly SatelliteTileGrid GRID = new (Vector2.zero, BUNDLED_CHUNK_SIZE);

        [TestCase(BASE_LEVEL_PIXELS_PER_UNIT / 8f, 3)] // never coarser than the base level
        [TestCase(BASE_LEVEL_PIXELS_PER_UNIT, 3)]
        [TestCase(BASE_LEVEL_PIXELS_PER_UNIT * 1.05f, 3)]
        [TestCase(BASE_LEVEL_PIXELS_PER_UNIT * 1.1f, 4)] // switches before a tile is magnified much
        [TestCase(BASE_LEVEL_PIXELS_PER_UNIT * 16f, 7)]
        [TestCase(BASE_LEVEL_PIXELS_PER_UNIT * 32f, 8)]
        [TestCase(100f, 8)]
        public void PickTheLevelNearestToTheScreenSharpness(float screenPixelsPerUnit, int expectedLevel)
        {
            // Act
            int level = SatelliteTileGrid.LevelFor(screenPixelsPerUnit, BASE_LEVEL_PIXELS_PER_UNIT);

            // Assert
            Assert.AreEqual(expectedLevel, level);
        }

        [Test]
        public void MatchTheBaseLevelPixelDensityToTheBundledChunk()
        {
            // Act
            float pixelsPerUnit = GRID.BaseLevelPixelsPerUnit;

            // Assert
            Assert.AreEqual(BASE_LEVEL_PIXELS_PER_UNIT, pixelsPerUnit, 1e-5f);
        }

        [Test]
        public void CoverOnlyTheTilesTheRectOverlaps()
        {
            // Arrange: inside level-4 tile (2, 3), which spans x 800..1200 and y -1200..-1600 below a top-left corner at the origin
            var rect = new Rect((2 * LEVEL_4_TILE_SIZE) + 10, -(4 * LEVEL_4_TILE_SIZE) + 10, LEVEL_4_TILE_SIZE - 20, LEVEL_4_TILE_SIZE - 20);

            // Act
            RectInt range = GRID.TileRange(rect, 4);

            // Assert
            Assert.AreEqual(new RectInt(2, 3, 1, 1), range);
        }

        [Test]
        public void ClampTheRangeToTheGrid()
        {
            // Arrange
            var rect = new Rect(-10000, -10000, 20000, 20000);

            // Act
            RectInt range = GRID.TileRange(rect, 4);

            // Assert
            Assert.AreEqual(new RectInt(0, 0, 16, 16), range);
        }

        [Test]
        public void KeepTheLevelWhenTheRectFitsTheTileBudget()
        {
            // Arrange: 4x4 level-8 tiles
            var rect = new Rect(1, -(4 * LEVEL_8_TILE_SIZE) + 1, (4 * LEVEL_8_TILE_SIZE) - 2, (4 * LEVEL_8_TILE_SIZE) - 2);

            // Act
            int level = GRID.LevelWithinTileBudget(rect, 8, out RectInt range);

            // Assert
            Assert.AreEqual(8, level);
            Assert.AreEqual(new RectInt(0, 0, 4, 4), range);
        }

        [Test]
        public void StepDownALevelWhenTheRectNeedsMoreTilesThanTheBudget()
        {
            // Arrange: 12x12 level-8 tiles is over the budget; level 7 needs 6x6
            var rect = new Rect(1, -(12 * LEVEL_8_TILE_SIZE) + 1, (12 * LEVEL_8_TILE_SIZE) - 2, (12 * LEVEL_8_TILE_SIZE) - 2);

            // Act
            int level = GRID.LevelWithinTileBudget(rect, 8, out RectInt range);

            // Assert
            Assert.AreEqual(7, level);
            Assert.AreEqual(new RectInt(0, 0, 6, 6), range);
        }

        [Test]
        public void StepDownToTheBaseLevelForTheWholeGrid()
        {
            // Arrange: the whole grid, within the budget only at the base level
            var rect = new Rect(-10000, -10000, 20000, 20000);

            // Act
            int level = GRID.LevelWithinTileBudget(rect, 6, out RectInt range);

            // Assert
            Assert.AreEqual(SatelliteTileGrid.BASE_LEVEL, level);
            Assert.AreEqual(new RectInt(0, 0, 8, 8), range);
        }

        [Test]
        public void PlaceTheTileCentreOnTheGrid()
        {
            // Arrange: level-4 tile (1, 2) spans x 400..800 and y -800..-1200 below a top-left corner at the origin
            var id = new Vector3Int(1, 2, 4);

            // Act
            Vector2 center = GRID.TileCenter(id);

            // Assert
            Assert.AreEqual(new Vector2(600f, -1000f), center);
        }
    }
}
