using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.CharacterCamera.Components;
using DCL.CharacterCamera;
using DCL.SyntheticInput.Core;
using DCL.SyntheticInput.Systems;
using DCL.SyntheticInput.UiSimulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DCL.SyntheticInput.Tests
{
    public class UiVirtualDeviceGestureSystemShould : InputTestFixture
    {
        private World world = null!;
        private Entity playerEntity;
        private Entity cameraEntity;
        private GameObject cameraGo = null!;
        private AutomationVirtualDevices devices = null!;
        private UiVirtualDeviceGestureSystem system = null!;

        [SetUp]
        public void SetUp()
        {
            base.Setup();

            world = World.Create();
            devices = new AutomationVirtualDevices();

            cameraGo = new GameObject("gesture-test-camera");
            cameraEntity = world.Create(new CameraComponent(cameraGo.AddComponent<Camera>()), new CursorComponent { CursorState = CursorState.Free });
            playerEntity = world.Create();

            system = new UiVirtualDeviceGestureSystem(world, playerEntity, devices);
            system.Initialize();
        }

        [TearDown]
        public void DisposeWorld()
        {
            SyntheticCursorState.Reset();
            devices.Dispose();
            Object.DestroyImmediate(cameraGo);
            world.Dispose();
        }

        private UniTask<UiGestureResult> Send(UiDeviceGestureRequest request) =>
            EcsRequest.SendAsync(world, playerEntity, request, new UiGestureResult { Ok = false, FailureReason = "preempted by a newer gesture" });

        private ref CursorComponent cursor => ref world.Get<CursorComponent>(cameraEntity);

        [Test]
        public void DriveAClickThroughMovePressReleaseFrames()
        {
            var target = new Vector2(100f, 200f);
            UniTask<UiGestureResult> gesture = Send(new UiDeviceGestureRequest { Kind = UiDeviceGestureKind.Click, To = target, Button = MouseButton.Left });

            system.Update(0); // move
            InputSystem.Update();
            Assert.That(devices.Mouse.position.ReadValue(), Is.EqualTo(target));
            Assert.That(devices.Mouse.leftButton.isPressed, Is.False);
            Assert.That(gesture.Status, Is.EqualTo(UniTaskStatus.Pending));

            system.Update(0); // press
            InputSystem.Update();
            Assert.That(devices.Mouse.leftButton.isPressed, Is.True);
            Assert.That(gesture.Status, Is.EqualTo(UniTaskStatus.Pending));

            system.Update(0); // release + complete
            InputSystem.Update();
            Assert.That(devices.Mouse.leftButton.isPressed, Is.False);
            Assert.That(gesture.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(gesture.GetAwaiter().GetResult().Ok, Is.True);
            Assert.That(world.Has<UiDeviceGestureRequest>(playerEntity), Is.False);
        }

        [Test]
        public void HoldTheButtonWhileInterpolatingADrag()
        {
            var from = new Vector2(10f, 10f);
            var to = new Vector2(110f, 10f);
            UniTask<UiGestureResult> gesture = Send(new UiDeviceGestureRequest { Kind = UiDeviceGestureKind.Drag, From = from, To = to, DurationFrames = 4, Button = MouseButton.Left });

            system.Update(0); // move to start
            system.Update(0); // press
            InputSystem.Update();
            Assert.That(devices.Mouse.leftButton.isPressed, Is.True);
            Assert.That(devices.Mouse.position.ReadValue(), Is.EqualTo(from));

            for (var i = 0; i < 4; i++)
            {
                system.Update(0);
                InputSystem.Update();
                Assert.That(devices.Mouse.leftButton.isPressed, Is.True, "the button stays held through the drag");
            }

            Assert.That(devices.Mouse.position.ReadValue(), Is.EqualTo(to));

            system.Update(0); // release + complete
            InputSystem.Update();
            Assert.That(devices.Mouse.leftButton.isPressed, Is.False);
            Assert.That(gesture.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [Test]
        public void PressAndReleaseAKeyWithAHold()
        {
            UniTask<UiGestureResult> gesture = Send(new UiDeviceGestureRequest { Kind = UiDeviceGestureKind.KeyPress, Key = Key.E, DurationFrames = 2 });

            system.Update(0); // press
            InputSystem.Update();
            Assert.That(devices.Keyboard.eKey.isPressed, Is.True);

            system.Update(0); // hold 1
            system.Update(0); // hold 2
            InputSystem.Update();
            Assert.That(devices.Keyboard.eKey.isPressed, Is.True);
            Assert.That(gesture.Status, Is.EqualTo(UniTaskStatus.Pending));

            system.Update(0); // release + complete
            InputSystem.Update();
            Assert.That(devices.Keyboard.eKey.isPressed, Is.False);
            Assert.That(gesture.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [Test]
        public void RejectPointerGesturesWhileTheCursorIsCaptured()
        {
            cursor.CursorState = CursorState.Locked;

            UniTask<UiGestureResult> gesture = Send(new UiDeviceGestureRequest { Kind = UiDeviceGestureKind.Click, To = Vector2.zero, Button = MouseButton.Left });

            system.Update(0);

            Assert.That(gesture.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            UiGestureResult result = gesture.GetAwaiter().GetResult();
            Assert.That(result.Ok, Is.False);
            Assert.That(result.FailureReason, Does.Contain("cursor is locked"));
            Assert.That(world.Has<UiDeviceGestureRequest>(playerEntity), Is.False);
        }

        [Test]
        public void AllowKeyGesturesWhileTheCursorIsCaptured()
        {
            cursor.CursorState = CursorState.Locked;

            UniTask<UiGestureResult> gesture = Send(new UiDeviceGestureRequest { Kind = UiDeviceGestureKind.KeyPress, Key = Key.Space });

            system.Update(0);
            InputSystem.Update();

            Assert.That(devices.Keyboard.spaceKey.isPressed, Is.True);
            Assert.That(gesture.Status, Is.EqualTo(UniTaskStatus.Pending));
        }

        [Test]
        public void SuppressOsCursorWarpsWhileAPointerGestureRuns()
        {
            Send(new UiDeviceGestureRequest { Kind = UiDeviceGestureKind.Hover, From = Vector2.one, To = Vector2.one, DurationFrames = 10 });

            system.Update(0);

            Assert.That(SyntheticCursorState.SuppressOsWarp, Is.True);
        }

        [Test]
        public void PublishEveryQueuedPointerPositionToTheCursorSystems()
        {
            var from = new Vector2(10f, 10f);
            var to = new Vector2(50f, 10f);
            Send(new UiDeviceGestureRequest { Kind = UiDeviceGestureKind.Drag, From = from, To = to, DurationFrames = 2, Button = MouseButton.Left });

            // The cursor system reads this instead of the hardware mouse, so every phase that moves the pointer
            // must publish it — otherwise the world reticle stays behind while the UI stack follows the gesture.
            system.Update(0); // move to the start
            Assert.That(SyntheticCursorState.TryGetPointerPosition(out Vector2 published), Is.True);
            Assert.That(published, Is.EqualTo(from));

            system.Update(0); // press at the start
            SyntheticCursorState.TryGetPointerPosition(out published);
            Assert.That(published, Is.EqualTo(from));

            system.Update(0); // first drag step
            SyntheticCursorState.TryGetPointerPosition(out published);
            Assert.That(published, Is.EqualTo(Vector2.Lerp(from, to, 0.5f)));
        }

        [Test]
        public void PublishNoPointerPositionForAKeyGesture()
        {
            Send(new UiDeviceGestureRequest { Kind = UiDeviceGestureKind.KeyPress, Key = Key.E, DurationFrames = 1 });

            system.Update(0);

            Assert.That(SyntheticCursorState.TryGetPointerPosition(out _), Is.False);
        }

        [Test]
        public void FailADragThatTurnedIntoACameraPanMidGesture()
        {
            UniTask<UiGestureResult> gesture = Send(new UiDeviceGestureRequest
            {
                Kind = UiDeviceGestureKind.Drag, From = new Vector2(10f, 10f), To = new Vector2(90f, 10f), DurationFrames = 4, Button = MouseButton.Left,
            });

            system.Update(0); // move
            system.Update(0); // press

            // A held button dragged across the world is the camera-pan gesture (TemporalLock binds the left mouse
            // button), so the cursor flips to panning and the drag the caller asked for never happens.
            cursor.CursorState = CursorState.Panning;
            system.Update(0);

            Assert.That(gesture.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            UiGestureResult result = gesture.GetAwaiter().GetResult();
            Assert.That(result.Ok, Is.False, "a gesture that panned the camera instead of dragging must not report a delivery");
            Assert.That(result.FailureReason, Does.Contain("panned the camera"));
            Assert.That(world.Has<UiDeviceGestureRequest>(playerEntity), Is.False);
        }
    }
}
