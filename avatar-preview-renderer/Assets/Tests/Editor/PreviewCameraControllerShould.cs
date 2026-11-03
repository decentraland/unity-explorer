using NUnit.Framework;

namespace Preview.Tests
{
    public class PreviewCameraControllerShould
    {
        private const float TOLERANCE = 0.0001f;

        [Test]
        public void FitTheBoundingSphereOnAWideFrame()
        {
            // Act: radius 0.3, half height 0.4 give a sphere of 0.5.
            var size = PreviewCameraController.OrthoSizeFor(0.3f, 0.4f, 2f, 1f);

            // Assert
            Assert.AreEqual(0.5f, size, TOLERANCE);
        }

        [Test]
        public void WidenForTheRadiusOnANarrowFrame()
        {
            // Act: at aspect 0.6 the radius needs 0.5 / 0.6 of half height, more than the sphere.
            var size = PreviewCameraController.OrthoSizeFor(0.5f, 0.5f, 0.6f, 1f);

            // Assert
            Assert.AreEqual(0.5f / 0.6f, size, TOLERANCE);
        }

        [Test]
        public void ApplyTheMargin()
        {
            // Act
            var size = PreviewCameraController.OrthoSizeFor(0.3f, 0.4f, 1f, 1.05f);

            // Assert
            Assert.AreEqual(0.525f, size, TOLERANCE);
        }
    }
}
