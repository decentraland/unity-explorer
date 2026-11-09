using Arch.Core;
using DCL.CharacterCamera;
using DCL.InWorldCamera;
using DCL.UI;
using DCL.Utilities;
using MVC;
using NSubstitute;
using NUnit.Framework;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DCL.Input.Systems.Tests
{
    [TestFixture]
    public class UpdateShowHideUIInputSystemShould : InputTestFixture
    {
        private UpdateShowHideUIInputSystem system = null!;
        private World world = null!;
        private Entity camera;
        private Keyboard keyboard = null!;
        private IMVCManager mvcManager = null!;
        private ReactiveProperty<bool> currentSceneUiOnly = null!;
        private GameObject toastGameObject = null!;

        [SetUp]
        public void CreateSystem()
        {
            base.Setup();

            world = World.Create();
            camera = world.Create(new CameraComponent());
            keyboard = InputSystem.AddDevice<Keyboard>();

            DCLInput.Instance.Enable();

            mvcManager = Substitute.For<IMVCManager>();
            currentSceneUiOnly = new ReactiveProperty<bool>(false);

            toastGameObject = new GameObject("Toast");
            var toast = toastGameObject.AddComponent<WarningNotificationView>();

            // The toast writes its text on every state change, so it needs a label; with no CanvasGroup the fades are skipped
            typeof(WarningNotificationView)
               .GetField($"<{nameof(WarningNotificationView.Text)}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
               .SetValue(toast, toastGameObject.AddComponent<TextMeshProUGUI>());

            system = new UpdateShowHideUIInputSystem(world, mvcManager, toast, currentSceneUiOnly);
            system.Initialize();
        }

        [TearDown]
        public override void TearDown()
        {
            InputSystem.RemoveDevice(keyboard);
            Object.DestroyImmediate(toastGameObject);
            world.Dispose();
            base.TearDown();
        }

        [Test]
        public void CycleExplorerHiddenAllHiddenAllVisibleOnU()
        {
            // Act
            PressU();

            // Assert
            Assert.IsTrue(currentSceneUiOnly.Value);
            mvcManager.Received(1).SetAllViewsCanvasActive(false);

            // Act
            PressU();

            // Assert
            Assert.IsFalse(currentSceneUiOnly.Value);
            mvcManager.Received(2).SetAllViewsCanvasActive(false);

            // Act
            PressU();

            // Assert
            Assert.IsFalse(currentSceneUiOnly.Value);
            mvcManager.Received(1).SetAllViewsCanvasActive(true);
        }

        [Test]
        public void HideEverythingOnToggleUIRequestThenShowEverythingOnU()
        {
            // Arrange
            PressU();
            world.Add(camera, new ToggleUIRequest { Enable = false });

            // Act
            system.Update(0);

            // Assert
            Assert.IsFalse(currentSceneUiOnly.Value);
            mvcManager.Received(1).SetAllViewsCanvasActive(Arg.Any<IController>(), false);
            Assert.IsFalse(world.Has<ToggleUIRequest>(camera));

            // Act
            PressU();

            // Assert
            mvcManager.Received(1).SetAllViewsCanvasActive(true);
        }

        [Test]
        public void ShowEverythingOnToggleUIRequestFromExplorerHidden()
        {
            // Arrange
            PressU();
            world.Add(camera, new ToggleUIRequest { Enable = true });

            // Act
            system.Update(0);

            // Assert
            Assert.IsFalse(currentSceneUiOnly.Value);
            mvcManager.Received(1).SetAllViewsCanvasActive(Arg.Any<IController>(), true);

            // Act
            PressU();

            // Assert
            Assert.IsTrue(currentSceneUiOnly.Value);
        }

        private void PressU()
        {
            Press(keyboard.uKey);
            system.Update(0);
            Release(keyboard.uKey);
        }
    }
}
