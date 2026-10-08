using NUnit.Framework;
using UnityEngine;

namespace Loading.Tests
{
    public class ModelMetricsShould
    {
        [Test]
        public void SumEveryCount()
        {
            // Arrange
            var first = new ModelMetrics(100, 2, 3, 4);
            var second = new ModelMetrics(10, 1, 2, 3);

            // Act
            var total = first + second;

            // Assert
            Assert.AreEqual(110, total.Triangles);
            Assert.AreEqual(3, total.Materials);
            Assert.AreEqual(5, total.Textures);
            Assert.AreEqual(7, total.Meshes);
        }

        [Test]
        public void StartEmpty()
        {
            // Act
            var total = ModelMetrics.Empty + new ModelMetrics(1, 2, 3, 4);

            // Assert
            Assert.AreEqual(1, total.Triangles);
            Assert.AreEqual(2, total.Materials);
            Assert.AreEqual(3, total.Textures);
            Assert.AreEqual(4, total.Meshes);
        }

        [Test]
        public void CountOnlyTheTexturesOfAFacialFeature()
        {
            // Arrange
            var main = new Texture2D(1, 1);
            var mask = new Texture2D(1, 1);

            try
            {
                // Act
                var withMask = ModelMetrics.FromTextures(main, mask);
                var withoutMask = ModelMetrics.FromTextures(main, null);

                // Assert
                Assert.AreEqual(2, withMask.Textures);
                Assert.AreEqual(1, withoutMask.Textures);
                Assert.AreEqual(0, withMask.Triangles);
                Assert.AreEqual(0, withMask.Materials);
                Assert.AreEqual(0, withMask.Meshes);
            }
            finally
            {
                Object.DestroyImmediate(main);
                Object.DestroyImmediate(mask);
            }
        }
    }
}
