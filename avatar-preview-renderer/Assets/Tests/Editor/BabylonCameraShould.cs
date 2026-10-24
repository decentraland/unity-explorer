using NUnit.Framework;
using UnityEngine;

namespace Preview.Tests
{
    public class BabylonCameraShould
    {
        private const float TOLERANCE = 0.0001f;

        [TearDown]
        public void ResetConfiguration()
        {
            PreviewConfiguration.RecreateFrom(string.Empty);
        }

        [Test]
        public void KeepTheDefaultForEveryAxisThePageLeftUnset()
        {
            // Arrange
            PreviewConfiguration.RecreateFrom("?cameraY=3");

            // Act
            var position = BabylonCamera.Resolve(PreviewConfiguration.Instance, BabylonCamera.AVATAR);

            // Assert
            Assert.AreEqual(new Vector3(0f, 3f, 3.5f), position);
        }

        [Test]
        public void ChangeNothingFromTheDefaultCamera()
        {
            // Act
            var (alpha, beta) = BabylonCamera.AnglesFrom(BabylonCamera.ITEM, BabylonCamera.ITEM);
            var closer = BabylonCamera.Closer(BabylonCamera.ITEM, BabylonCamera.ITEM);

            // Assert
            Assert.AreEqual(0f, alpha, TOLERANCE);
            Assert.AreEqual(0f, beta, TOLERANCE);
            Assert.AreEqual(1f, closer, TOLERANCE);
        }

        [Test]
        public void TurnAQuarterWhenTheCameraMovesToTheSide()
        {
            // Act: from in front of the avatar to its side, at the same height and distance.
            var (alpha, beta) = BabylonCamera.AnglesFrom(new Vector3(3.5f, 1f, 0f), BabylonCamera.AVATAR);

            // Assert
            Assert.AreEqual(-90f, alpha, TOLERANCE);
            Assert.AreEqual(0f, beta, TOLERANCE);
        }

        [Test]
        public void ShowTheSubjectLargerFromHalfTheDistance()
        {
            // Act
            var closer = BabylonCamera.Closer(BabylonCamera.AVATAR * 0.5f, BabylonCamera.AVATAR);

            // Assert
            Assert.AreEqual(2f, closer, TOLERANCE);
        }

        [Test]
        public void KeepTheViewForACameraOnTheTarget()
        {
            // Act
            var closer = BabylonCamera.Closer(Vector3.zero, BabylonCamera.AVATAR);

            // Assert
            Assert.AreEqual(1f, closer, TOLERANCE);
        }
    }
}
