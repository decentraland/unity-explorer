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
        private RectTransform imageRT;
        private GameObject ui;
        private IEventSystem eventSystem;

        [SetUp]
        public void CreateCameraSetup()
        {
            base.Setup();

            world = World.Create();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();

            var dlcInput = new DCLInput();
            dlcInput.Enable();

            entity = world.Create( new CameraInput());
            eventSystem = Substitute.For<IEventSystem>();
            eventSystem.GetPointerEventData().Returns(new PointerEventData(null));

            system = new UpdateCursorInputSystem(world, dlcInput, this.eventSystem);
            system.Initialize();
        }

        [Test]
        public void DontLockCursorWhenOverUI()
        {
            world.Set(entity, new CameraInput { IsCursorLocked = false });

            eventSystem.When(x => x.RaycastAll(Arg.Any<PointerEventData>(),Arg.Any<List<RaycastResult>>() ))
                       .Do(callInfo =>
                        {
                            var list = callInfo.ArgAt<List<RaycastResult>>(1);
                            list.Add(new RaycastResult());
                        });

            Press(mouse.leftButton);

            system.Update(0);

            Assert.IsFalse(world.Get<CameraInput>(entity).IsCursorLocked);
        }

        [Test]
        public void LockCursorWhenNotClickingUI()
        {
            world.Set(entity, new CameraInput { IsCursorLocked = false });

            Press(mouse.leftButton);

            system.Update(0);

            Assert.IsTrue(world.Get<CameraInput>(entity).IsCursorLocked);
        }

        [Test]
        public void DontRaycastUIWhileLocked1()
        {
            world.Set(entity, new CameraInput { IsCursorLocked = true });

            Press(mouse.leftButton);

            system.Update(0);

            eventSystem.DidNotReceive().RaycastAll(Arg.Any<PointerEventData>(), Arg.Any<List<RaycastResult>>());
        }

        [Test]
        public void UnlockCursor()
        {
            world.Set(entity, new CameraInput { IsCursorLocked = true });

            Press(keyboard.escapeKey);

            system.Update(0);

            Assert.IsFalse(world.Get<CameraInput>(entity).IsCursorLocked);
        }
    }


}
