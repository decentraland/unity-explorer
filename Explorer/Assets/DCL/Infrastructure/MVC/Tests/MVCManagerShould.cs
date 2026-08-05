using Cysharp.Threading.Tasks;
using MVC.PopupsController.PopupCloser;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MVC.Tests
{
    public class MVCManagerShould
    {
        private IWindowsStackManager windowsStackManager;
        private MVCManager mvcManager;
        private IPopupCloserView popupCloserView;

        [SetUp]
        public void Setup()
        {
            windowsStackManager = Substitute.For<IWindowsStackManager>();
            windowsStackManager.PushFullscreen(Arg.Any<IController>()).Returns(new FullscreenPushInfo(new List<(IController, int)>(), new CanvasOrdering(), new UniTaskCompletionSource()));
            popupCloserView = Substitute.For<IPopupCloserView>();
            mvcManager = new MVCManager(windowsStackManager, new CancellationTokenSource(), popupCloserView);
        }

        [Test]
        public void RegisterController()
        {
            // Arrange
            IController<ITestView, TestInputData> controller = Substitute.For<IController<ITestView, TestInputData>>();

            // Act
            mvcManager.RegisterController(controller);

            // Assert
            Assert.AreEqual(1, mvcManager.Controllers.Count);
        }

        [Test]
        public void RegisterControllerThrowsExceptionWhenSameControllerIsAddedTwice()
        {
            // Arrange
            IController<ITestView, TestInputData> controller = Substitute.For<IController<ITestView, TestInputData>>();

            // Act
            mvcManager.RegisterController(controller);

            // Assert
            Assert.Throws<ArgumentException>(() => mvcManager.RegisterController(controller));
        }

        [Test]
        public async Task ReshowFullscreenControllerWhenAlreadyVisible()
        {
            // Arrange
            IController<ITestView, TestInputData> controller = Substitute.For<IController<ITestView, TestInputData>, IReshowController<TestInputData>>();
            controller.Layer.Returns(CanvasOrdering.SortingLayer.Fullscreen);
            controller.State.Returns(ControllerState.ViewFocused);
            windowsStackManager.CurrentFullscreenController.Returns(controller);

            IController popup = Substitute.For<IController>();
            var popups = new List<(IController, int)> { (popup, 2) };
            windowsStackManager.GetNonPersistentControllersInfo().Returns(new NonPersistentControllersInfo(popups, controller, null));

            mvcManager.RegisterController(controller);
            var input = new TestInputData();

            // Act
            await mvcManager.ShowAsync(new ShowCommand<ITestView, TestInputData>(input));

            // Assert
            ((IReshowController<TestInputData>)controller).Received(1).OnReshowWhileVisible(input);
            windowsStackManager.Received(1).PopPopup(popup);
            await popupCloserView.Received().HideAsync(Arg.Any<CancellationToken>());
            windowsStackManager.DidNotReceive().PushFullscreen(Arg.Any<IController>());
            Assert.AreEqual(0, popups.Count);
        }

        [Test]
        public async Task SilentlyReturnWhenVisibleControllerIsNotReshowable()
        {
            // Arrange
            IController<ITestView, TestInputData> controller = Substitute.For<IController<ITestView, TestInputData>>();
            controller.Layer.Returns(CanvasOrdering.SortingLayer.Fullscreen);
            controller.State.Returns(ControllerState.ViewFocused);
            windowsStackManager.CurrentFullscreenController.Returns(controller);

            mvcManager.RegisterController(controller);

            // Act
            await mvcManager.ShowAsync(new ShowCommand<ITestView, TestInputData>(new TestInputData()));

            // Assert
            windowsStackManager.DidNotReceive().PopPopup(Arg.Any<IController>());
            windowsStackManager.DidNotReceive().PushFullscreen(Arg.Any<IController>());
            await controller.DidNotReceive().LaunchViewLifeCycleAsync(Arg.Any<CanvasOrdering>(), Arg.Any<TestInputData>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task NotReshowWhenControllerIsNotCurrentFullscreen()
        {
            // Arrange
            IController<ITestView, TestInputData> controller = Substitute.For<IController<ITestView, TestInputData>, IReshowController<TestInputData>>();
            controller.Layer.Returns(CanvasOrdering.SortingLayer.Fullscreen);
            controller.State.Returns(ControllerState.ViewFocused);
            windowsStackManager.CurrentFullscreenController.Returns(Substitute.For<IController>());

            mvcManager.RegisterController(controller);

            // Act
            await mvcManager.ShowAsync(new ShowCommand<ITestView, TestInputData>(new TestInputData()));

            // Assert
            ((IReshowController<TestInputData>)controller).DidNotReceive().OnReshowWhileVisible(Arg.Any<TestInputData>());
            windowsStackManager.DidNotReceive().PopPopup(Arg.Any<IController>());
        }

        [Test]
        [TestCase(ControllerState.ViewShowing)]
        [TestCase(ControllerState.ViewHiding)]
        public async Task NotReshowWhileTransitioning(ControllerState state)
        {
            // Arrange
            IController<ITestView, TestInputData> controller = Substitute.For<IController<ITestView, TestInputData>, IReshowController<TestInputData>>();
            controller.Layer.Returns(CanvasOrdering.SortingLayer.Fullscreen);
            controller.State.Returns(state);
            windowsStackManager.CurrentFullscreenController.Returns(controller);

            mvcManager.RegisterController(controller);

            // Act
            await mvcManager.ShowAsync(new ShowCommand<ITestView, TestInputData>(new TestInputData()));

            // Assert
            ((IReshowController<TestInputData>)controller).DidNotReceive().OnReshowWhileVisible(Arg.Any<TestInputData>());
            windowsStackManager.DidNotReceive().PopPopup(Arg.Any<IController>());
        }

        [Test]
        public async Task NotRaiseShowEventsOnReshow()
        {
            // Arrange
            IController<ITestView, TestInputData> controller = Substitute.For<IController<ITestView, TestInputData>, IReshowController<TestInputData>>();
            controller.Layer.Returns(CanvasOrdering.SortingLayer.Fullscreen);
            controller.State.Returns(ControllerState.ViewFocused);
            windowsStackManager.CurrentFullscreenController.Returns(controller);
            windowsStackManager.GetNonPersistentControllersInfo().Returns(new NonPersistentControllersInfo(new List<(IController, int)>(), controller, null));

            mvcManager.RegisterController(controller);

            var showedRaised = false;
            var closedRaised = false;
            mvcManager.OnViewShowed += _ => showedRaised = true;
            mvcManager.OnViewClosed += _ => closedRaised = true;

            // Act
            await mvcManager.ShowAsync(new ShowCommand<ITestView, TestInputData>(new TestInputData()));

            // Assert
            ((IReshowController<TestInputData>)controller).Received(1).OnReshowWhileVisible(Arg.Any<TestInputData>());
            Assert.IsFalse(showedRaised);
            Assert.IsFalse(closedRaised);
        }

        [Test]
        [TestCase(CanvasOrdering.SortingLayer.Popup)]
        [TestCase(CanvasOrdering.SortingLayer.Fullscreen)]
        [TestCase(CanvasOrdering.SortingLayer.Overlay)]
        [TestCase(CanvasOrdering.SortingLayer.Persistent)]
        public async Task Show(CanvasOrdering.SortingLayer layer)
        {
            IController<ITestView, TestInputData> controller = Substitute.For<IController<ITestView, TestInputData>>();
            controller.Layer.Returns(layer);

            mvcManager.RegisterController(controller);

            await mvcManager.ShowAsync(new ShowCommand<ITestView, TestInputData>());

            switch (layer)
            {
                case CanvasOrdering.SortingLayer.Popup:
                    await popupCloserView.Received().ShowAsync(Arg.Any<CancellationToken>());
                    windowsStackManager.Received().PushPopup(controller);
                    break;
                case CanvasOrdering.SortingLayer.Fullscreen:
                    await popupCloserView.DidNotReceive().ShowAsync(Arg.Any<CancellationToken>());
                    windowsStackManager.Received().PushFullscreen(controller);
                    break;
                case CanvasOrdering.SortingLayer.Overlay:
                    await popupCloserView.DidNotReceive().ShowAsync(Arg.Any<CancellationToken>());
                    windowsStackManager.Received().PushOverlay(controller);
                    break;
                case CanvasOrdering.SortingLayer.Persistent:
                    await popupCloserView.DidNotReceive().ShowAsync(Arg.Any<CancellationToken>());
                    windowsStackManager.Received().PushPersistent(controller);
                    break;
            }
        }
    }

    public class TestInputData { }

    public interface ITestView : IView { }
}
