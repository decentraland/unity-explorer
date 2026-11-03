using NUnit.Framework;

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
        public void FadeOutFromHalfOfMaxDistance(float distance, float maxDistance, float expectedOpacity)
        {
            // Act
            float opacity = NametagMathHelper.CalculateOpacity(distance, maxDistance);

            // Assert
            Assert.That(opacity, Is.EqualTo(expectedOpacity).Within(0.0001f));
        }

        [TestCase(100f, 60f, 100f)]
        [TestCase(10f, 60f, 10f)]
        [TestCase(0f, 60f, 60f)]
        [TestCase(-5f, 60f, 60f)]
        [TestCase(float.NaN, 60f, 60f)]
        [TestCase(float.PositiveInfinity, 60f, 60f)]
        [TestCase(0f, 0f, 40f)]
        [TestCase(0f, -1f, 40f)]
        [TestCase(0f, float.NaN, 40f)]
        [TestCase(float.NaN, float.NaN, 40f)]
        public void ResolveMaxDistanceByCameraThenAvatarThenDefault(float cameraMaxDistance, float avatarMaxDistance, float expectedMaxDistance)
        {
            // Act
            float maxDistance = NametagMathHelper.ResolveMaxDistance(cameraMaxDistance, avatarMaxDistance, 40f);

            // Assert
            Assert.That(maxDistance, Is.EqualTo(expectedMaxDistance));
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
