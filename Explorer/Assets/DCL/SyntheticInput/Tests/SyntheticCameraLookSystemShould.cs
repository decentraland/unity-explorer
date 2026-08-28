using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.Input.Systems;
using DCL.SyntheticInput.Components;
using DCL.SyntheticInput.Systems;
using ECS.TestSuite;
using NUnit.Framework;
using UnityEngine;

namespace DCL.SyntheticInput.Tests
{
    public class SyntheticCameraLookSystemShould : UnitySystemTestBase<SyntheticCameraLookSystem>
    {
        private Entity playerEntity;
        private Entity cameraEntity;

        private GameObject cameraGo = null!;
        private GameObject playerGo = null!;

        [SetUp]
        public void SetUp()
        {
            cameraGo = new GameObject("synthetic-look-test-camera");
            playerGo = new GameObject("synthetic-look-test-player");

            cameraEntity = world.Create(new CameraComponent(cameraGo.AddComponent<Camera>()), new CameraInput());
            playerEntity = world.Create(new CharacterTransform(playerGo.transform));

            system = new SyntheticCameraLookSystem(world, playerEntity);
            system.Initialize();
        }

        protected override void OnTearDown()
        {
            Object.DestroyImmediate(cameraGo);
            Object.DestroyImmediate(playerGo);
        }

        private UniTaskCompletionSource<SyntheticInputDelivery> AddIntent(Vector2 axisValue, float secondsFromNow, Vector3? lookAtTarget = null)
        {
            var completion = new UniTaskCompletionSource<SyntheticInputDelivery>();

            world.Add(playerEntity, new SyntheticCameraLookIntent
            {
                AxisValue = axisValue,
                EndTime = UnityEngine.Time.time + secondsFromNow,
                LookAtTarget = lookAtTarget,
                Completion = completion,
            });

            return completion;
        }

        private ref CameraInput cameraInput => ref world.Get<CameraInput>(cameraEntity);

        [Test]
        public void ReassertDeltaOverRealInputWhileHeld()
        {
            var axisValue = new Vector2(5f, -2f);
            UniTaskCompletionSource<SyntheticInputDelivery> completion = AddIntent(axisValue, secondsFromNow: 100f);

            // The real camera input system zeroed the delta earlier this frame (cursor not locked).
            cameraInput.Delta = Vector2.zero;

            system.Update(0);

            Assert.That(cameraInput.Delta, Is.EqualTo(axisValue));
            Assert.That(completion.Task.Status, Is.EqualTo(UniTaskStatus.Pending));
        }

        [Test]
        public void SuppressDeltaWhileCameraIsBlocked()
        {
            UniTaskCompletionSource<SyntheticInputDelivery> completion = AddIntent(new Vector2(5f, 0f), secondsFromNow: 100f);

            world.Add(cameraEntity, new CameraBlockerComponent());
            cameraInput.Delta = Vector2.zero;

            system.Update(0);

            Assert.That(cameraInput.Delta, Is.EqualTo(Vector2.zero));
            Assert.That(completion.Task.Status, Is.EqualTo(UniTaskStatus.Pending), "the hold keeps running; only its effect is suppressed");
        }

        [Test]
        public void CompleteOnExpiry()
        {
            UniTaskCompletionSource<SyntheticInputDelivery> completion = AddIntent(new Vector2(5f, 0f), secondsFromNow: -1f);

            system.Update(0);

            Assert.That(world.Has<SyntheticCameraLookIntent>(playerEntity), Is.False);
            Assert.That(completion.Task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(completion.Task.GetAwaiter().GetResult(), Is.EqualTo(SyntheticInputDelivery.Completed));
        }

        [Test]
        public void IssueLookAtIntentAndCompleteOnceTheCameraIsOnTarget()
        {
            // Straight ahead of the test camera (identity rotation looks down +Z), so no refinement is needed.
            var target = new Vector3(0f, 0f, 30f);
            UniTaskCompletionSource<SyntheticInputDelivery> completion = AddIntent(Vector2.zero, secondsFromNow: 0f, lookAtTarget: target);

            system.Update(0);

            Assert.That(world.Has<CameraLookAtIntent>(cameraEntity), Is.True);
            Assert.That(world.Get<CameraLookAtIntent>(cameraEntity).LookAtTarget, Is.EqualTo(target));
            Assert.That(completion.Task.Status, Is.EqualTo(UniTaskStatus.Pending));

            // The camera intent survives a frame in which the Cinemachine systems did not run yet.
            system.Update(0);
            Assert.That(completion.Task.Status, Is.EqualTo(UniTaskStatus.Pending));

            // ApplyCinemachineCameraInputSystem removes the intent once it applied the rotation.
            world.Remove<CameraLookAtIntent>(cameraEntity);
            system.Update(0);

            Assert.That(world.Has<SyntheticCameraLookIntent>(playerEntity), Is.False);
            Assert.That(completion.Task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(completion.Task.GetAwaiter().GetResult(), Is.EqualTo(SyntheticInputDelivery.Completed));
            Assert.That(cameraInput.Delta, Is.EqualTo(Vector2.zero));
        }

        /// <summary>
        ///     The production look-at drives the rig's orbit value and leaves the aim off the point (right yaw,
        ///     wrong pitch on a third-person rig); the request must keep steering the look input until the target
        ///     is actually under the reticle.
        /// </summary>
        [Test]
        public void RefineTheAimWhileTheCameraStillMissesTheTarget()
        {
            // 45 degrees to the right of, and above, the camera's forward.
            var target = new Vector3(30f, 30f, 30f);
            UniTaskCompletionSource<SyntheticInputDelivery> completion = AddIntent(Vector2.zero, secondsFromNow: 0f, lookAtTarget: target);

            system.Update(0);
            world.Remove<CameraLookAtIntent>(cameraEntity);

            cameraInput.Delta = Vector2.zero;
            system.Update(0);

            Assert.That(completion.Task.Status, Is.EqualTo(UniTaskStatus.Pending), "the request stays open while the aim is off");
            Assert.That(cameraInput.Delta.x, Is.GreaterThan(0f), "the target is to the right, so the look input turns right");
            Assert.That(cameraInput.Delta.y, Is.GreaterThan(0f), "the target is above, so the look input looks up");
        }

        [Test]
        public void StopRefiningWhenTheRigCannotGetAnyCloser()
        {
            // Nothing in the test moves the camera, so every frame measures the same error — what a clamped rig
            // (third-person pitch limits) looks like from here.
            UniTaskCompletionSource<SyntheticInputDelivery> completion = AddIntent(Vector2.zero, secondsFromNow: 0f, lookAtTarget: new Vector3(0f, 100f, 1f));

            system.Update(0);
            world.Remove<CameraLookAtIntent>(cameraEntity);

            for (var frame = 0; frame < 32 && completion.Task.Status == UniTaskStatus.Pending; frame++)
                system.Update(0);

            Assert.That(completion.Task.Status, Is.EqualTo(UniTaskStatus.Succeeded), "the refinement gives up instead of holding the request open");
            Assert.That(world.Has<SyntheticCameraLookIntent>(playerEntity), Is.False);
        }
    }
}
