using Arch.Core;
using DCL.Character.CharacterCamera.Components;
using DCL.CharacterCamera.Components;
using DCL.CharacterCamera.Systems;
using DCL.Input;
using DCL.Input.Crosshair;
using DCL.Input.Systems;
using DCL.Interaction.PlayerOriginated.Components;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace DCL.CharacterCamera.Tests
{
    [TestFixture]
    public class UpdateCursorInputSystemShould : InputTestFixture
    {
        private UpdateCursorInputSystem system;
        private World world;
        private Entity entity;
        private Keyboard keyboard;
        private Mouse mouse;
        private IEventSystem eventSystem;
        private ICursor cursor;
        private ICrosshairView crosshairView;
        private InputControl<Vector2> positionControl;
        private Entity hoverEntity;

        [SetUp]
        public void CreateCameraSetup()
        {
            base.Setup();

            world = World.Create();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();

            var dlcInput = new DCLInput();
            dlcInput.Enable();

            hoverEntity = world.Create(new HoverStateComponent());
            entity = world.Create(new CursorComponent());
            eventSystem = Substitute.For<IEventSystem>();
            cursor = Substitute.For<ICursor>();
            crosshairView = Substitute.For<ICrosshairView>();
            positionControl = mouse.GetChildControl<Vector2Control>("Position");
            Move(positionControl, new Vector2(50, 50), new Vector2(0.5f, 0.5f));

            system = new UpdateCursorInputSystem(world, dlcInput, eventSystem, cursor, crosshairView);
            system.Initialize();
        }

        [TearDown]
        public void Teardown()
        {
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
        }

        [Test]
        public void DontLockCursorWhenOverUI()
        {
            world.Set(entity, new CursorComponent { CursorState = CursorState.Free });

            eventSystem.RaycastAll(Arg.Any<Vector2>()).Returns(new List<RaycastResult> { new () });

            Press(mouse.leftButton);

            system.Update(0);

            Assert.AreEqual(CursorState.Free, world.Get<CursorComponent>(entity).CursorState);
            cursor.DidNotReceive().Lock();
        }

        [Test]
        public void LockCursorWhenNotClickingUI()
        {
            world.Set(entity, new CursorComponent { CursorState = CursorState.Free });
            cursor.IsLocked().Returns(true);
            PressAndRelease(mouse.rightButton);

            system.Update(0);

            Assert.AreEqual(CursorState.Locked, world.Get<CursorComponent>(entity).CursorState);
            cursor.Received(1).Lock();
        }

        [Test]
        public void SetCursorToInteractableWhenHoveringOverClickableUI()
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
            world.Set(hoverEntity, new HoverStateComponent
                { IsAtDistance = isAtDistance, IsHoverOver = false, HasCollider = true });

            world.Set(entity, new CursorComponent { CursorState = CursorState.Free });
            cursor.IsLocked().Returns(false);
            eventSystem.IsPointerOverGameObject().Returns(false);

            system.Update(0);

            cursor.Received(1).SetStyle(cursorStyle);
            crosshairView.Received(1).SetCursorStyle(cursorStyle);
        }

        [Test]
        public void SetCursorToNormalWhenHoveringOverNotClickableUI()
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

            Press(mouse.rightButton);

            cursor.IsLocked().Returns(false);

            system.Update(0);

            Assert.AreEqual(CursorState.Panning, world.Get<CursorComponent>(entity).CursorState);

            // setup release
            Release(mouse.rightButton);
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
    }
}
