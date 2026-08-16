using DCL.MapRenderer.ConsumerUtils;
using DCL.MapRenderer.MapCameraController;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DCL.MapRenderer.Tests.ConsumerUtils
{
    public class PixelPerfectMapRendererTextureProviderShould
    {
        private const float RECT_SIZE = 300f;

        private GameObject root = null!;
        private GameObject providerGo = null!;
        private PixelPerfectMapRendererTextureProvider provider = null!;
        private IMapCameraController cameraController = null!;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("root", typeof(RectTransform));
            providerGo = new GameObject("provider", typeof(RectTransform));
            providerGo.transform.SetParent(root.transform, false);
            ((RectTransform)providerGo.transform).sizeDelta = new Vector2(RECT_SIZE, RECT_SIZE);
            providerGo.AddComponent<RawImage>();
            provider = providerGo.AddComponent<PixelPerfectMapRendererTextureProvider>();

            // null hud camera = overlay-canvas branch of WorldToScreenPoint: screen size == world size,
            // deterministic in a headless EditMode run
            cameraController = Substitute.For<IMapCameraController>();
            provider.Activate(cameraController);
            cameraController.ClearReceivedCalls();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(providerGo);
            Object.DestroyImmediate(root);
        }

        private void PumpLateUpdate() =>
            providerGo.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);

        private void ScaleCanvasBy2()
        {
            // windowed -> fullscreen as production sees it: the scale-with-screen-size canvas absorbs
            // the new pixel resolution into its scale factor, the provider's own rect (canvas units)
            // keeps identical dimensions, so Unity never sends OnRectTransformDimensionsChange
            root.transform.localScale = 2f * Vector3.one;
        }

        [Test]
        public void ResizeWhenScreenSpaceSizeChangesWithoutRectChange()
        {
            LogAssert.ignoreFailingMessages = true;

            ScaleCanvasBy2();

            PumpLateUpdate();
            PumpLateUpdate();

            cameraController.Received(1).ResizeTexture(new Vector2Int(600, 600));
        }

        [Test]
        public void NotResizeWhenScreenSpaceSizeUnchanged()
        {
            LogAssert.ignoreFailingMessages = true;

            PumpLateUpdate();
            PumpLateUpdate();

            cameraController.DidNotReceive().ResizeTexture(Arg.Any<Vector2Int>());
        }

        [Test]
        public void ResizeOnRectDimensionsChangeMessage()
        {
            LogAssert.ignoreFailingMessages = true;

            ScaleCanvasBy2();

            providerGo.SendMessage("OnRectTransformDimensionsChange", SendMessageOptions.DontRequireReceiver);

            cameraController.Received(1).ResizeTexture(new Vector2Int(600, 600));
        }

        [Test]
        public void NotResizeAfterDeactivate()
        {
            LogAssert.ignoreFailingMessages = true;

            provider.Deactivate();
            ScaleCanvasBy2();

            PumpLateUpdate();

            cameraController.DidNotReceive().ResizeTexture(Arg.Any<Vector2Int>());
        }
    }
}
