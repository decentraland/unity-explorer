using NUnit.Framework;
using UnityEngine;

namespace DCL.InWorldCamera.Tests
{
    public class ScreenshotMetadataBuilderShould
    {
        private const float TOLERANCE = 0.01f;

        private Camera? createdCamera;

        [TearDown]
        public void TearDown()
        {
            if (createdCamera != null)
                Object.DestroyImmediate(createdCamera.gameObject);

            createdCamera = null;
        }

        [Test]
        public void CentreTheRectOnBoundsInFrontOfTheCamera()
        {
            // Arrange
            Camera camera = CreateCamera();
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
            Camera camera = CreateCamera();
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
            Camera camera = CreateCamera();
            var bounds = new Bounds(new Vector3(0f, 0f, -5f), Vector3.one);

            // Act
            Rect rect = ScreenshotMetadataBuilder.CalculateScreenRect(camera, bounds);

            // Assert
            Assert.AreEqual(Rect.zero, rect);
        }

        [Test]
        public void ReturnZeroForBoundsOutsideTheCroppedFrame()
        {
            // Arrange — on screen, but in the side band the photo's crop leaves out.
            Camera camera = CreateCamera();
            var bounds = new Bounds(new Vector3(4.8f, 0f, 5f), new Vector3(0.1f, 0.1f, 0.1f));

            // Act
            Rect rect = ScreenshotMetadataBuilder.CalculateScreenRect(camera, bounds);

            // Assert
            Assert.Less(camera.WorldToViewportPoint(bounds.max).x, 1f);
            Assert.AreEqual(Rect.zero, rect);
        }

        [Test]
        public void ClampTheRectToTheImageForBoundsLargerThanTheFrame()
        {
            // Arrange
            Camera camera = CreateCamera();
            var bounds = new Bounds(new Vector3(0f, 0f, 5f), new Vector3(100f, 100f, 1f));

            // Act
            Rect rect = ScreenshotMetadataBuilder.CalculateScreenRect(camera, bounds);

            // Assert
            Assert.AreEqual(0f, rect.xMin, TOLERANCE);
            Assert.AreEqual(0f, rect.yMin, TOLERANCE);
            Assert.AreEqual(1f, rect.xMax, TOLERANCE);
            Assert.AreEqual(1f, rect.yMax, TOLERANCE);
        }

        [TestCase(1f)]
        [TestCase(1.5f)]
        public void KeepTheSameFrameWhateverTheScreenAspectRatioIs(float screenToTargetAspectRatio)
        {
            // Arrange — the near face of a unit cube centred 5 away sits 4.5 from the camera.
            Camera camera = CreateCamera(ScreenRecorder.TARGET_ASPECT_RATIO * screenToTargetAspectRatio);
            var bounds = new Bounds(new Vector3(0f, 0f, 5f), Vector3.one);
            float viewportHeight = 1f / (2f * Mathf.Tan(camera.fieldOfView / 2f * Mathf.Deg2Rad) * 4.5f);

            // Act
            Rect rect = ScreenshotMetadataBuilder.CalculateScreenRect(camera, bounds);

            // Assert
            Assert.AreEqual(0.5f, rect.center.x, TOLERANCE);
            Assert.AreEqual(0.5f, rect.center.y, TOLERANCE);
            Assert.AreEqual(viewportHeight / ScreenRecorder.FRAME_SCALE, rect.height, TOLERANCE);
            Assert.AreEqual(viewportHeight / ScreenRecorder.FRAME_SCALE / ScreenRecorder.TARGET_ASPECT_RATIO, rect.width, TOLERANCE);
        }

        [Test]
        public void HandleNarrowerThanTargetScreenAspectRatio()
        {
            // Arrange — a screen narrower than 16:9 exercises the width-limited scaling path, where the crop
            // keeps the full width share and trims the top and bottom instead.
            Camera camera = CreateCamera(ScreenRecorder.TARGET_ASPECT_RATIO * 0.5f);
            var bounds = new Bounds(new Vector3(0f, 0f, 5f), Vector3.one);
            float viewportHeight = 1f / (2f * Mathf.Tan(camera.fieldOfView / 2f * Mathf.Deg2Rad) * 4.5f);
            float frameHeightShare = ScreenRecorder.FRAME_SCALE * camera.aspect / ScreenRecorder.TARGET_ASPECT_RATIO;

            // Act
            Rect rect = ScreenshotMetadataBuilder.CalculateScreenRect(camera, bounds);

            // Assert
            Assert.AreEqual(0.5f, rect.center.x, TOLERANCE);
            Assert.AreEqual(0.5f, rect.center.y, TOLERANCE);
            Assert.AreEqual(viewportHeight / frameHeightShare, rect.height, TOLERANCE);
            Assert.AreEqual(viewportHeight / camera.aspect / ScreenRecorder.FRAME_SCALE, rect.width, TOLERANCE);
        }

