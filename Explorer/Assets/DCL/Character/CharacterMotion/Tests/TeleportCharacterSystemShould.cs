using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Systems;
using DCL.Multiplayer.Movement;
using DCL.Utilities;
using ECS;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Reporting;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
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
        private IMovementMessageBus? teleportBroadcast;
        private CharacterController characterController;
        private Camera? camera;
        private readonly List<GameObject> spawnedColliders = new ();

        [SetUp]
        public void Setup()
        {
            teleportBroadcast = Substitute.For<IMovementMessageBus>();
            system = new TeleportCharacterSystem(world, sceneReadinessReportQueue = Substitute.For<ISceneReadinessReportQueue>(), teleportBroadcast);
            characterController = new GameObject().AddComponent<CharacterController>();
            camera = new GameObject().AddComponent<Camera>();

            camera.transform.position = new Vector3(2, 2, 0);
        }

        [TearDown]
        public void CleanUp()
        {
            UnityObjectUtils.SafeDestroyGameObject(camera);
            UnityObjectUtils.SafeDestroyGameObject(characterController);

            foreach (GameObject collider in spawnedColliders)
                UnityObjectUtils.SafeDestroy(collider);

            spawnedColliders.Clear();
        }

        // Spawns a horizontal box collider whose top face sits at worldTopY, wide enough to cover the
        // whole land-on-parcel probe grid centred on (x,z). Default layer 0 is part of CHARACTER_ONLY_MASK.
        private void SpawnFloor(float worldTopY, float x = 8f, float z = 8f)
        {
            const float THICKNESS = 1f;
            var go = new GameObject("test-floor");
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(100f, THICKNESS, 100f);
            go.transform.position = new Vector3(x, worldTopY - (THICKNESS / 2f), z);
            spawnedColliders.Add(go);
            Physics.SyncTransforms();
        }

        private static PlayerTeleportIntent LandOnParcelIntent(Vector3 position) =>
            new (null, new Vector2Int(0, 0), position, CancellationToken.None, isPositionSet: true, landOnParcel: true);

        [Test]
        public void ResolveTeleportImmediatelyWithoutAssetsToWait()
        {
            Entity e = world.Create(characterController, new CharacterPlatformComponent(), new CharacterRigidTransform(),
                new PlayerTeleportIntent(null, new Vector2Int(22, 22), Vector3.one * 100, CancellationToken.None, isPositionSet: true));

            system!.Update(0);

            Assert.That(world.Has<PlayerTeleportIntent>(e), Is.False);
            Assert.That(characterController.transform.position, Is.EqualTo(Vector3.one * 100));
            teleportBroadcast!.Received(1).BroadcastTeleport(Vector3.one * 100);
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
            var teleportIntent = new PlayerTeleportIntent(null, new Vector2Int(22, 22), Vector3.one * 100, CancellationToken.None, loadReport, isPositionSet: true);

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
            teleportBroadcast!.DidNotReceive().BroadcastTeleport(Arg.Any<Vector3>());
        }

        [Test]
        public void PreserveIsGroundedOnInPlaceRotation()
        {
            Vector3 samePosition = new Vector3(10, 5, 10);
            characterController.transform.position = samePosition;

            var rigidTransform = new CharacterRigidTransform { IsGrounded = true };
            Entity e = world.Create(characterController, new CharacterPlatformComponent(), rigidTransform,
                new PlayerTeleportIntent(null, new Vector2Int(22, 22), samePosition, CancellationToken.None, isPositionSet: true));

            system!.Update(0);

            Assert.That(world.Has<PlayerTeleportIntent>(e), Is.False, "Teleport intent should be resolved");
            Assert.That(characterController.transform.position, Is.EqualTo(samePosition), "Position should remain unchanged");

            // Verify IsGrounded is preserved when position doesn't change (in-place rotation scenario)
            CharacterRigidTransform updatedRigidTransform = world.Get<CharacterRigidTransform>(e);
            Assert.That(updatedRigidTransform.IsGrounded, Is.True, "IsGrounded should be preserved for in-place rotation");
        }

        [Test]
        public void LandOnParcelSnapsDownOntoFloorCollider()
        {
            // Floor top at y=0; the avatar is anchored 5m above it and should snap onto the floor.
            SpawnFloor(worldTopY: 0f);

            Entity e = world.Create(characterController, new CharacterPlatformComponent(), new CharacterRigidTransform(),
                LandOnParcelIntent(new Vector3(8f, 5f, 8f)));

            system!.Update(0);

            Assert.That(world.Has<PlayerTeleportIntent>(e), Is.False, "Teleport intent should be resolved");
            Assert.That(characterController.transform.position.y, Is.EqualTo(0.1f).Within(0.001f), "Should land just above the floor (top + ground clearance)");
        }

        [Test]
        public void LandOnParcelPicksHighestWalkableSurfaceAndIgnoresRoof()
        {
            // A terraced parcel: low plane at y=0, an elevated walkable step at y=3, and a roof at y=50.
            // The step is within a step-up of the lowest floor; the roof is far overhead and must be ignored.
            SpawnFloor(worldTopY: 0f);
            SpawnFloor(worldTopY: 3f);
            SpawnFloor(worldTopY: 50f);

            Entity e = world.Create(characterController, new CharacterPlatformComponent(), new CharacterRigidTransform(),
                LandOnParcelIntent(new Vector3(8f, 0f, 8f)));

            system!.Update(0);

            Assert.That(world.Has<PlayerTeleportIntent>(e), Is.False, "Teleport intent should be resolved");
            Assert.That(characterController.transform.position.y, Is.EqualTo(3.1f).Within(0.001f), "Should land on the elevated step, not the lower plane or the roof");
        }

        [Test]
        public void LandOnParcelKeepsAnchorWhenNoFloorFound()
        {
            // No colliders in the probe grid: the precomputed anchor position must be preserved as-is.
            var anchor = new Vector3(8f, 5f, 8f);

            Entity e = world.Create(characterController, new CharacterPlatformComponent(), new CharacterRigidTransform(),
                LandOnParcelIntent(anchor));

            system!.Update(0);

            Assert.That(world.Has<PlayerTeleportIntent>(e), Is.False, "Teleport intent should be resolved");
            Assert.That(characterController.transform.position, Is.EqualTo(anchor), "Should keep the anchored position when there is no floor to snap to");
        }
    }
}
