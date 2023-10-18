using Arch.Core;
using DCL.CharacterCamera.Components;
using DCL.CharacterCamera.Systems;
using DCL.Input;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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
        private IUIRaycaster uiRaycaster;
        private ICursor cursor;

        [SetUp]
        public void CreateCameraSetup()
        {
            base.Setup();

            world = World.Create();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();

            var dlcInput = new DCLInput();
            dlcInput.Enable();

            entity = world.Create(new CameraComponent());
            uiRaycaster = Substitute.For<IUIRaycaster>();
            cursor = Substitute.For<ICursor>();

            system = new UpdateCursorInputSystem(world, dlcInput, this.uiRaycaster, cursor);
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
            world.Set(entity, new CameraComponent { CursorIsLocked = false });

            uiRaycaster.RaycastAll(Arg.Any<Vector2>()).Returns(new List<RaycastResult> { new () });

            Press(mouse.leftButton);

            system.Update(0);

            Assert.IsFalse(world.Get<CameraComponent>(entity).CursorIsLocked);
            cursor.DidNotReceive().Lock();
        }

        [Test]
        public void LockCursorWhenNotClickingUI()
        {
            world.Set(entity, new CameraComponent { CursorIsLocked = false });
            cursor.IsLocked().Returns(true);
            Press(mouse.leftButton);

            system.Update(0);

            Assert.IsTrue(world.Get<CameraComponent>(entity).CursorIsLocked);
            cursor.Received(1).Lock();
        }

        [Test]
        public void DontRaycastUIWhileLocked()
        {
            world.Set(entity, new CameraComponent { CursorIsLocked = true });

            Press(mouse.leftButton);

            system.Update(0);

            uiRaycaster.DidNotReceive().RaycastAll(Arg.Any<Vector2>());
        }

        [Test]
        public void UnlockCursor()
        {
            world.Set(entity, new CameraComponent { CursorIsLocked = true });

            Press(keyboard.escapeKey);

            system.Update(0);

            Assert.IsFalse(world.Get<CameraComponent>(entity).CursorIsLocked);
            cursor.Received(1).Unlock();
        }

        [Test]
        public void LockAndUnlockCursorWithTemporalLock()
        {
            //setup press
            world.Set(entity, new CameraComponent { CursorIsLocked = false });

            Press(mouse.rightButton);
            cursor.IsLocked().Returns(true);

            system.Update(0);

            Assert.IsTrue(world.Get<CameraComponent>(entity).CursorIsLocked);
            cursor.Received(1).Lock();

            // setup release
            Release(mouse.rightButton);
            cursor.IsLocked().Returns(false);

            system.Update(0);

            Assert.IsFalse(world.Get<CameraComponent>(entity).CursorIsLocked);
            cursor.Received(1).Unlock();
        }

        [Test]
        public void AutomaticallyUnlockCursorByExternalUnlock()
        {
            world.Set(entity, new CameraComponent { CursorIsLocked = true });
            cursor.IsLocked().Returns(false);

            system.Update(0);

            Assert.IsFalse(world.Get<CameraComponent>(entity).CursorIsLocked);
            cursor.Received(1).Unlock();
        }
    }
}
