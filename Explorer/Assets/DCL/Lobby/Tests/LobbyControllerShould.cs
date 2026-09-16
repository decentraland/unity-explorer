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

            typeof(LobbyView).GetField($"<{nameof(LobbyView.JumpInButton)}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                             .SetValue(view, jumpInButton);

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
            UniTask lifeCycle = controller.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Fullscreen, 0), default(ControllerNoData), CancellationToken.None);
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Pending));
            inputBlock.Received(1).Disable(InputMapComponent.BLOCK_USER_INPUT);

            // Act
            jumpInButton.onClick.Invoke();
            controller.HideViewAsync(CancellationToken.None).Forget();

            // Assert
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            inputBlock.Received(1).Enable(InputMapComponent.BLOCK_USER_INPUT);
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
    }
}
