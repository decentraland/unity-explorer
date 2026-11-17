using Cinemachine;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.McpServer.Utils;
using NUnit.Framework;
using System.Threading;
using UnityEngine;
using CameraType = DCL.ECSComponents.CameraType;

namespace DCL.McpServer.Tests
{
    public class CameraBlendWaitShould
    {
        private IExposedCameraData.Fake camera = null!;
        private GameObject? brainGameObject;

        [SetUp]
        public void SetUp()
        {
            camera = new IExposedCameraData.Fake(Vector3.zero, Quaternion.identity, CameraType.CtThirdPerson, pointerIsLocked: false);
        }

        [TearDown]
        public void TearDown()
        {
            if (brainGameObject != null)
                Object.DestroyImmediate(brainGameObject);
        }

        [Test]
        public void ReturnImmediatelyWithoutABrain()
        {
            // Act
            UniTask<(int waitedMs, bool stillBlending)> wait = CameraBlendWait.WaitAsync(camera, 1.5f, CancellationToken.None);

            // Assert
            Assert.That(wait.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            (int waitedMs, bool stillBlending) = wait.GetAwaiter().GetResult();
            Assert.That(waitedMs, Is.EqualTo(0));
            Assert.That(stillBlending, Is.False);
        }

        [Test]
        public void ReturnImmediatelyWhenNotBlending()
        {
            // Arrange
            brainGameObject = new GameObject(nameof(CameraBlendWaitShould));
            camera.CinemachineBrain = brainGameObject.AddComponent<CinemachineBrain>();

            // Act
            UniTask<(int waitedMs, bool stillBlending)> wait = CameraBlendWait.WaitAsync(camera, 1.5f, CancellationToken.None);

            // Assert
            Assert.That(wait.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(wait.GetAwaiter().GetResult().stillBlending, Is.False);
        }
    }
}
