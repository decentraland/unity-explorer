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
            Assume.That(urpAsset, Is.Not.Null, "The project's active render pipeline is not URP");

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

        [Test]
        public void RestoreUserScaleWhenOverlappingPreviewUIsClose()
        {
            // Arrange
            // NSubstitute proxy type names end in "Proxy", so they still match the controller-name allow-list
            IController explorePanel = Substitute.For<FakeExplorePanelController>();
            IController passport = Substitute.For<FakePassportController>();
            urpAsset.renderScale = 0.5f;

            // Act
            mvcManager.OnViewShowed += Raise.Event<Action<IController>>(explorePanel);
            mvcManager.OnViewShowed += Raise.Event<Action<IController>>(passport);
            float scaleWhileOpen = urpAsset.renderScale;
            mvcManager.OnViewClosed += Raise.Event<Action<IController>>(passport);
            mvcManager.OnViewClosed += Raise.Event<Action<IController>>(explorePanel);

            // Assert
            Assert.AreEqual(1f, scaleWhileOpen);
            Assert.AreEqual(0.5f, urpAsset.renderScale);
        }

        public abstract class FakeExplorePanelController : IController
        {
            public abstract ControllerState State { get; }
            public abstract CanvasOrdering.SortingLayer Layer { get; }

            public abstract void Focus();

            public abstract void Blur();

            public abstract UniTask HideViewAsync(CancellationToken ct);

            public abstract void SetViewCanvasActive(bool isActive);

            public abstract void Dispose();
        }

        public abstract class FakePassportController : IController
        {
            public abstract ControllerState State { get; }
            public abstract CanvasOrdering.SortingLayer Layer { get; }

            public abstract void Focus();

            public abstract void Blur();

            public abstract UniTask HideViewAsync(CancellationToken ct);

            public abstract void SetViewCanvasActive(bool isActive);

            public abstract void Dispose();
        }
    }
}
