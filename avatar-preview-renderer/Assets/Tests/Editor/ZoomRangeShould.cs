using NUnit.Framework;

namespace Preview.Tests
{
    public class ZoomRangeShould
    {
        private const float TOLERANCE = 0.0001f;

        [TestCase(0f, 1f)]
        [TestCase(50f, 1.9f)]
        [TestCase(100f, 2.8f)]
        [TestCase(-10f, 1f)]
        [TestCase(250f, 2.8f)]
        public void MapBabylonZoomPercentToAFactor(float percent, float expected)
        {
            // Act
            var factor = ZoomRange.FactorFromPercent(percent);

            // Assert
            Assert.AreEqual(expected, factor, TOLERANCE);
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
            Assert.AreEqual(2.8f, range.Min, TOLERANCE);
            Assert.AreEqual(2.8f, range.Max, TOLERANCE);
            Assert.AreEqual(2.8f, range.Start, TOLERANCE);
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
            Assert.AreEqual(1.4f, range.Min, TOLERANCE);
            Assert.AreEqual(2.8f, range.Max, TOLERANCE);
            Assert.AreEqual(1.4f, range.Start, TOLERANCE);
        }

        [Test]
        public void StartHalfwayInRadiusWhenWheelStartIs50()
        {
            // Act: radius limits 1.25 and 2.5 give a start radius of 1.875, a factor of 2.8 / 1.5.
            var range = ZoomRange.FromOptions(100f, 2f, 50f, 2f, 1.5f);

            // Assert
            Assert.AreEqual(2.8f / 1.5f, range.Start, TOLERANCE);
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
