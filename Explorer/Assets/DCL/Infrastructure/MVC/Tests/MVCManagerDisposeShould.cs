using MVC.PopupsController.PopupCloser;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading;
using UnityEngine.TestTools;

namespace MVC.Tests
{
    [TestFixture]
    public class MVCManagerDisposeShould
    {
        private IWindowsStackManager windowsStackManager = null!;
        private MVCManager mvcManager = null!;

        [SetUp]
        public void SetUp()
        {
            windowsStackManager = Substitute.For<IWindowsStackManager>();
            mvcManager = new MVCManager(windowsStackManager, new CancellationTokenSource(), Substitute.For<IPopupCloserView>());
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void DisposeRemainingControllersAndStackWhenOneControllerThrows()
        {
            // The guarded disposal loop reports the throwing controller via ReportHub (error-level log).
            LogAssert.ignoreFailingMessages = true;

            IController<ITestView, TestInputData> throwingController = Substitute.For<IController<ITestView, TestInputData>>();
            throwingController.When(c => c.Dispose()).Do(_ => throw new InvalidOperationException("dispose failure"));
            IController<IOtherTestView, TestInputData> otherController = Substitute.For<IController<IOtherTestView, TestInputData>>();

            mvcManager.RegisterController(throwingController);
            mvcManager.RegisterController(otherController);

            Assert.DoesNotThrow(mvcManager.Dispose);

            // Order-independent: whichever controller is disposed first, both must be reached
            // and the windows stack must be disposed after the loop.
            otherController.Received(1).Dispose();
            windowsStackManager.Received(1).Dispose();
        }
    }

    public interface IOtherTestView : IView { }
}
