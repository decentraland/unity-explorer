using DCL.MapRenderer.MapLayers.Atlas.SatelliteAtlas;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.MapRenderer.Tests.SatelliteAtlas
{
    public class SatelliteDetailTilesShould
    {
        private const float BUNDLED_PIXELS_PER_UNIT = 512f / 800f;
        private const float BUNDLED_CHUNK_SIZE = 800f;
        private const float LEVEL_4_TILE_SIZE = BUNDLED_CHUNK_SIZE / 2f;
        private const float LEVEL_8_TILE_SIZE = BUNDLED_CHUNK_SIZE / 32f;
        private const int CURRENT_REFRESH = 10;

        private readonly Dictionary<Vector3Int, SatelliteDetailTiles.Tile> tiles = new ();
        private readonly List<KeyValuePair<int, Vector3Int>> evictions = new ();
        private readonly List<Vector3Int> unusedLoads = new ();
        private GameObject viewObject;
        private AtlasChunk view;
        private Texture2D loadedTexture;

        [SetUp]
        public void SetUp()
        {
            viewObject = new GameObject(nameof(SatelliteDetailTilesShould));
            view = viewObject.AddComponent<AtlasChunk>();
            loadedTexture = new Texture2D(1, 1);
        }

        [TearDown]
        public void TearDown()
        {
            tiles.Clear();
            Object.DestroyImmediate(viewObject);
            Object.DestroyImmediate(loadedTexture);
        }

        [TestCase(0.5f, 3)]
        [TestCase(BUNDLED_PIXELS_PER_UNIT, 3)]
        [TestCase(BUNDLED_PIXELS_PER_UNIT * 1.4f, 3)] // below sqrt(2): the bundled chunks are nearer
        [TestCase(BUNDLED_PIXELS_PER_UNIT * 1.5f, 4)] // above sqrt(2): level 4 is nearer
        [TestCase(BUNDLED_PIXELS_PER_UNIT * 2f, 4)]
        [TestCase(BUNDLED_PIXELS_PER_UNIT * 2.8f, 4)]
        [TestCase(BUNDLED_PIXELS_PER_UNIT * 2.9f, 5)]
        [TestCase(BUNDLED_PIXELS_PER_UNIT * 32f, 8)]
        [TestCase(100f, 8)]
        public void PickTheLevelNearestToTheScreenSharpness(float screenPixelsPerUnit, int expectedLevel)
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

            // Assert
            Assert.AreEqual(new RectInt(2, 3, 1, 1), range);
        }

        [Test]
        public void ClampTheRangeToTheGrid()
        {
            // Arrange
            var rect = new Rect(-10000, -10000, 20000, 20000);

            // Act
            RectInt range = SatelliteDetailTiles.TileRange(rect, 4, Vector2.zero, BUNDLED_CHUNK_SIZE);

            // Assert
            Assert.AreEqual(new RectInt(0, 0, 16, 16), range);
        }

        [Test]
        public void KeepTheLevelWhenTheRectFitsTheTileBudget()
        {
            // Arrange: 4x4 level-8 tiles
            var rect = new Rect(1, -(4 * LEVEL_8_TILE_SIZE) + 1, (4 * LEVEL_8_TILE_SIZE) - 2, (4 * LEVEL_8_TILE_SIZE) - 2);

            // Act
            int level = SatelliteDetailTiles.LevelWithinTileBudget(rect, 8, SatelliteDetailTiles.MIN_LEVEL, Vector2.zero, BUNDLED_CHUNK_SIZE, out RectInt range);

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
            int level = SatelliteDetailTiles.LevelWithinTileBudget(rect, 8, SatelliteDetailTiles.MIN_LEVEL, Vector2.zero, BUNDLED_CHUNK_SIZE, out RectInt range);

            // Assert
            Assert.AreEqual(7, level);
            Assert.AreEqual(new RectInt(0, 0, 6, 6), range);
        }

        [Test]
        public void NotStepBelowTheMinimumLevel()
        {
            // Arrange: the whole grid, over the budget even at the minimum level
            var rect = new Rect(-10000, -10000, 20000, 20000);

            // Act
            int level = SatelliteDetailTiles.LevelWithinTileBudget(rect, 6, SatelliteDetailTiles.MIN_LEVEL, Vector2.zero, BUNDLED_CHUNK_SIZE, out RectInt range);

            // Assert
            Assert.AreEqual(SatelliteDetailTiles.MIN_LEVEL, level);
            Assert.AreEqual(new RectInt(0, 0, 16, 16), range);
        }

        [Test]
        public void NotStepBelowAHigherMinimumLevel()
        {
            // Arrange: 12x12 level-8 tiles is over the budget, but level 8 is the minimum
            var rect = new Rect(1, -(12 * LEVEL_8_TILE_SIZE) + 1, (12 * LEVEL_8_TILE_SIZE) - 2, (12 * LEVEL_8_TILE_SIZE) - 2);

            // Act
            int level = SatelliteDetailTiles.LevelWithinTileBudget(rect, 8, 8, Vector2.zero, BUNDLED_CHUNK_SIZE, out RectInt range);

            // Assert
            Assert.AreEqual(8, level);
            Assert.AreEqual(new RectInt(0, 0, 12, 12), range);
        }

        [Test]
        public void KeepARangeWithinTheTileBudget()
        {
            // Arrange
            var range = new RectInt(2, 3, 16, 4);

            // Act
            RectInt capped = SatelliteDetailTiles.CapRange(range, new Vector2Int(5, 5), SatelliteDetailTiles.MAX_TILES_PER_CAMERA);

            // Assert
            Assert.AreEqual(range, capped);
        }

        [Test]
        public void CapARangeOverTheBudgetAroundTheCameraTile()
        {
            // Arrange: the whole level-4 grid, with the camera over tile (10, 6)
            var range = new RectInt(0, 0, 16, 16);

            // Act
            RectInt capped = SatelliteDetailTiles.CapRange(range, new Vector2Int(10, 6), SatelliteDetailTiles.MAX_TILES_PER_CAMERA);

            // Assert
            Assert.AreEqual(new RectInt(6, 2, 8, 8), capped);
        }

        [Test]
        public void CutTheLongerSideFirstWhenCapping()
        {
            // Arrange: a tall, narrow world 3 tiles wide
            var range = new RectInt(4, 0, 3, 40);

            // Act
            RectInt capped = SatelliteDetailTiles.CapRange(range, new Vector2Int(5, 0), SatelliteDetailTiles.MAX_TILES_PER_CAMERA);

            // Assert: all 3 columns kept, the rows start at the range's edge nearest to the camera
            Assert.AreEqual(new RectInt(4, 0, 3, 21), capped);
        }

        [Test]
        public void ClipTheCameraRectToTheWorldBounds()
        {
            // Arrange
            var rect = new Rect(-100, -100, 400, 400);
            var bounds = new Rect(0, 0, 100, 50);

            // Act
            bool overlaps = SatelliteDetailTiles.TryClip(rect, bounds, out Rect clipped);

            // Assert
            Assert.IsTrue(overlaps);
            Assert.AreEqual(bounds, clipped);
        }

        [Test]
        public void ReportNoOverlapWhenTheCameraIsOutsideTheWorld()
        {
            // Arrange
            var rect = new Rect(500, 500, 100, 100);
            var bounds = new Rect(0, 0, 100, 50);

            // Act
            bool overlaps = SatelliteDetailTiles.TryClip(rect, bounds, out _);

            // Assert
            Assert.IsFalse(overlaps);
        }

        [Test]
        public void RequestOnlyTheTilesOverASmallWorld()
        {
            // Arrange: a zoomed-out camera over the whole grid, and a world inside level-4 tile (2, 3)
            var cameraRect = new Rect(-10000, -10000, 20000, 20000);
            var world = new Rect((2 * LEVEL_4_TILE_SIZE) + 10, -(4 * LEVEL_4_TILE_SIZE) + 10, 100, 100);
            SatelliteDetailTiles.TryClip(cameraRect, world, out Rect clipped);

            // Act
            RectInt range = SatelliteDetailTiles.TileRange(clipped, SatelliteDetailTiles.MIN_LEVEL, Vector2.zero, BUNDLED_CHUNK_SIZE);

            // Assert
            Assert.AreEqual(new RectInt(2, 3, 1, 1), range);
        }

        [Test]
        public void FindTheTileUnderAPosition()
        {
            // Arrange: inside level-4 tile (1, 2), which spans x 400..800 and y -800..-1200
            var position = new Vector2(500f, -900f);

            // Act
            Vector2Int tile = SatelliteDetailTiles.TileIndex(position, 4, Vector2.zero, BUNDLED_CHUNK_SIZE);

            // Assert
            Assert.AreEqual(new Vector2Int(1, 2), tile);
        }

        [Test]
        public void PlaceTheTileCentreOnTheGrid()
        {
            // Arrange: level-4 tile (1, 2) spans x 400..800 and y -800..-1200 below a top-left corner at the origin
            var id = new Vector3Int(1, 2, 4);

            // Act
            Vector2 center = SatelliteDetailTiles.TileCenter(id, Vector2.zero, BUNDLED_CHUNK_SIZE);

            // Assert
            Assert.AreEqual(new Vector2(600f, -1000f), center);
        }

        [Test]
        public void DropOnlyTheUnfinishedLoadsOfTilesOutOfView()
        {
            // Arrange
            AddTile(0, lastUsed: 1); // loading, out of view
            AddTile(1, lastUsed: CURRENT_REFRESH); // loading, in view
            AddTile(2, lastUsed: 1, loaded: true); // loaded, out of view: kept for the cache

            // Act
            SatelliteDetailTiles.CollectUnusedLoads(tiles, CURRENT_REFRESH, unusedLoads);

            // Assert
            Assert.AreEqual(1, unusedLoads.Count);
            Assert.AreEqual(TileId(0), unusedLoads[0]);
        }

        [Test]
        public void EvictNothingWithinTheCap()
        {
            // Arrange
            AddTile(0, lastUsed: 1);
            AddTile(1, lastUsed: 2);

            // Act
            SatelliteDetailTiles.CollectEvictions(tiles, CURRENT_REFRESH, 2, evictions);

            // Assert
            Assert.IsEmpty(evictions);
        }

        [Test]
        public void EvictTheOldestTilesFirstDownToTheCap()
        {
            // Arrange
            AddTile(0, lastUsed: 5);
            AddTile(1, lastUsed: 1);
            AddTile(2, lastUsed: 3);
            AddTile(3, lastUsed: 2);

            // Act
            SatelliteDetailTiles.CollectEvictions(tiles, CURRENT_REFRESH, 2, evictions);

            // Assert
            Assert.AreEqual(2, evictions.Count);
            Assert.AreEqual(TileId(1), evictions[0].Value);
            Assert.AreEqual(TileId(3), evictions[1].Value);
        }

        [Test]
        public void NeverEvictTilesInView()
        {
            // Arrange
            AddTile(0, lastUsed: CURRENT_REFRESH);
            AddTile(1, lastUsed: CURRENT_REFRESH);
            AddTile(2, lastUsed: 1);
            AddTile(3, lastUsed: 2);

            // Act
            SatelliteDetailTiles.CollectEvictions(tiles, CURRENT_REFRESH, 1, evictions);

            // Assert: three over the cap, but only the two out of view can go
            Assert.AreEqual(2, evictions.Count);
            Assert.AreEqual(TileId(2), evictions[0].Value);
            Assert.AreEqual(TileId(3), evictions[1].Value);
        }

        private static Vector3Int TileId(int i) =>
            new (i, 0, SatelliteDetailTiles.MIN_LEVEL);

        private void AddTile(int i, int lastUsed, bool loaded = false)
        {
            tiles.Add(TileId(i), new SatelliteDetailTiles.Tile(view) { LastUsed = lastUsed, Texture = loaded ? loadedTexture : null });
        }
    }
}
