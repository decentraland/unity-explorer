using Arch.Core;
using DCL.CharacterPreview;
using DCL.CharacterPreview.Tests;
using DCL.Utilities;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace DCL.Settings.Tests
{
    [TestFixture]
    public class UpscalingControllerShould
    {
        private const float USER_SCALE = 0.5f;

        private readonly List<Object> created = new ();

        private UniversalRenderPipelineAsset urpAsset = null!;
        private float originalRenderScale;
        private UpscalingFilterSelection originalUpscalingFilter;
        private CharacterPreviewEventBus bus = null!;
        private World world = null!;
        private UpscalingController upscalingController = null!;

        [SetUp]
        public void SetUp()
        {
            urpAsset = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;

            // UpscalingController writes to the project's URP asset, so both touched fields are restored in TearDown
            originalRenderScale = urpAsset.renderScale;
            originalUpscalingFilter = urpAsset.upscalingFilter;

            bus = new CharacterPreviewEventBus();
            world = World.Create();
            upscalingController = new UpscalingController(bus);
            upscalingController.UpdateUpscaling(USER_SCALE);
        }

        [TearDown]
        public void TearDown()
        {
            upscalingController.Dispose();
            World.Destroy(world);

            foreach (Object obj in created)
                Object.DestroyImmediate(obj);

            created.Clear();

            urpAsset.renderScale = originalRenderScale;
            urpAsset.upscalingFilter = originalUpscalingFilter;
        }

        [Test]
        public void ForceFullRenderScaleWhileAPreviewIsShown()
        {
            // Arrange
            CharacterPreviewControllerBase preview = CreatePreview();

            // Act
            preview.OnShow();
            float scaleWhileShown = urpAsset.renderScale;
            UpscalingFilterSelection filterWhileShown = urpAsset.upscalingFilter;
            preview.OnHide();

            // Assert
            Assert.AreEqual(1f, scaleWhileShown);
            Assert.AreEqual(UpscalingFilterSelection.Auto, filterWhileShown);
            Assert.AreEqual(USER_SCALE, urpAsset.renderScale);
            Assert.AreEqual(UpscalingFilterSelection.FSR, urpAsset.upscalingFilter);
        }

        [Test]
        public void RestoreTheUserScaleOnlyWhenTheLastPreviewHides()
        {
            // Arrange
            CharacterPreviewControllerBase first = CreatePreview();
            CharacterPreviewControllerBase second = CreatePreview();
            first.OnShow();
            second.OnShow();

            // Act & Assert
            first.OnHide();
            Assert.AreEqual(1f, urpAsset.renderScale);

            second.OnHide();
            Assert.AreEqual(USER_SCALE, urpAsset.renderScale);
        }

        [Test]
        public void KeepFullRenderScaleWhileAnotherRequesterHoldsIt()
        {
            // Arrange
            var badgeCamera = new object();
            CharacterPreviewControllerBase preview = CreatePreview();
            preview.OnShow();
            upscalingController.RequireFullRenderScale(badgeCamera);

            // Act & Assert
            preview.OnHide();
            Assert.AreEqual(1f, urpAsset.renderScale);

            upscalingController.ReleaseFullRenderScale(badgeCamera);
            Assert.AreEqual(USER_SCALE, urpAsset.renderScale);
        }

        [Test]
        public void IgnoreAReleaseWithoutARequire()
        {
            // Arrange
            CharacterPreviewControllerBase preview = CreatePreview();
            preview.OnShow();

            // Act
            upscalingController.ReleaseFullRenderScale(new object());

            // Assert
            Assert.AreEqual(1f, urpAsset.renderScale);
        }

        [Test]
        public void CountARepeatedRequireOnce()
        {
            // Arrange
            var requester = new object();

            // Act
            upscalingController.RequireFullRenderScale(requester);
            upscalingController.RequireFullRenderScale(requester);
            upscalingController.ReleaseFullRenderScale(requester);

            // Assert
            Assert.AreEqual(USER_SCALE, urpAsset.renderScale);
        }

        [Test]
        public void ApplyAScaleChangedWhileHeldOnRelease()
        {
            // Arrange
            CharacterPreviewControllerBase preview = CreatePreview();
            preview.OnShow();

            // Act
            upscalingController.UpdateUpscaling(0.7f);
            float scaleWhileShown = urpAsset.renderScale;
            preview.OnHide();

            // Assert
            Assert.AreEqual(1f, scaleWhileShown);
            Assert.AreEqual(0.7f, urpAsset.renderScale);
        }

        [Test]
        public void RestoreTheUserScaleWhenTheLastShownPreviewIsDisposed()
        {
            // Arrange
            CharacterPreviewControllerBase preview = CreatePreview();
            preview.OnShow();

            // Act
            preview.Dispose();

            // Assert
            Assert.AreEqual(USER_SCALE, urpAsset.renderScale);
        }

        private CharacterPreviewControllerBase CreatePreview() =>
            new CharacterPreviewTestViews.TestPreview(CharacterPreviewTestViews.Create(created), Substitute.For<ICharacterPreviewFactory>(), world, bus);
    }
}
