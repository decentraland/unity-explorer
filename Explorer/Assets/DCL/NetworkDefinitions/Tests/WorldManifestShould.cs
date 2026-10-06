using NUnit.Framework;
using Unity.Mathematics;

namespace ECS.Tests
{
    public class WorldManifestShould
    {
        [Test]
        public void BoundTheOccupiedParcels()
        {
            // Arrange
            WorldManifest manifest = WorldManifest.Create(new WorldManifestDto { occupied = new[] { "2,-1", "-3,4", "0,0" } });

            // Act
            bool hasBounds = manifest.TryGetOccupiedBounds(out int2 min, out int2 max);

            // Assert
            Assert.IsTrue(hasBounds);
            Assert.AreEqual(new int2(-3, -1), min);
            Assert.AreEqual(new int2(2, 4), max);
            manifest.Dispose();
        }

        [Test]
        public void HaveNoBoundsWithoutOccupiedParcels()
        {
            // Arrange
            WorldManifest manifest = WorldManifest.Create(new WorldManifestDto { roads = new[] { "0,0" } });

            // Act
            bool hasBounds = manifest.TryGetOccupiedBounds(out _, out _);

            // Assert
            Assert.IsFalse(hasBounds);
            manifest.Dispose();
        }

        [Test]
        public void HaveNoBoundsWhenEmpty()
        {
            Assert.IsFalse(WorldManifest.Empty.TryGetOccupiedBounds(out _, out _));
        }
    }
}
