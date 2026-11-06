using NUnit.Framework;
using Unity.Mathematics;

namespace DCL.Nametags.Tests
{
    public class NametagMathHelperShould
    {
        [TestCase(0f, 40f, 1f)]
        [TestCase(20f, 40f, 1f)]
        [TestCase(30f, 40f, 0.5f)]
        [TestCase(40f, 40f, 0f)]
        [TestCase(50f, 40f, 0f)]
        [TestCase(100f, 200f, 1f)]
        [TestCase(150f, 200f, 0.5f)]
        [TestCase(0f, float.PositiveInfinity, 1f)]
        [TestCase(10000f, float.PositiveInfinity, 1f)]
        public void FadeOutFromHalfOfMaxDistance(float distance, float maxDistance, float expectedOpacity)
        {
            // Act
            float opacity = NametagMathHelper.CalculateOpacity(distance, maxDistance);

            // Assert
            Assert.That(opacity, Is.EqualTo(expectedOpacity).Within(0.0001f));
        }

        [Test]
        public void NeverPutTagOutOfRangeWithInfiniteMaxDistance()
        {
            // Arrange
            float maxDistance = float.PositiveInfinity;

            // Act
            bool outOfRange = NametagMathHelper.IsOutOfRenderRange(new float3(0f, 0f, 0f), new float3(100000f, 0f, 0f), maxDistance * maxDistance, 4f);

            // Assert
            Assert.That(outOfRange, Is.False);
        }

        [Test]
        public void DetectMaxDistanceChangeToAndFromInfinity()
        {
            // Act
            bool toInfinity = NametagMathHelper.HasChanged(float.PositiveInfinity, 40f);
            bool fromInfinity = NametagMathHelper.HasChanged(40f, float.PositiveInfinity);
            bool unchanged = NametagMathHelper.HasChanged(float.PositiveInfinity, float.PositiveInfinity);

            // Assert
            Assert.That(toInfinity, Is.True);
            Assert.That(fromInfinity, Is.True);
            Assert.That(unchanged, Is.False);
        }

        [Test]
        public void DetectFovScaleFactorChange()
        {
            // Arrange
            float wideFov = NametagMathHelper.CalculateFovScaleFactor(60f, 0.15f);
            float narrowFov = NametagMathHelper.CalculateFovScaleFactor(20f, 0.15f);

            // Act
            bool changed = NametagMathHelper.HasChanged(narrowFov, wideFov);
            bool unchanged = NametagMathHelper.HasChanged(wideFov, wideFov);

            // Assert
            Assert.That(changed, Is.True);
            Assert.That(unchanged, Is.False);
        }
    }
}
