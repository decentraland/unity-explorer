using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Systems;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Reporting;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;
using Utility;

namespace DCL.CharacterMotion.Tests
{
    public class TeleportCharacterSystemShould : UnitySystemTestBase<TeleportCharacterSystem>
    {
        private ISceneReadinessReportQueue? sceneReadinessReportQueue;
        private CharacterController characterController;
        private Camera? camera;

        [SetUp]
        public void Setup()
        {
            system = new TeleportCharacterSystem(world, sceneReadinessReportQueue = Substitute.For<ISceneReadinessReportQueue>());
            characterController = new GameObject().AddComponent<CharacterController>();
            camera = new GameObject().AddComponent<Camera>();

            camera.transform.position = new Vector3(2, 2, 0);
        }

        [TearDown]
        public void CleanUp()
        {
            UnityObjectUtils.SafeDestroyGameObject(camera);
            UnityObjectUtils.SafeDestroyGameObject(characterController);
        }

        [Test]
        public void RestoreCameraDataOnFailure([Values(UniTaskStatus.Faulted, UniTaskStatus.Canceled)] UniTaskStatus status)
        {
            var cameraSamplingData = new CameraSamplingData();
            cameraSamplingData.Position = new Vector3(50, 50, 0);

            Entity camEntity = world.Create(new CameraComponent(camera!), cameraSamplingData);
            var loadReport = AsyncLoadProcessReport.Create(CancellationToken.None);
            var teleportIntent = new PlayerTeleportIntent(new Vector3(100, 100, 100), new Vector2Int(22, 22), CancellationToken.None, loadReport);

            Entity e = world.Create(characterController, new CharacterPlatformComponent(), teleportIntent);

            if (status == UniTaskStatus.Faulted)
            {
                loadReport.SetException(new Exception());
                LogAssert.Expect(LogType.Exception, "Exception: Exception of type 'System.Exception' was thrown.");
            }
            else
                loadReport.SetCancelled();

            system!.Update(0);

            Assert.That(cameraSamplingData.Position, Is.EqualTo(camera!.transform.position));
            Assert.That(cameraSamplingData.IsDirty, Is.True);
        }
    }
}
