using Cysharp.Threading.Tasks;
using MVC.PopupsController.PopupCloser;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading;
using UnityEngine;

namespace MVC.Tests
{
    public class MVCManagerShould
    {
        private MVCManager mvcManager;

        [SetUp]
        public void SetUp()
        {
            mvcManager = new MVCManager(Substitute.For<IWindowsStackManager>(), new CancellationTokenSource(), Substitute.For<IPopupCloserView>());
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
