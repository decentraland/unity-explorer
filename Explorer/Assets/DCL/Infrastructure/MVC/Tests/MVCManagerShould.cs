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
        [TestCase(CanvasOrdering.SortingLayer.POPUP)]
        [TestCase(CanvasOrdering.SortingLayer.FULLSCREEN)]
        [TestCase(CanvasOrdering.SortingLayer.OVERLAY)]
        [TestCase(CanvasOrdering.SortingLayer.PERSISTENT)]
        public async Task Show(CanvasOrdering.SortingLayer layer)
        {
            IController<ITestView, TestInputData> controller = Substitute.For<IController<ITestView, TestInputData>>();
            controller.Layer.Returns(layer);

            mvcManager.RegisterController(controller);

            await mvcManager.ShowAsync(new ShowCommand<ITestView, TestInputData>());

            switch (layer)
            {
                case CanvasOrdering.SortingLayer.POPUP:
                    await popupCloserView.Received().ShowAsync(Arg.Any<CancellationToken>());
                    windowsStackManager.Received().PushPopup(controller);
                    break;
                case CanvasOrdering.SortingLayer.FULLSCREEN:
                    await popupCloserView.DidNotReceive().ShowAsync(Arg.Any<CancellationToken>());
                    windowsStackManager.Received().PushFullscreen(controller);
                    break;
                case CanvasOrdering.SortingLayer.OVERLAY:
                    await popupCloserView.DidNotReceive().ShowAsync(Arg.Any<CancellationToken>());
                    windowsStackManager.Received().PushOverlay(controller);
                    break;
                case CanvasOrdering.SortingLayer.PERSISTENT:
                    await popupCloserView.DidNotReceive().ShowAsync(Arg.Any<CancellationToken>());
                    windowsStackManager.Received().PushPersistent(controller);
                    break;
            }
        }
    }

    public class TestInputData { }

    public interface ITestView : IView { }
}
