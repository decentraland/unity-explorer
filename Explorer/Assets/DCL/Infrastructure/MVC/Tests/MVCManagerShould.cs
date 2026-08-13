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
        private List<IController> showed;
        private List<IController> closed;

        [SetUp]
        public void Setup()
        {
            windowsStackManager = Substitute.For<IWindowsStackManager>();
            windowsStackManager.PushFullscreen(Arg.Any<IController>()).Returns(new FullscreenPushInfo(new List<(IController, int)>(), new CanvasOrdering(), new UniTaskCompletionSource()));
            popupCloserView = Substitute.For<IPopupCloserView>();
            mvcManager = new MVCManager(windowsStackManager, new CancellationTokenSource(), popupCloserView);

            showed = new List<IController>();
            closed = new List<IController>();

            mvcManager.OnViewShowed += showed.Add;
            mvcManager.OnViewClosed += closed.Add;
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

        [Test]
        public async Task RaiseViewClosedWhenTheViewLifeCycleIsCancelled()
        {
            IController<ITestView, TestInputData> controller = Substitute.For<IController<ITestView, TestInputData>>();
            controller.Layer.Returns(CanvasOrdering.SortingLayer.Fullscreen);

            var viewLifeCycle = new UniTaskCompletionSource();

            controller.LaunchViewLifeCycleAsync(Arg.Any<CanvasOrdering>(), Arg.Any<TestInputData>(), Arg.Any<CancellationToken>())
                      .Returns(viewLifeCycle.Task);

            mvcManager.RegisterController(controller);

            UniTask show = mvcManager.ShowAsync(new ShowCommand<ITestView, TestInputData>());

            viewLifeCycle.TrySetCanceled();

            await show;

            // Guards the test itself: without this the assertion below would also hold for a run that
            // never entered the show path at all.
            Assert.That(showed, Is.EqualTo(new[] { controller }));

            // ShowFullScreenAsync tears the view down in a finally block and pops it off the stack, so a
            // cancelled life cycle still ends with the view hidden. Subscribers are told about every other
            // way it can end, and they have no other signal to pair with OnViewShowed.
            Assert.That(closed, Is.EqualTo(new[] { controller }));
        }

        [Test]
        public async Task RaiseViewClosedOnceWhenTheViewLifeCycleCompletes()
        {
            IController<ITestView, TestInputData> controller = Substitute.For<IController<ITestView, TestInputData>>();
            controller.Layer.Returns(CanvasOrdering.SortingLayer.Fullscreen);

            mvcManager.RegisterController(controller);

            await mvcManager.ShowAsync(new ShowCommand<ITestView, TestInputData>());

            // Exact sequences rather than counts: the show path leaves the view hidden the ordinary way
            // too, and the pairing is one to one, so a second OnViewClosed here would be a duplicate.
            Assert.That(showed, Is.EqualTo(new[] { controller }));
            Assert.That(closed, Is.EqualTo(new[] { controller }));
        }

        [Test]
        public void RaiseViewClosedWhenTheViewLifeCycleFails()
        {
            IController<ITestView, TestInputData> controller = Substitute.For<IController<ITestView, TestInputData>>();
            controller.Layer.Returns(CanvasOrdering.SortingLayer.Fullscreen);

            var viewLifeCycle = new UniTaskCompletionSource();

            controller.LaunchViewLifeCycleAsync(Arg.Any<CanvasOrdering>(), Arg.Any<TestInputData>(), Arg.Any<CancellationToken>())
                      .Returns(viewLifeCycle.Task);

            mvcManager.RegisterController(controller);

            UniTask show = mvcManager.ShowAsync(new ShowCommand<ITestView, TestInputData>());

            viewLifeCycle.TrySetException(new InvalidOperationException());

            // Only cancellation is swallowed, so this one travels out of ShowAsync — and the view is torn
            // down on the way, which is what subscribers are told about.
            Assert.ThrowsAsync<InvalidOperationException>(async () => await show);
            Assert.That(closed, Is.EqualTo(new[] { controller }));
        }

        [Test]
        public async Task RaiseNoViewEventsForAControllerThatIsNotHidden()
        {
            IController<ITestView, TestInputData> controller = Substitute.For<IController<ITestView, TestInputData>>();
            controller.Layer.Returns(CanvasOrdering.SortingLayer.Fullscreen);
            controller.State.Returns(ControllerState.ViewFocused);

            mvcManager.RegisterController(controller);

            await mvcManager.ShowAsync(new ShowCommand<ITestView, TestInputData>());

            // ShowAsync returns before showing anything in this case, so there is no session to report
            // either end of.
            Assert.That(showed, Is.Empty);
            Assert.That(closed, Is.Empty);
        }
    }

    public class TestInputData { }

    public interface ITestView : IView { }
}
