using Cysharp.Threading.Tasks;
using DCL.Input;
using DCL.Input.Component;
using DCL.RealmNavigation;
using MVC;
using NSubstitute;
using NUnit.Framework;
using System.Reflection;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyControllerShould
    {
        private GameObject root = null!;
        private Button jumpInButton = null!;
        private Button closeButton = null!;
        private IInputBlock inputBlock = null!;
        private LoadingStatus loadingStatus = null!;
        private LobbyController controller = null!;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject(nameof(LobbyControllerShould));
            LobbyView view = root.AddComponent<LobbyView>();

            var buttonGo = new GameObject("JumpInButton");
            buttonGo.transform.SetParent(root.transform);
            jumpInButton = buttonGo.AddComponent<Button>();

            var closeButtonGo = new GameObject("CloseButton");
            closeButtonGo.transform.SetParent(root.transform);
            closeButton = closeButtonGo.AddComponent<Button>();

            SetBackingField(view, nameof(LobbyView.JumpInButton), jumpInButton);
            SetBackingField(view, nameof(LobbyView.CloseButton), closeButton);

            inputBlock = Substitute.For<IInputBlock>();
            loadingStatus = new LoadingStatus();
            controller = new LobbyController(() => view, inputBlock, loadingStatus);
        }

        [TearDown]
        public void TearDown()
        {
            controller.Dispose();
            Object.DestroyImmediate(root);
        }

        [Test]
        public void CloseOnJumpInAndToggleInputBlock()
        {
            // Arrange
            UniTask lifeCycle = Launch(isStartup: true);
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Pending));
            inputBlock.Received(1).Disable(InputMapComponent.BLOCK_USER_INPUT);

            // Act
            jumpInButton.onClick.Invoke();
            controller.HideViewAsync(CancellationToken.None).Forget();

            // Assert
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            inputBlock.Received(1).Enable(InputMapComponent.BLOCK_USER_INPUT);
        }

        [Test]
        public void CloseOnCloseButton()
        {
            // Arrange
            UniTask lifeCycle = Launch(isStartup: false);
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Pending));

            // Act
            closeButton.onClick.Invoke();

            // Assert
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void ShowCloseButtonOnlyWhenNotStartup(bool isStartup, bool closeButtonVisible)
        {
            // Act
            Launch(isStartup);

            // Assert
            Assert.That(closeButton.gameObject.activeSelf, Is.EqualTo(closeButtonVisible));
        }

        [TestCase(LoadingStatus.LoadingStage.Init, false)]
        [TestCase(LoadingStatus.LoadingStage.AuthenticationScreenShowing, false)]
        [TestCase(LoadingStatus.LoadingStage.PlayerTeleporting, false)]
        [TestCase(LoadingStatus.LoadingStage.Completed, true)]
        public void BeClosableByEscapeOnlyOnceTheWorldIsLoaded(LoadingStatus.LoadingStage stage, bool expected)
        {
            // Act
            loadingStatus.SetCurrentStage(stage);

            // Assert
            Assert.That(controller.CanBeClosedByEscape, Is.EqualTo(expected));
        }

        private UniTask Launch(bool isStartup) =>
            controller.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Fullscreen, 0), new LobbyParameter(isStartup), CancellationToken.None);

        private static void SetBackingField(LobbyView view, string propertyName, Button button) =>
            typeof(LobbyView).GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                             .SetValue(view, button);
    }
}
