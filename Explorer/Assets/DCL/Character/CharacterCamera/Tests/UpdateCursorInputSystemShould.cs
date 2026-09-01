using Arch.Core;
using DCL.Character.CharacterCamera.Components;
using DCL.CharacterCamera;
using DCL.Input;
using DCL.Input.Crosshair;
using DCL.Input.Systems;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.SyntheticInput.UiSimulation;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace DCL.Character.CharacterCamera.Tests
{
    [TestFixture]
    public class UpdateCursorInputSystemShould : InputTestFixture
    {
        private UpdateCursorInputSystem system = null!;
        private World world = null!;
        private Entity entity;
        private Keyboard keyboard = null!;
        private Mouse mouse = null!;
        private IEventSystem eventSystem = null!;
        private ICursor cursor = null!;
        private ICrosshairView crosshairView = null!;
        private InputControl<Vector2> positionControl = null!;
        private Entity hoverEntity;

        [SetUp]
        public void CreateCameraSetup()
        {
            base.Setup();

            world = World.Create();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();

            DCLInput.Instance.Enable();

            hoverEntity = world.Create(new HoverStateComponent());
            entity = world.Create(new CursorComponent(), new ExposedCameraData());
            eventSystem = Substitute.For<IEventSystem>();
            cursor = Substitute.For<ICursor>();
            crosshairView = Substitute.For<ICrosshairView>();
            positionControl = mouse.GetChildControl<Vector2Control>("Position");
            Move(positionControl, new Vector2(50, 50), new Vector2(0.5f, 0.5f));

            system = new UpdateCursorInputSystem(world, eventSystem, cursor, crosshairView);
            system.Initialize();
        }

        [TearDown]
        public override void TearDown()
        {
            // Editor tests share frames, so an asserted synthetic pointer would steer the next fixture's cursor.
            SyntheticCursorState.Reset();
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            base.TearDown();
        }

        [Test]
        public void FollowTheSyntheticPointerWhileAnAutomationGestureRuns()
        {
            world.Set(entity, new CursorComponent { CursorState = CursorState.Free });
            eventSystem.RaycastAll(Arg.Any<Vector2>()).Returns(new List<RaycastResult>());

            var gesturePoint = new Vector2(320f, 240f);
            SyntheticCursorState.AssertPointerPositionThisFrame(gesturePoint);

            system.Update(0);

            // The automation mouse is a device of its own, so the hardware position (50, 50) must lose to it:
            // everything downstream of this value — the UI raycast, the cursor style, and the world reticle ray
            // PlayerOriginatedRaycastSystem builds from CursorComponent.Position — has to describe the same pointer.
            Assert.AreEqual(gesturePoint, world.Get<CursorComponent>(entity).Position);
            eventSystem.Received().RaycastAll(gesturePoint);
        }

        [Test]
        public void FallBackToTheHardwareMouseWhenNoGestureIsRunning()
        {
            world.Set(entity, new CursorComponent { CursorState = CursorState.Free });
            eventSystem.RaycastAll(Arg.Any<Vector2>()).Returns(new List<RaycastResult>());

            system.Update(0);

            Assert.AreEqual(new Vector2(50, 50), world.Get<CursorComponent>(entity).Position);
        }

        [Test]
        public void DontLockCursorWhenOverUi()
        {
            world.Set(entity, new CursorComponent { CursorState = CursorState.Free });

            eventSystem.RaycastAll(Arg.Any<Vector2>()).Returns(new List<RaycastResult> { new () });

            Press(mouse.leftButton);

            system.Update(0);

            Assert.AreEqual(CursorState.Free, world.Get<CursorComponent>(entity).CursorState);
            cursor.DidNotReceive().Lock();
        }

        [Test]
        public void LockCursorWhenNotClickingUi()
        {
            world.Set(entity, new CursorComponent { CursorState = CursorState.Free });
            cursor.IsLocked().Returns(true);
            PressAndRelease(mouse.rightButton);

            system.Update(0);

            Assert.AreEqual(CursorState.Locked, world.Get<CursorComponent>(entity).CursorState);
            cursor.Received(1).Lock();
        }

        [Test]
        public void SetCursorToInteractableWhenHoveringOverClickableUi()
        {
            world.Set(entity, new CursorComponent { CursorState = CursorState.Free });
            cursor.IsLocked().Returns(false);
            eventSystem.IsPointerOverGameObject().Returns(true);

            var temporalGameObject = new GameObject("TEMP_GO");
            temporalGameObject.AddComponent<Button>();

            eventSystem.RaycastAll(Arg.Any<Vector2>()).Returns(new List<RaycastResult> { new () { gameObject = temporalGameObject } });

            system.Update(0);

            cursor.Received(1).SetStyle(CursorStyle.Interaction);
            crosshairView.Received(1).SetCursorStyle(CursorStyle.Interaction);

            Object.DestroyImmediate(temporalGameObject);
        }

        [TestCase(CursorStyle.Interaction, true)]
        [TestCase(CursorStyle.Normal, false)]
        public void ChangeCursorStyleWhenHoveringOverSDKInteractable(CursorStyle cursorStyle, bool isAtDistance)
        {
            world.Set(hoverEntity, new HoverStateComponent(isAtDistance, null, true, true));

            world.Set(entity, new CursorComponent { CursorState = CursorState.Free });
            cursor.IsLocked().Returns(false);
            eventSystem.IsPointerOverGameObject().Returns(false);

            system.Update(0);

            cursor.Received(1).SetStyle(cursorStyle);
            crosshairView.Received(1).SetCursorStyle(cursorStyle);
        }

        [Test]
        public void SetCursorToNormalWhenHoveringOverNotClickableUi()
        {
            world.Set(entity, new CursorComponent { CursorState = CursorState.Free });
            cursor.IsLocked().Returns(false);
            eventSystem.IsPointerOverGameObject().Returns(true);

            var temporalGameObject = new GameObject("TEMP_GO");
            temporalGameObject.AddComponent<Image>();

            eventSystem.RaycastAll(Arg.Any<Vector2>()).Returns(new List<RaycastResult> { new () { gameObject = temporalGameObject } });

            system.Update(0);

            cursor.Received(1).SetStyle(CursorStyle.Normal);
            crosshairView.Received(1).SetCursorStyle(CursorStyle.Normal);

            Object.DestroyImmediate(temporalGameObject);
        }

        [Test]
        public void UnlockCursor()
        {
            world.Set(entity, new CursorComponent { CursorState = CursorState.Locked });

            Press(keyboard.escapeKey);

            system.Update(0);

            Assert.AreEqual(CursorState.Free, world.Get<CursorComponent>(entity).CursorState);
            cursor.Received(1).Unlock();
        }

        [Test]
        public void AllowCameraMovementWithTemporalLock()
        {
            //setup press
            world.Set(entity, new CursorComponent { CursorState = CursorState.Free, PositionIsDirty = true });

            Press(mouse.leftButton);

            cursor.IsLocked().Returns(false);

            system.Update(0);

            Assert.AreEqual(CursorState.Panning, world.Get<CursorComponent>(entity).CursorState);

            // setup release
            Release(mouse.leftButton);
            cursor.IsLocked().Returns(false);

            system.Update(0);

            Assert.AreEqual(CursorState.Free, world.Get<CursorComponent>(entity).CursorState);
        }

        [Test]
        public void AutomaticallyUnlockCursorByExternalUnlock()
        {
            world.Set(entity, new CursorComponent { CursorState = CursorState.Locked });
            cursor.IsLocked().Returns(false);

            system.Update(0);

            Assert.AreEqual(CursorState.Free, world.Get<CursorComponent>(entity).CursorState);
            cursor.Received(1).Unlock();
        }

        [Test]
        public void DisallowPanningWhenInSDKCameraMode()
        {
            // Arrange
            world.Set(entity, new CursorComponent { CursorState = CursorState.Free, PositionIsDirty = true });
            ref var cameraData = ref world.Get<ExposedCameraData>(entity);
            cameraData.CameraMode = CameraMode.SdkCamera;

            Press(mouse.leftButton); // Temporal lock

            cursor.IsLocked().Returns(false);
            eventSystem.IsPointerOverGameObject().Returns(false);

            // Act
            system.Update(0);

            // Assert
            Assert.AreEqual(CursorState.Free, world.Get<CursorComponent>(entity).CursorState);
        }
    }
}
