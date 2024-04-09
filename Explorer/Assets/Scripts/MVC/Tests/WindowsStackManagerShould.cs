using NSubstitute;
using NUnit.Framework;

namespace MVC.Tests
{
    public class WindowsStackManagerShould
    {
        private WindowStackManager manager;
        private IController controller;


        public void Setup()
        {
            manager = new WindowStackManager();
            controller = Substitute.For<IController>();
        }


        public void PushPopup()
        {
            manager.PushPopup(controller);

            Assert.AreEqual(1, manager.popupStack.Count);
        }


        public void PopPopup()
        {
            manager.PushPopup(controller);
            manager.PopPopup(controller);

            Assert.AreEqual(0, manager.popupStack.Count);
        }


        public void PushPopupWithPrevious()
        {
            var previousController = Substitute.For<IController>();

            manager.PushPopup(previousController);
            var pushInfo = manager.PushPopup(controller);

            Assert.AreEqual(402, pushInfo.ControllerOrdering.OrderInLayer);
            Assert.AreEqual(401, pushInfo.PopupCloserOrdering.OrderInLayer);
            Assert.AreSame(previousController, pushInfo.PreviousController);
        }


        public void PushFullscreen()
        {
            manager.PushFullscreen(controller);

            Assert.AreSame(controller, manager.fullscreenController);
        }


        public void PopFullscreen()
        {
            manager.PushFullscreen(controller);

            manager.PopFullscreen(controller);

            Assert.IsNull(manager.fullscreenController);
        }


        public void PushPersistent()
        {
            manager.PushPersistent(controller);

            Assert.AreEqual(1, manager.persistentStack.Count);
        }


        public void PushTop()
        {
            manager.PushOverlay(controller);

            Assert.AreSame(controller, manager.topController);
        }


        public void PopTop()
        {
            manager.PushOverlay(controller);

            manager.PopOverlay(controller);

            Assert.IsNull(manager.topController);
        }
    }
    public class TestInputData2 { }

    public class TestView2 : ViewBase, IView
    {
    }
}
