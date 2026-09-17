using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterPreview;
using DCL.ExplorePanel;
using DCL.Input;
using DCL.Input.Component;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using DCL.UI;
using MVC;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Reflection;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
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
        private CharacterPreviewInputDetector avatarInputDetector = null!;
        private CharacterPreviewSettingsSO previewSettings = null!;
        private IInputBlock inputBlock = null!;
        private IMVCManager mvcManager = null!;
        private LoadingStatus loadingStatus = null!;
        private World world = null!;
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
            SetBackingField(view, nameof(LobbyView.CharacterPreviewView), CreateCharacterPreviewView());

            inputBlock = Substitute.For<IInputBlock>();
            mvcManager = Substitute.For<IMVCManager>();
            loadingStatus = new LoadingStatus();
            world = World.Create();

            // Without an own profile the avatar preview is never initialized, which keeps the rendering stack out of the test
            ISelfProfile selfProfile = Substitute.For<ISelfProfile>();
            selfProfile.ProfileAsync(Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<Profile?>(null));

            controller = new LobbyController(() => view, inputBlock, loadingStatus, mvcManager, selfProfile, new ProfileChangesBus(),
                Substitute.For<ICharacterPreviewFactory>(), new CharacterPreviewEventBus(), new LobbyAvatarSettings(), world);
        }

        [TearDown]
        public void TearDown()
        {
            controller.Dispose();
            World.Destroy(world);
            Object.DestroyImmediate(previewSettings);
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

        [Test]
        public void OpenTheBackpackWhenTheAvatarIsClicked()
        {
            // Arrange
            Launch(isStartup: true).Forget();

            // Act
            avatarInputDetector.OnPointerClick(new PointerEventData(EventSystem.current));

            // Assert
            mvcManager.Received(1).ShowAsync(Arg.Is<ShowCommand<ExplorePanelView, ExplorePanelParameter>>(c => c.InputData.Section == ExploreSections.Backpack), Arg.Any<CancellationToken>());
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

        private CharacterPreviewView CreateCharacterPreviewView()
        {
            var previewGo = new GameObject("CharacterPreviewView");
            previewGo.transform.SetParent(root.transform);
            CharacterPreviewView previewView = previewGo.AddComponent<CharacterPreviewView>();
            avatarInputDetector = previewGo.AddComponent<CharacterPreviewInputDetector>();

            previewSettings = ScriptableObject.CreateInstance<CharacterPreviewSettingsSO>();
            SetBackingField(previewSettings, nameof(CharacterPreviewSettingsSO.cursorSettings), Array.Empty<CharacterPreviewInputCursorSetting>());

            SetBackingField(previewView, nameof(CharacterPreviewView.CharacterPreviewInputDetector), avatarInputDetector);
            SetBackingField(previewView, nameof(CharacterPreviewView.CharacterPreviewCursorContainer), previewGo.AddComponent<CharacterPreviewCursorContainer>());
            SetBackingField(previewView, nameof(CharacterPreviewView.CharacterPreviewSettingsSo), previewSettings);

            return previewView;
        }

        private static void SetBackingField(object target, string propertyName, object value) =>
            target.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                  .SetValue(target, value);
    }
}
