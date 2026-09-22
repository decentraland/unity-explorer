using NUnit.Framework;
using UnityEngine;

namespace DCL.InWorldCamera.Tests
{
    public class ScreenshotMetadataBuilderShould
    {
        private const float TOLERANCE = 0.01f;

        private Camera camera;

        [SetUp]
        public void Setup()
        {
            camera = new GameObject(nameof(ScreenshotMetadataBuilderShould)).AddComponent<Camera>();
            camera.transform.position = Vector3.zero;
            camera.transform.rotation = Quaternion.identity;
            camera.fieldOfView = 60f;
            camera.aspect = ScreenRecorder.TARGET_ASPECT_RATIO;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(camera.gameObject);
        }

        [Test]
        public void CentreTheRectOnBoundsInFrontOfTheCamera()
        {
            // Arrange
            var bounds = new Bounds(new Vector3(0f, 0f, 5f), Vector3.one);

            // Act
            Rect rect = ScreenshotMetadataBuilder.CalculateScreenRect(camera, bounds);

            // Assert
            Assert.AreEqual(0.5f, rect.center.x, TOLERANCE);
            Assert.AreEqual(0.5f, rect.center.y, TOLERANCE);
            Assert.Greater(rect.width, 0f);
            Assert.Greater(rect.height, 0f);
        }

        [Test]
        public void MeasureTheRectFromTheTopOfTheImage()
        {
            // Arrange
            var above = new Bounds(new Vector3(0f, 1.5f, 5f), Vector3.one);
            var below = new Bounds(new Vector3(0f, -1.5f, 5f), Vector3.one);

            // Act
            Rect aboveRect = ScreenshotMetadataBuilder.CalculateScreenRect(camera, above);
            Rect belowRect = ScreenshotMetadataBuilder.CalculateScreenRect(camera, below);

            // Assert
            Assert.Less(aboveRect.center.y, 0.5f);
            Assert.Greater(belowRect.center.y, 0.5f);
        }

        [Test]
        public void ReturnZeroForBoundsBehindTheCamera()
        {
            // Arrange
            var bounds = new Bounds(new Vector3(0f, 0f, -5f), Vector3.one);

            // Act
            Rect rect = ScreenshotMetadataBuilder.CalculateScreenRect(camera, bounds);

            // Assert
            Assert.AreEqual(Rect.zero, rect);
        }

        [Test]
        public void ReturnZeroForBoundsOutsideTheCroppedFrame()
        {
            // Arrange — far enough to the side that the photo's crop leaves it out.
            var bounds = new Bounds(new Vector3(20f, 0f, 5f), Vector3.one);

            // Act
            Rect rect = ScreenshotMetadataBuilder.CalculateScreenRect(camera, bounds);

            // Assert
            Assert.AreEqual(Rect.zero, rect);
        }

        [Test]
        public void ClampTheRectToTheImageForBoundsLargerThanTheFrame()
        {
            // Arrange
            var bounds = new Bounds(new Vector3(0f, 0f, 5f), new Vector3(100f, 100f, 1f));

            // Act
            Rect rect = ScreenshotMetadataBuilder.CalculateScreenRect(camera, bounds);

            // Assert
            Assert.AreEqual(0f, rect.xMin, TOLERANCE);
            Assert.AreEqual(0f, rect.yMin, TOLERANCE);
            Assert.AreEqual(1f, rect.xMax, TOLERANCE);
            Assert.AreEqual(1f, rect.yMax, TOLERANCE);
        }

        [Test]
        public void KeepTheSameFrameWhateverTheScreenAspectRatioIs()
        {
            // Arrange — the photo is always 16:9, so a wider screen crops the sides, not the subject.
            var bounds = new Bounds(new Vector3(0f, 0f, 5f), Vector3.one);
            camera.aspect = ScreenRecorder.TARGET_ASPECT_RATIO * 1.5f;

            // Act
            Rect rect = ScreenshotMetadataBuilder.CalculateScreenRect(camera, bounds);

            // Assert
            Assert.AreEqual(0.5f, rect.center.x, TOLERANCE);
            Assert.AreEqual(0.5f, rect.center.y, TOLERANCE);
        }
    }
}
