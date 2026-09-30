using Cysharp.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MVC.Tests
{
    public class ControllerBaseShould
    {
        private TestController controller;
        private ControllerBase<ITestView, TestInputData>.ViewFactoryMethod viewFactoryMethod;
        private ITestView testView;

        private IMVCControllerModule module;

        [SetUp]
        public void SetUp()
        {
            viewFactoryMethod = Substitute.For<ControllerBase<ITestView, TestInputData>.ViewFactoryMethod>();
            testView = Substitute.For<ITestView>();
            viewFactoryMethod().Returns(testView);

            controller = new TestController(viewFactoryMethod);
        }

        [Test]
        public async Task LaunchViewLifeCycle()
        {
            var canvasOrdering = new CanvasOrdering(CanvasOrdering.SortingLayer.Fullscreen, 100);
            var input = new TestInputData { Value = 123 };

            // Fire the closing intent
            controller.CompletionSource.TrySetResult();
            await controller.LaunchViewLifeCycleAsync(canvasOrdering, input, CancellationToken.None);

            // View is created
            viewFactoryMethod.Received(1).Invoke();

            // Draw Order is set
            testView.Received(1).SetDrawOrder(canvasOrdering);

            // Show Async is called
            await testView.Received(1).ShowAsync(CancellationToken.None);

            // State is changed
            Assert.That(controller.State, Is.EqualTo(ControllerState.ViewFocused));

            // Call is propagated to modules
            controller.Module.Received(1).OnViewShow();

            // Input is set
            Assert.That(controller.Input, Is.EqualTo(input));
        }

        [Test]
        public async Task HideView()
        {
            // Show first

            var canvasOrdering = new CanvasOrdering(CanvasOrdering.SortingLayer.Fullscreen, 100);

            controller.CompletionSource.TrySetResult();
            await controller.LaunchViewLifeCycleAsync(canvasOrdering, new TestInputData(), CancellationToken.None);

            // Hide
            var icontroller = (IController)controller;

            await icontroller.HideViewAsync(CancellationToken.None);

            // State is changed
            Assert.That(controller.State, Is.EqualTo(ControllerState.ViewHidden));

            // Modules are called
            controller.Module.Received(1).OnViewHide();

            // View is hidden
            await testView.Received(1).HideAsync(CancellationToken.None);
        }

        [Test]
        public async Task RunCloseAfterShowWhenHideIsRequestedWhileShowing()
        {
            // Arrange
            var showAnimation = new UniTaskCompletionSource();
            testView.ShowAsync(Arg.Any<CancellationToken>()).Returns(showAnimation.Task);

            UniTask lifeCycle = controller.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Fullscreen, 100), new TestInputData(), CancellationToken.None);

            // Act
            UniTask hide = ((IController)controller).HideViewAsync(CancellationToken.None);

            // Assert
            // The hide must not tear down a view whose show is still in flight
            Assert.That(hide.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(controller.State, Is.EqualTo(ControllerState.ViewShowing));
            Assert.That(controller.Callbacks, Is.Empty);

            showAnimation.TrySetResult();
            await hide;

            Assert.That(controller.Callbacks, Is.EqualTo(new[] { "Show", "Close" }));
            Assert.That(controller.State, Is.EqualTo(ControllerState.ViewHidden));
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Pending));
            await testView.Received(1).HideAsync(CancellationToken.None);
        }

        [Test]
        public void Blur()
        {
            controller.Blur();

            // State is changed
            Assert.That(controller.State, Is.EqualTo(ControllerState.ViewBlurred));

            // Modules are called
            controller.Module.Received(1).OnBlur();
        }

        [Test]
        public void Focus()
        {
            controller.Focus();

            // State is changed
            Assert.That(controller.State, Is.EqualTo(ControllerState.ViewFocused));

            // Modules are called
            controller.Module.Received(1).OnFocus();
        }

        public interface ITestView : IView { }

        public struct TestInputData
        {
            public int Value;
        }

        public class TestController : ControllerBase<ITestView, TestInputData>
        {
            public readonly UniTaskCompletionSource CompletionSource = new ();

            public readonly IMVCControllerModule Module;

            public readonly List<string> Callbacks = new ();

            public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

            internal TestInputData Input => inputData;

            public TestController(ViewFactoryMethod viewFactory) : base(viewFactory)
            {
                AddModule(Module = Substitute.For<IMVCControllerModule>());
            }

            protected override void OnViewShow() =>
                Callbacks.Add("Show");

            protected override void OnViewClose() =>
                Callbacks.Add("Close");

            protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
                CompletionSource.Task;
        }
    }
}
