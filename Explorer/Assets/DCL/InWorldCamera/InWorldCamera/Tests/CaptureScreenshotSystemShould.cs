using DCL.InWorldCamera.Systems;
using NUnit.Framework;
using UnityEngine;

namespace DCL.InWorldCamera.Tests
{
    public class CaptureScreenshotSystemShould
    {
        private Camera? createdCamera;

        [TearDown]
        public void TearDown()
        {
            if (createdCamera != null)
                Object.DestroyImmediate(createdCamera.gameObject);

            createdCamera = null;
        }

        [Test]
        public void LeaveOutPeopleInTheBandThePhotoCropsAway()
        {
            // Arrange — a screen wider than 16:9, so the photo crops its sides.
            Camera camera = CreateCamera(ScreenRecorder.TARGET_ASPECT_RATIO * 1.5f);
            var inTheCroppedBand = new Bounds(new Vector3(6f, 0f, 5f), new Vector3(0.1f, 0.1f, 0.1f));
            var centred = new Bounds(new Vector3(0f, 0f, 5f), Vector3.one);

            // Act
            Plane[] frustumPlanes = CaptureScreenshotSystem.CalculatePhotoFrustumPlanes(camera);

            // Assert
            Assert.Less(camera.WorldToViewportPoint(inTheCroppedBand.max).x, 1f);
            Assert.IsFalse(GeometryUtility.TestPlanesAABB(frustumPlanes, inTheCroppedBand));
            Assert.IsTrue(GeometryUtility.TestPlanesAABB(frustumPlanes, centred));
        }

        /// <summary>
        /// A camera at the origin looking down +Z, framing what the in-world one frames.
        /// </summary>
        private Camera CreateCamera(float aspectRatio)
        {
            createdCamera = new GameObject(nameof(CaptureScreenshotSystemShould)).AddComponent<Camera>();
            createdCamera.transform.position = Vector3.zero;
            createdCamera.transform.rotation = Quaternion.identity;
            createdCamera.fieldOfView = 60f;
            createdCamera.aspect = aspectRatio;

            return createdCamera;
        }
    }
}
