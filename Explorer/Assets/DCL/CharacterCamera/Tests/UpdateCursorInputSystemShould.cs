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

            system = new UpdateCursorInputSystem(world, dlcInput, this.uiRaycaster);
            system.Initialize();
        }

        [Test]
        public void DontLockCursorWhenOverUI()
        {
            world.Set(entity, new CameraComponent { CursorIsLocked = false });

            uiRaycaster.RaycastAll(Arg.Any<Vector2>()).Returns(new List<RaycastResult> { new () });

            Press(mouse.leftButton);

            system.Update(0);

            Assert.IsFalse(world.Get<CameraComponent>(entity).CursorIsLocked);
        }

        [Test]
        public void LockCursorWhenNotClickingUI()
        {
            world.Set(entity, new CameraComponent { CursorIsLocked = false });

            Press(mouse.leftButton);

            system.Update(0);

            Assert.IsTrue(world.Get<CameraComponent>(entity).CursorIsLocked);
        }

        [Test]
        public void DontRaycastUIWhileLocked1()
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
        }
    }
}
