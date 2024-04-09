using MVC.PopupsController.PopupCloser;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace MVC.Tests
{
    public class MVCManagerShould
    {
        private IWindowsStackManager windowsStackManager;
        private MVCManager mvcManager;
        private IPopupCloserView popupCloserView;


        public void Setup()
        {
            windowsStackManager = Substitute.For<IWindowsStackManager>();
            popupCloserView = Substitute.For<IPopupCloserView>();
            mvcManager = new MVCManager(windowsStackManager, new CancellationTokenSource(), popupCloserView);
        }


        public void RegisterController()
        {
            // Arrange
            IController<ITestView, TestInputData> controller = Substitute.For<IController<ITestView, TestInputData>>();

            // Act
            mvcManager.RegisterController(controller);

            // Assert
            Assert.AreEqual(1, mvcManager.Controllers.Count);
        }


        public void RegisterControllerThrowsExceptionWhenSameControllerIsAddedTwice()
        {
            // Arrange
            IController<ITestView, TestInputData> controller = Substitute.For<IController<ITestView, TestInputData>>();

            // Act
            mvcManager.RegisterController(controller);

            // Assert
            Assert.Throws<ArgumentException>(() => mvcManager.RegisterController(controller));
        }






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