        [Test]
        public void ReachTheEdgeOfTheImageForBoundsThatStraddleTheCamera()
        {
            // Arrange — the box spans z −1 → 3, just right of the camera, so part of it is behind the lens.
            Camera camera = CreateCamera();
            var bounds = new Bounds(new Vector3(0.3f, 0f, 1f), new Vector3(0.2f, 1f, 4f));

            // Act
            Rect rect = ScreenshotMetadataBuilder.CalculateScreenRect(camera, bounds);

            // Assert
            Assert.AreEqual(1f, rect.xMax, TOLERANCE);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MeasureTheCharacterWhetherOrNotItsControllerIsEnabled(bool controllerEnabled)
        {
            // Arrange — emotes switch the controller off, which empties its collider bounds.
            var character = new GameObject(nameof(MeasureTheCharacterWhetherOrNotItsControllerIsEnabled));

            try
            {
                character.transform.position = new Vector3(10f, 2f, 30f);
                character.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
                CharacterController controller = character.AddComponent<CharacterController>();
                controller.center = new Vector3(0f, 1f, 0f);
                controller.radius = 0.3f;
                controller.height = 2f;
                controller.enabled = controllerEnabled;
                Physics.SyncTransforms();

                // Act
                Bounds bounds = ScreenshotMetadataBuilder.CalculateCharacterBounds(controller);

                // Assert — enabled, Unity measures the same capsule itself; disabled is what the shape adds.
                if (controllerEnabled)
                {
                    // Unity may pad the collider's box by the skin width, which the shape leaves out.
                    float skinTolerance = (controller.skinWidth * 2f) + TOLERANCE;
                    AssertApproximately(controller.bounds.center, bounds.center);
                    Assert.AreEqual(controller.bounds.size.x, bounds.size.x, skinTolerance);
                    Assert.AreEqual(controller.bounds.size.y, bounds.size.y, skinTolerance);
                    Assert.AreEqual(controller.bounds.size.z, bounds.size.z, skinTolerance);
                }

                AssertApproximately(new Vector3(10f, 3f, 30f), bounds.center);
                AssertApproximately(new Vector3(0.6f, 2f, 0.6f), bounds.size);
            }
            finally
            {
                Object.DestroyImmediate(character);
            }
        }

        [Test]
        public void KeepTheCharacterAtLeastAsTallAsItIsWide()
        {
            // Arrange — Unity draws a capsule shorter than its diameter as a sphere.
            var character = new GameObject(nameof(KeepTheCharacterAtLeastAsTallAsItIsWide));

            try
            {
                CharacterController controller = character.AddComponent<CharacterController>();
                controller.radius = 0.5f;
                controller.height = 0.2f;

                // Act
                Bounds bounds = ScreenshotMetadataBuilder.CalculateCharacterBounds(controller);

                // Assert
                AssertApproximately(Vector3.one, bounds.size);
            }
            finally
            {
                Object.DestroyImmediate(character);
            }
        }

        private static void AssertApproximately(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, TOLERANCE);
            Assert.AreEqual(expected.y, actual.y, TOLERANCE);
            Assert.AreEqual(expected.z, actual.z, TOLERANCE);
        }

        /// <summary>
        /// A camera at the origin looking down +Z, framing what the in-world one frames.
        /// </summary>
        private Camera CreateCamera(float aspectRatio = ScreenRecorder.TARGET_ASPECT_RATIO)
        {
            createdCamera = new GameObject(nameof(ScreenshotMetadataBuilderShould)).AddComponent<Camera>();
            createdCamera.transform.position = Vector3.zero;
            createdCamera.transform.rotation = Quaternion.identity;
            createdCamera.fieldOfView = 60f;
            createdCamera.aspect = aspectRatio;

            return createdCamera;
        }
    }
}
