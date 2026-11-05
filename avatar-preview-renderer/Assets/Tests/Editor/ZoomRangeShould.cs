using NUnit.Framework;

namespace Preview.Tests
{
    public class ZoomRangeShould
    {
        private const float TOLERANCE = 0.0001f;

        [TestCase(0f, 1f / 1.75f)]
        [TestCase(50f, 1.9f / 1.75f)]
        [TestCase(100f, 1.6f)]
        [TestCase(-10f, 1f / 1.75f)]
        [TestCase(250f, 1.6f)]
        public void MapBabylonZoomPercentToAFactor(float percent, float expected)
        {
            // Act
            var factor = ZoomRange.FactorFromPercent(percent);

            // Assert
            Assert.AreEqual(expected, factor, TOLERANCE);
        }

        [Test]
        public void ShowTheFittedViewForBabylonsDefaultZoom()
        {
            // Act: zoom 41.67 is Babylon's default divisor of 1.75.
            var factor = ZoomRange.FactorFromPercent(0.75f / 1.8f * 100f);

            // Assert
            Assert.AreEqual(1f, factor, TOLERANCE);
        }

        [Test]
        public void StartAtTheFittedViewWithTheEditorsScaledRange()
        {
            // Arrange: the creators editor's options, Babylon's default zoom scaled by 3 with a wheel of 5.
            const float zoomScale = 3f;

            // Act
            var range = ZoomRange.FromOptions(0.75f / 1.8f * 100f, 5f, 50f, 2f, 1.5f);

            // Assert
            Assert.AreEqual(1f, range.Start * zoomScale, TOLERANCE);
        }

        [Test]
        public void UseTheRendererRangeWhenNoOptionIsGiven()
        {
            // Act
            var range = ZoomRange.FromOptions(null, null, 50f, 2f, 1.5f);

            // Assert
            Assert.AreEqual(1f / 1.5f, range.Min, TOLERANCE);
            Assert.AreEqual(2f, range.Max, TOLERANCE);
            Assert.AreEqual(1f, range.Start, TOLERANCE);
        }

        [Test]
        public void FreezeTheWheelWhenOnlyZoomIsGiven()
        {
            // Act
            var range = ZoomRange.FromOptions(100f, null, 50f, 2f, 1.5f);

            // Assert
            Assert.AreEqual(1.6f, range.Min, TOLERANCE);
            Assert.AreEqual(1.6f, range.Max, TOLERANCE);
            Assert.AreEqual(1.6f, range.Start, TOLERANCE);
        }

        [Test]
        public void StartAtTheClosestViewWhenWheelStartIs100()
        {
            // Act
            var range = ZoomRange.FromOptions(null, 1.5f, 100f, 2f, 1.5f);

            // Assert
            Assert.AreEqual(1f / 1.5f, range.Min, TOLERANCE);
            Assert.AreEqual(1f, range.Max, TOLERANCE);
            Assert.AreEqual(1f, range.Start, TOLERANCE);
        }

        [Test]
        public void StartAtTheFarthestViewWhenWheelStartIs0()
        {
            // Act
            var range = ZoomRange.FromOptions(100f, 2f, 0f, 2f, 1.5f);

            // Assert
            Assert.AreEqual(0.8f, range.Min, TOLERANCE);
            Assert.AreEqual(1.6f, range.Max, TOLERANCE);
            Assert.AreEqual(0.8f, range.Start, TOLERANCE);
        }

        [Test]
        public void StartHalfwayInRadiusWhenWheelStartIs50()
        {
            // Act: radius limits 2.1875 and 4.375 give a start radius of 3.28125, a factor of 1.6 / 1.5.
            var range = ZoomRange.FromOptions(100f, 2f, 50f, 2f, 1.5f);

            // Assert
            Assert.AreEqual(1.6f / 1.5f, range.Start, TOLERANCE);
        }

        [Test]
        public void BoundAnExtremeWheelZoom()
        {
            // Act: a pull-back of 1000 is held at 100, so the farthest view stays a finite float.
            var range = ZoomRange.FromOptions(100f, 1000f, 0f, 2f, 1.5f);

            // Assert
            Assert.AreEqual(0.016f, range.Min, TOLERANCE);
            Assert.AreEqual(0.016f, range.Start, TOLERANCE);
        }

        [Test]
        public void ClampIntoTheRange()
        {
            // Arrange
            var range = new ZoomRange(0.5f, 2f, 1f);

            // Assert
            Assert.AreEqual(0.5f, range.Clamp(0.1f), TOLERANCE);
            Assert.AreEqual(2f, range.Clamp(5f), TOLERANCE);
            Assert.AreEqual(1.3f, range.Clamp(1.3f), TOLERANCE);
        }

        [Test]
        public void PullCloserOnAPositiveZoomDelta()
        {
            // Arrange
            var range = new ZoomRange(0.5f, 4f, 1f);

            // Act
            var closer = range.StepByZoomDelta(1f, 0.1f);
            var farther = range.StepByZoomDelta(1f, -0.1f);

            // Assert: one metre off a 3.5 m orbit.
            Assert.AreEqual(3.5f / 2.5f, closer, TOLERANCE);
            Assert.AreEqual(3.5f / 4.5f, farther, TOLERANCE);
        }

        [Test]
        public void StopAtTheLimitsWhenSteppingByRadius()
        {
            // Arrange
            var range = new ZoomRange(0.5f, 2f, 1f);

            // Act
            var closest = range.StepByRadius(1f, -100f);
            var farthest = range.StepByRadius(1f, 100f);

            // Assert
            Assert.AreEqual(2f, closest, TOLERANCE);
            Assert.AreEqual(0.5f, farthest, TOLERANCE);
        }
    }
}
