using Cysharp.Threading.Tasks;
using DCL.UI;
using MVC;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DCL.ExplorePanel.Tests
{
    public class ExploreSectionModalControllerShould
    {
        private static readonly CanvasOrdering ORDERING = new (CanvasOrdering.SortingLayer.Popup, 0);

        private GameObject viewObject = null!;
        private InstantModalView view = null!;
        private IHostableSection places = null!;
        private IHostableSection events = null!;
        private ExploreSectionModalController controller = null!;

        [SetUp]
        public void SetUp()
        {
            viewObject = new GameObject(nameof(ExploreSectionModalControllerShould));
            view = viewObject.AddComponent<InstantModalView>();
            view.SectionHost = NewRect("SectionHost");
            view.CloseButton = new GameObject("Close").AddComponent<Button>();
            view.CloseButton.transform.SetParent(viewObject.transform, false);

            places = Substitute.For<IHostableSection>();
            events = Substitute.For<IHostableSection>();

            controller = new ExploreSectionModalController(() => view, new Dictionary<ExploreSections, IHostableSection>
            {
                { ExploreSections.Places, places },
                { ExploreSections.Events, events },
            });
        }

        [TearDown]
        public void TearDown()
        {
            controller.Dispose();
            Object.DestroyImmediate(viewObject);
        }

        [Test]
        public async Task HostActivateAndResetTheRequestedSectionOnShow()
        {
            // Act
            UniTask lifecycle = controller.LaunchViewLifeCycleAsync(ORDERING, new ExploreSectionModalParameter(ExploreSections.Events), CancellationToken.None);

            // Assert
            Received.InOrder(() =>
            {
                events.AttachTo(view.SectionHost);
                events.Activate();
                events.ResetAnimator();
            });

            places.DidNotReceive().AttachTo(Arg.Any<RectTransform>());
            places.DidNotReceive().Activate();

            view.CloseButton.onClick.Invoke();
            await lifecycle;
        }

        [Test]
        public async Task CloseWhenTheFrameButtonIsClicked()
        {
            // Arrange
            UniTask lifecycle = controller.LaunchViewLifeCycleAsync(ORDERING, new ExploreSectionModalParameter(ExploreSections.Places), CancellationToken.None);
            Assert.That(lifecycle.Status, Is.EqualTo(UniTaskStatus.Pending));

            // Act
            view.CloseButton.onClick.Invoke();

            // Assert
            Assert.That(lifecycle.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            await lifecycle;
        }

        [Test]
        public async Task ReturnTheSectionHomeOnClose()
        {
            // Arrange
            places.CurrentHost.Returns(view.SectionHost);
            await ShowAndRequestCloseAsync(ExploreSections.Places);

            // Act
            await controller.HideViewAsync(CancellationToken.None);

            // Assert
            Received.InOrder(() =>
            {
                places.Deactivate();
                places.AttachToHome();
            });
        }

        [Test]
        public async Task LeaveTheSectionToTheHostThatClaimedItOnClose()
        {
            // Arrange
            places.CurrentHost.Returns(NewRect("ExplorePanelSlot"));
            await ShowAndRequestCloseAsync(ExploreSections.Places);

            // Act
            await controller.HideViewAsync(CancellationToken.None);

            // Assert
            places.DidNotReceive().Deactivate();
            places.DidNotReceive().AttachToHome();
        }

        private async UniTask ShowAndRequestCloseAsync(ExploreSections section)
        {
            UniTask lifecycle = controller.LaunchViewLifeCycleAsync(ORDERING, new ExploreSectionModalParameter(section), CancellationToken.None);
            view.CloseButton.onClick.Invoke();
            await lifecycle;
        }

        private RectTransform NewRect(string name)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(viewObject.transform, false);
            return rect;
        }

        // The DOTween fades need the player loop, which the edit mode runner does not tick
        private class InstantModalView : ExploreSectionModalView
        {
            protected override UniTask PlayShowAnimationAsync(CancellationToken ct) =>
                UniTask.CompletedTask;

            protected override UniTask PlayHideAnimationAsync(CancellationToken ct) =>
                UniTask.CompletedTask;
        }
    }
}
