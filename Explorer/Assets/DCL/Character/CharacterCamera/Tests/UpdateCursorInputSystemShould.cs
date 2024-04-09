using Arch.Core;
using DCL.CharacterCamera.Components;
using DCL.CharacterCamera.Systems;
using DCL.Input;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace DCL.CharacterCamera.Tests
{

    public class UpdateCursorInputSystemShould : InputTestFixture
    {

        public void CreateCameraSetup()
        {
            base.Setup();

            world = World.Create();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();

            var dlcInput = new DCLInput();
            dlcInput.Enable();

            entity = world.Create(new CursorComponent());
            eventSystem = Substitute.For<IEventSystem>();
            cursor = Substitute.For<ICursor>();

            system = new UpdateCursorInputSystem(world, dlcInput, eventSystem, cursor);
            system.Initialize();
        }


        public void Teardown()
        {
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
        }

        private UpdateCursorInputSystem system;
        private World world;
        private Entity entity;
        private Keyboard keyboard;
        private Mouse mouse;
        private IEventSystem eventSystem;
        private ICursor cursor;


        public void DontLockCursorWhenOverUI()
        {
            world.Set(entity, new CursorComponent { CursorIsLocked = false });

            eventSystem.RaycastAll(Arg.Any<Vector2>()).Returns(new List<RaycastResult> { new () });

            Press(mouse.leftButton);

            system.Update(0);

            Assert.IsFalse(world.Get<CursorComponent>(entity).CursorIsLocked);
            cursor.DidNotReceive().Lock();
        }


        public void LockCursorWhenNotClickingUI()
        {
            world.Set(entity, new CursorComponent { CursorIsLocked = false });
            cursor.IsLocked().Returns(true);
            Press(mouse.leftButton);

            system.Update(0);

            Assert.IsTrue(world.Get<CursorComponent>(entity).CursorIsLocked);
            cursor.Received(1).Lock();
        }


        public void DontRaycastUIWhileLocked()
        {
            world.Set(entity, new CursorComponent { CursorIsLocked = true });

            Press(mouse.leftButton);

            system.Update(0);

            eventSystem.DidNotReceive().RaycastAll(Arg.Any<Vector2>());
        }


        public void UnlockCursor()
        {
            world.Set(entity, new CursorComponent { CursorIsLocked = true });

            Press(keyboard.escapeKey);

            system.Update(0);

            Assert.IsFalse(world.Get<CursorComponent>(entity).CursorIsLocked);
            cursor.Received(1).Unlock();
        }


        public void LockAndUnlockCursorWithTemporalLock()
        {
            //setup press
            world.Set(entity, new CursorComponent { CursorIsLocked = false });

            Press(mouse.rightButton);
            cursor.IsLocked().Returns(true);

            system.Update(0);

            Assert.IsTrue(world.Get<CursorComponent>(entity).CursorIsLocked);
            cursor.Received(1).Lock();

            // setup release
            Release(mouse.rightButton);
            cursor.IsLocked().Returns(false);

            system.Update(0);

            Assert.IsFalse(world.Get<CursorComponent>(entity).CursorIsLocked);
            cursor.Received(1).Unlock();
        }


        public void AutomaticallyUnlockCursorByExternalUnlock()
        {
            world.Set(entity, new CursorComponent { CursorIsLocked = true });
            cursor.IsLocked().Returns(false);

            system.Update(0);

            Assert.IsFalse(world.Get<CursorComponent>(entity).CursorIsLocked);
            cursor.Received(1).Unlock();
        }
    }
}
