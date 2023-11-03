using Cysharp.Threading.Tasks;
using MVC.PopupsController.PopupCloser;
using NSubstitute;
using NSubstitute.ReceivedExtensions;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

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
            Assert.AreEqual(1, mvcManager.controllers.Count);
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
        [TestCase(CanvasOrdering.SortingLayer.Popup)]
        [TestCase(CanvasOrdering.SortingLayer.Fullscreen)]
        [TestCase(CanvasOrdering.SortingLayer.Overlay)]
        [TestCase(CanvasOrdering.SortingLayer.Persistent)]
        public async Task Show(CanvasOrdering.SortingLayer layer)
        {
            IController<ITestView, TestInputData> controller = Substitute.For<IController<ITestView, TestInputData>>();
            controller.SortLayers.Returns(layer);

            mvcManager.RegisterController(controller);

            await mvcManager.Show(new ShowCommand<ITestView, TestInputData>());

            switch (layer)
            {
                case CanvasOrdering.SortingLayer.Popup:
                    popupCloserView.Received().Show(Arg.Any<CancellationToken>());
                    windowsStackManager.Received().PushPopup(controller);
                    break;
                case CanvasOrdering.SortingLayer.Fullscreen:
                    popupCloserView.DidNotReceive().Show(Arg.Any<CancellationToken>());
                    windowsStackManager.Received().PushFullscreen(controller);
                    break;
                case CanvasOrdering.SortingLayer.Overlay:
                    popupCloserView.DidNotReceive().Show(Arg.Any<CancellationToken>());
                    windowsStackManager.Received().PushOverlay(controller);
                    break;
                case CanvasOrdering.SortingLayer.Persistent:
                    popupCloserView.DidNotReceive().Show(Arg.Any<CancellationToken>());
                    windowsStackManager.Received().PushPersistent(controller);
                    break;
            }
        }

    }

    public class TestController : IController<ITestView, TestInputData>
    {
        public UniTask LaunchViewLifeCycle(CanvasOrdering ordering, TestInputData inputData, CancellationToken ct) =>
            throw new NotImplementedException();

        public CanvasOrdering.SortingLayer SortLayers { get; }

        public void OnFocus()
        {

        }

        public void OnBlur()
        {

        }

        UniTask IController.HideView(CancellationToken ct) =>
            throw new NotImplementedException();
    }

    public class TestInputData { }

    public interface ITestView : IView
    {
    }

}
