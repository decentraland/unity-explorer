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
                EndTime = Time.time + secondsFromNow,
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

            system!.Update(0);

            Assert.That(cameraInput.Delta, Is.EqualTo(axisValue));
            Assert.That(completion.Task.Status, Is.EqualTo(UniTaskStatus.Pending));
        }

        [Test]
        public void SuppressDeltaWhileCameraIsBlocked()
        {
            UniTaskCompletionSource<SyntheticInputDelivery> completion = AddIntent(new Vector2(5f, 0f), secondsFromNow: 100f);

            world.Add(cameraEntity, new CameraBlockerComponent());
            cameraInput.Delta = Vector2.zero;

            system!.Update(0);

            Assert.That(cameraInput.Delta, Is.EqualTo(Vector2.zero));
            Assert.That(completion.Task.Status, Is.EqualTo(UniTaskStatus.Pending), "the hold keeps running; only its effect is suppressed");
        }

        [Test]
        public void CompleteOnExpiry()
        {
            UniTaskCompletionSource<SyntheticInputDelivery> completion = AddIntent(new Vector2(5f, 0f), secondsFromNow: -1f);

            system!.Update(0);

            Assert.That(world.Has<SyntheticCameraLookIntent>(playerEntity), Is.False);
            Assert.That(completion.Task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(completion.Task.GetAwaiter().GetResult(), Is.EqualTo(SyntheticInputDelivery.Completed));
        }

        [Test]
        public void IssueLookAtIntentAndCompleteOnceTheCameraConsumedIt()
        {
            var target = new Vector3(10f, 2f, 30f);
            UniTaskCompletionSource<SyntheticInputDelivery> completion = AddIntent(Vector2.zero, secondsFromNow: 0f, lookAtTarget: target);

            system!.Update(0);

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
        }
    }
}
