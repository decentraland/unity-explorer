using NUnit.Framework;
using UnityEngine;

namespace Utility.Tests
{
    public class ScreenBoundsProjectionShould
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
        public void CentreABoxInFrontOfTheCamera()
        {
            // Arrange
            Camera camera = CreateCamera();
            var bounds = new Bounds(new Vector3(0f, 0f, 5f), Vector3.one);

            // Act
            bool projected = ScreenBoundsProjection.TryProjectToViewport(camera, in bounds, out Rect viewport);

            // Assert
            Assert.IsTrue(projected);
            Assert.AreEqual(0.5f, viewport.center.x, TOLERANCE);
            Assert.AreEqual(0.5f, viewport.center.y, TOLERANCE);
            Assert.Greater(viewport.width, 0f);
            Assert.Greater(viewport.height, 0f);
        }

        [Test]
        public void KeepTheViewportOrientationWithYGrowingUpward()
        {
            // Arrange
            Camera camera = CreateCamera();
            var above = new Bounds(new Vector3(0f, 1.5f, 5f), Vector3.one);

            // Act
            ScreenBoundsProjection.TryProjectToViewport(camera, in above, out Rect viewport);

            // Assert
            Assert.Greater(viewport.center.y, 0.5f, "viewport space is bottom-left; a box above the axis projects above the centre");
        }

        [Test]
        public void ReturnFalseForABoxBehindTheCamera()
        {
            // Arrange
            Camera camera = CreateCamera();
            var bounds = new Bounds(new Vector3(0f, 0f, -5f), Vector3.one);

            // Act
            bool projected = ScreenBoundsProjection.TryProjectToViewport(camera, in bounds, out Rect viewport);

            // Assert
            Assert.IsFalse(projected);
            Assert.AreEqual(Rect.zero, viewport);
        }

        [Test]
        public void ReachBeyondTheViewportForABoxStraddlingTheLens()
        {
            // Arrange — the box spans z −1 → 3, just right of the camera, so part of it is behind the lens.
            Camera camera = CreateCamera();
            var bounds = new Bounds(new Vector3(0.3f, 0f, 1f), new Vector3(0.2f, 1f, 4f));

            // Act
            bool projected = ScreenBoundsProjection.TryProjectToViewport(camera, in bounds, out Rect viewport);

            // Assert
            Assert.IsTrue(projected);
            Assert.GreaterOrEqual(viewport.xMax, 1f, "the near-plane crossing lands at the edge of the view, unclamped");
        }

        [Test]
        public void NotClampABoxLargerThanTheView()
        {
            // Arrange
            Camera camera = CreateCamera();
            var bounds = new Bounds(new Vector3(0f, 0f, 5f), new Vector3(100f, 100f, 1f));

            // Act
            ScreenBoundsProjection.TryProjectToViewport(camera, in bounds, out Rect viewport);

            // Assert
            Assert.Less(viewport.xMin, 0f);
            Assert.Greater(viewport.xMax, 1f);
        }

        private Camera CreateCamera()
        {
            createdCamera = new GameObject(nameof(ScreenBoundsProjectionShould)).AddComponent<Camera>();
            createdCamera.transform.position = Vector3.zero;
            createdCamera.transform.rotation = Quaternion.identity;
            createdCamera.fieldOfView = 60f;
            createdCamera.aspect = 16f / 9f;

            return createdCamera;
        }
    }
}
