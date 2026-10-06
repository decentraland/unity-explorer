using DCL.MapRenderer.CoordsUtils;
using NUnit.Framework;
using UnityEngine;

namespace DCL.MapRenderer.Tests.CoordsUtils
{
    public class ChunkCoordsUtilsShould
    {
        private const int PARCEL_SIZE = 20;

        // Parcels 0..3 x 0..1
        private static readonly RectInt WORLD_PARCELS = new (0, 0, 4, 2);

        private ChunkCoordsUtils coordsUtils;

        [SetUp]
        public void SetUp()
        {
            coordsUtils = new ChunkCoordsUtils(PARCEL_SIZE);
        }

        [Test]
        public void BoundTheMapToAWorldsParcelsWithPadding()
        {
            // Act
            coordsUtils.SetWorldBounds(WORLD_PARCELS);

            // Assert: the parcels span x -20..60 and y -20..20, plus 10 parcels on each side
            Assert.AreEqual(Rect.MinMaxRect(-220, -220, 260, 220), coordsUtils.VisibleWorldBounds);
            Assert.AreEqual(new Vector2(20, 0), coordsUtils.VisibleWorldCenter);
        }

        [Test]
        public void RestoreGenesisCityBounds()
        {
            // Arrange
            Rect genesisBounds = coordsUtils.VisibleWorldBounds;
            coordsUtils.SetWorldBounds(WORLD_PARCELS);

            // Act
            coordsUtils.SetWorldBounds(null);

            // Assert
            Assert.AreEqual(genesisBounds, coordsUtils.VisibleWorldBounds);
            Assert.AreEqual(Vector2.zero, coordsUtils.VisibleWorldCenter);
        }

        [Test]
        public void NotifyBoundsChanges()
        {
            // Arrange
            var notified = 0;
            coordsUtils.VisibleWorldBoundsChanged += () => notified++;

            // Act
            coordsUtils.SetWorldBounds(WORLD_PARCELS);
            coordsUtils.SetWorldBounds(null);

            // Assert
            Assert.AreEqual(2, notified);
        }

        [Test]
        public void InteractOnlyWithAWorldsParcels()
        {
            // Arrange
            Vector3 worldParcel = coordsUtils.CoordsToPositionWithOffset(new Vector2(3, 1));
            Vector3 genesisParcel = coordsUtils.CoordsToPositionWithOffset(new Vector2(100, 100));

            // Act
            coordsUtils.SetWorldBounds(WORLD_PARCELS);

            // Assert
            Assert.IsTrue(coordsUtils.TryGetCoordsWithinInteractableBounds(worldParcel, out Vector2Int coords));
            Assert.AreEqual(new Vector2Int(3, 1), coords);
            Assert.IsFalse(coordsUtils.TryGetCoordsWithinInteractableBounds(genesisParcel, out _));
        }

        [Test]
        public void InteractWithGenesisCityAgainAfterAWorld()
        {
            // Arrange
            Vector3 genesisParcel = coordsUtils.CoordsToPositionWithOffset(new Vector2(100, 100));
            coordsUtils.SetWorldBounds(WORLD_PARCELS);

            // Act
            coordsUtils.SetWorldBounds(null);

            // Assert
            Assert.IsTrue(coordsUtils.TryGetCoordsWithinInteractableBounds(genesisParcel, out Vector2Int coords));
            Assert.AreEqual(new Vector2Int(100, 100), coords);
        }
    }
}
