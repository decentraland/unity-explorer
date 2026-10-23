using DCL.MapRenderer.MapLayers.Atlas.SatelliteAtlas;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.MapRenderer.Tests.SatelliteAtlas
{
    public class SatelliteDetailTilesShould
    {
        private const int CURRENT_REFRESH = 10;

        private readonly Dictionary<Vector3Int, SatelliteDetailTiles.Tile> tiles = new ();
        private readonly List<KeyValuePair<int, Vector3Int>> evictions = new ();
        private readonly List<Vector3Int> unusedLoads = new ();
        private GameObject viewObject = null!;
        private AtlasChunk view = null!;
        private Texture2D loadedTexture = null!;

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

        [Test]
        public void NeverDropTheBaseLevelLoads()
        {
            // Arrange
            AddTile(0, lastUsed: 1, level: SatelliteTileGrid.BASE_LEVEL); // loading, out of view
            AddTile(1, lastUsed: 1); // loading, out of view

            // Act
            SatelliteDetailTiles.CollectUnusedLoads(tiles, CURRENT_REFRESH, unusedLoads);

            // Assert
            Assert.AreEqual(1, unusedLoads.Count);
            Assert.AreEqual(TileId(1), unusedLoads[0]);
        }

        [Test]
        public void NeitherEvictNorCountTheBaseLevelTiles()
        {
            // Arrange
            AddTile(0, lastUsed: 1, loaded: true, level: SatelliteTileGrid.BASE_LEVEL);
            AddTile(1, lastUsed: 2, loaded: true, level: SatelliteTileGrid.BASE_LEVEL);
            AddTile(2, lastUsed: 3, loaded: true);

            // Act
            SatelliteDetailTiles.CollectEvictions(tiles, CURRENT_REFRESH, 1, evictions);

            // Assert: one finer tile within a cap of one
            Assert.IsEmpty(evictions);
        }

        private static Vector3Int TileId(int i, int level = SatelliteTileGrid.BASE_LEVEL + 1) =>
            new (i, 0, level);

        private void AddTile(int i, int lastUsed, bool loaded = false, int level = SatelliteTileGrid.BASE_LEVEL + 1)
        {
            tiles.Add(TileId(i, level), new SatelliteDetailTiles.Tile(view) { LastUsed = lastUsed, Texture = loaded ? loadedTexture : null });
        }
    }
}
