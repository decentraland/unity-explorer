using Cysharp.Threading.Tasks;
using MVC.PopupsController.PopupCloser;
using NSubstitute;
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

        [SetUp]
        public void Setup()
        {
            windowsStackManager = Substitute.For<IWindowsStackManager>();
            mvcManager = new MVCManager(windowsStackManager, new CancellationTokenSource(), Substitute.For<IPopupCloserView>());
        }

        [Test]
        public void RegisterController()
        {
            // Arrange
            TestController controller = new TestController(TestController.CreateLazily(new GameObject("TEST_GO").AddComponent<TestView>(), null));

            // Act
            mvcManager.RegisterController(controller);

            // Assert
            Assert.AreEqual(1, mvcManager.controllers.Count);
        }

        [Test]
        public void RegisterControllerThrowsExceptionWhenSameControllerIsAddedTwice()
        {
            // Arrange
            TestController controller = new TestController(TestController.CreateLazily(new GameObject("TEST_GO").AddComponent<TestView>(), null));

            // Act
            mvcManager.RegisterController(controller);

            // Assert
            Assert.Throws<ArgumentException>(() => mvcManager.RegisterController(controller));
        }

        [Test]
        public async Task Show()
        {
            TestController controller = new TestController(TestController.CreateLazily(new GameObject("TEST_GO").AddComponent<TestView>(), null));

            mvcManager.RegisterController(controller);

            await mvcManager.Show(TestController.IssueCommand(new TestInputData()));
            windowsStackManager.Received().PushPopup(Arg.Any<IController>());
        }

    }

    public class TestController : ControllerBase<TestView, TestInputData>
    {
        public TestController(ViewFactoryMethod viewFactory) : base(viewFactory)
        {
        }

        public override CanvasOrdering.SortingLayer SortLayers => CanvasOrdering.SortingLayer.Popup;

        protected override UniTask WaitForCloseIntent(CancellationToken ct) =>
            throw new NotImplementedException();
    }

    public class TestInputData { }

    public class TestView : ViewBase, IView
    {
    }

}
