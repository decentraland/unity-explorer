using Cysharp.Threading.Tasks;
using DCL.Utilities;
using MVC;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DCL.Settings.Tests
{
    [TestFixture]
    public class UpscalingControllerShould
    {
        private UniversalRenderPipelineAsset urpAsset;
        private float originalRenderScale;
        private UpscalingFilterSelection originalUpscalingFilter;
        private IMVCManager mvcManager;
        private UpscalingController upscalingController;

        [SetUp]
        public void SetUp()
        {
            urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Assert.IsNotNull(urpAsset, "The project's active render pipeline is not URP");

            // UpscalingController writes to the project's URP asset, so both touched fields are restored in TearDown
            originalRenderScale = urpAsset.renderScale;
            originalUpscalingFilter = urpAsset.upscalingFilter;

            mvcManager = Substitute.For<IMVCManager>();
            upscalingController = new UpscalingController(mvcManager);
        }

        [TearDown]
        public void TearDown()
        {
            upscalingController?.Dispose();

            if (urpAsset == null) return;

            urpAsset.renderScale = originalRenderScale;
            urpAsset.upscalingFilter = originalUpscalingFilter;
        }

        [TestCase(typeof(FakeAuthenticationScreenController))]
        [TestCase(typeof(FakeExplorePanelController))]
        [TestCase(typeof(FakePassportController))]
        [TestCase(typeof(FakeLobbyController))]
        [TestCase(typeof(FakeBackpackModalController))]
        public void ForceFullRenderScaleWhilePreviewUIIsOpen(Type controllerType)
        {
            // Arrange
            var controller = (IController)Activator.CreateInstance(controllerType);
            urpAsset.renderScale = 0.5f;

            // Act
            mvcManager.OnViewShowed += Raise.Event<Action<IController>>(controller);
            float scaleWhileOpen = urpAsset.renderScale;
            mvcManager.OnViewClosed += Raise.Event<Action<IController>>(controller);

            // Assert
            Assert.AreEqual(1f, scaleWhileOpen);
            Assert.AreEqual(0.5f, urpAsset.renderScale);
        }

        [Test]
        public void RestoreUserScaleWhenOverlappingPreviewUIsClose()
        {
            // Arrange
            IController lobby = new FakeLobbyController();
            IController backpack = new FakeBackpackModalController();
            urpAsset.renderScale = 0.5f;

            // Act
            mvcManager.OnViewShowed += Raise.Event<Action<IController>>(lobby);
            mvcManager.OnViewShowed += Raise.Event<Action<IController>>(backpack);
            float scaleWhileOpen = urpAsset.renderScale;
            mvcManager.OnViewClosed += Raise.Event<Action<IController>>(backpack);
            mvcManager.OnViewClosed += Raise.Event<Action<IController>>(lobby);

            // Assert
            Assert.AreEqual(1f, scaleWhileOpen);
            Assert.AreEqual(0.5f, urpAsset.renderScale);
        }

        public class FakeController : IController
        {
            public ControllerState State => default;
            public CanvasOrdering.SortingLayer Layer => default;

            public void Focus() { }

            public void Blur() { }

            public UniTask HideViewAsync(CancellationToken ct) =>
                UniTask.CompletedTask;

            public void SetViewCanvasActive(bool isActive) { }

            public void Dispose() { }
        }

        public class FakeAuthenticationScreenController : FakeController { }

        public class FakeExplorePanelController : FakeController { }

        public class FakePassportController : FakeController { }

        public class FakeLobbyController : FakeController { }

        public class FakeBackpackModalController : FakeController { }
    }
}
