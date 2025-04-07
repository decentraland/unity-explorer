using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Systems;
using DCL.Utilities;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Reporting;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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
        public void ResolveTeleportImmediatelyWithoutAssetsToWait()
        {
            Entity e = world.Create(characterController, new CharacterPlatformComponent(), new CharacterRigidTransform(),
                new PlayerTeleportIntent(null, new Vector2Int(22, 22), Vector3.one * 100, CancellationToken.None, isPositionSet: true));

            system!.Update(0);

            Assert.That(world.Has<PlayerTeleportIntent>(e), Is.False);
            Assert.That(characterController.transform.position, Is.EqualTo(Vector3.one * 100));
        }

        [Test]
        public async Task RestoreCameraDataOnFailureAsync([Values(UniTaskStatus.Faulted, UniTaskStatus.Canceled)] UniTaskStatus status)
        {
            var cameraSamplingData = new CameraSamplingData
            {
                Position = new Vector3(50, 50, 0),
            };

            Entity camEntity = world.Create(new CameraComponent(camera!), cameraSamplingData);
            var loadReport = AsyncLoadProcessReport.Create(CancellationToken.None);
            var teleportIntent = new PlayerTeleportIntent(null, new Vector2Int(22, 22), Vector3.one * 100, CancellationToken.None, loadReport);

            Entity e = world.Create(characterController, new CharacterPlatformComponent(), new CharacterRigidTransform(), teleportIntent);

            if (status == UniTaskStatus.Faulted)
            {
                loadReport.SetException(new Exception(nameof(RestoreCameraDataOnFailureAsync)));
                LogAssert.Expect(LogType.Exception, new Regex($".*{nameof(RestoreCameraDataOnFailureAsync)}.*"));
            }
            else
                loadReport.SetCancelled();

            // Consume unobserved UniTask exception, otherwise it will be throws from the destructor
            await loadReport.WaitUntilFinishedAsync();

            system!.Update(0);

            Assert.That(cameraSamplingData.Position, Is.EqualTo(camera!.transform.position));
            Assert.That(cameraSamplingData.IsDirty, Is.True);
        }
    }
}
