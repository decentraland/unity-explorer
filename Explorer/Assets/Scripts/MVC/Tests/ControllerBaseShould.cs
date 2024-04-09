using Cysharp.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
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


        public void SetUp()
        {
            viewFactoryMethod = Substitute.For<ControllerBase<ITestView, TestInputData>.ViewFactoryMethod>();
            testView = Substitute.For<ITestView>();
            viewFactoryMethod().Returns(testView);

            controller = new TestController(viewFactoryMethod);
        }


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


        public void Blur()
        {
            controller.Blur();

            // State is changed
            Assert.That(controller.State, Is.EqualTo(ControllerState.ViewBlurred));

            // Modules are called
            controller.Module.Received(1).OnBlur();
        }


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

            public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

            internal TestInputData Input => inputData;

            public TestController(ViewFactoryMethod viewFactory) : base(viewFactory)
            {
                AddModule(Module = Substitute.For<IMVCControllerModule>());
            }

            protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
                CompletionSource.Task;
        }
    }
}
