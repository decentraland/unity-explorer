using NUnit.Framework;
using UnityEngine;
using Utility;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class PresentationCompositorShould
    {
        private const string SHADER_NAME = "DCL/PresentationCompositor";

        private Material material = null!;
        private PresentationCompositor compositor = null!;
        private Texture2D video = null!;
        private Texture2D camera = null!;

        [SetUp]
        public void SetUp()
        {
            material = new Material(Shader.Find(SHADER_NAME));
            compositor = new PresentationCompositor(material);
            video = new Texture2D(16, 9);
            camera = new Texture2D(4, 4);
        }

        [TearDown]
        public void TearDown()
        {
            compositor.Dispose();
            UnityObjectUtils.SafeDestroy(material);
            UnityObjectUtils.SafeDestroy(video);
            UnityObjectUtils.SafeDestroy(camera);
        }

        [Test]
        public void FindShader_ByName()
        {
            Assert.IsNotNull(Shader.Find(SHADER_NAME));
        }

        [Test]
        public void ReturnRenderTextureOfSlideSize_WhenComposing()
        {
            Texture result = Compose(1920, 1080);

            Assert.IsInstanceOf<RenderTexture>(result);
            var rt = (RenderTexture)result;
            Assert.AreEqual(1920, rt.width);
            Assert.AreEqual(1080, rt.height);
            Assert.AreEqual(RenderTextureFormat.BGRA32, rt.format);
        }

        [Test]
        public void ReuseRenderTexture_WhenSizeUnchanged()
        {
            Texture first = Compose(1920, 1080);
            Texture second = Compose(1920, 1080);

            Assert.IsNotNull(first);
            Assert.AreSame(first, second);
        }

        [Test]
        public void RecreateRenderTexture_WhenSizeChanges()
        {
            Texture landscape = Compose(1920, 1080);
            Texture portrait = Compose(1080, 1920);

            Assert.IsNotNull(landscape);
            Assert.IsNotNull(portrait);
            Assert.AreNotSame(landscape, portrait);
            Assert.AreEqual(1080, portrait.width);
            Assert.AreEqual(1920, portrait.height);
        }

        [Test]
        public void AcceptVideoAndCamera_WhenProvided()
        {
            Texture? result = null;

            Assert.DoesNotThrow(() => result = compositor.Compose(1920, 1080, Texture2D.blackTexture, true,
                new Vector4(0.25f, 0.25f, 0.5f, 0.5f), video, camera, new Vector4(0.02f, 0.7f, 0.15f, 0.27f)));
            Assert.IsNotNull(result);
        }

        [Test]
        public void AcceptVideoRectWithoutFrame()
        {
            Texture? result = null;

            Assert.DoesNotThrow(() => result = compositor.Compose(1920, 1080, Texture2D.blackTexture, true,
                new Vector4(0.25f, 0.25f, 0.5f, 0.5f), null, null, default));
            Assert.IsNotNull(result);
        }

        [Test]
        public void NotMutateSharedMaterial_WhenComposing()
        {
            compositor.Compose(1920, 1080, Texture2D.blackTexture, true,
                new Vector4(0.1f, 0.1f, 0.5f, 0.5f), video, camera, new Vector4(0.02f, 0.7f, 0.15f, 0.27f));

            Assert.AreEqual(new Vector4(1f, 1f, 0f, 0f), material.GetVector("_SlideSize"));
            Assert.AreEqual(0f, material.GetFloat("_CameraEnabled"));
        }

        [Test]
        public void CapCompositeSize_WhenSlideExceedsMax()
        {
            Texture landscape = Compose(4096, 2304);

            Assert.AreEqual(2048, landscape.width);
            Assert.AreEqual(1152, landscape.height);

            Texture portrait = Compose(1080, 4096);

            Assert.AreEqual(540, portrait.width);
            Assert.AreEqual(2048, portrait.height);
        }

        [Test]
        public void KeepSlideSize_WhenWithinMax()
        {
            Texture result = Compose(1920, 1080);

            Assert.AreEqual(1920, result.width);
            Assert.AreEqual(1080, result.height);
        }

        [Test]
        public void SkipBlit_WhenInputsUnchanged()
        {
            Texture first = ComposeWithVideo(new Vector4(0.1f, 0.1f, 0.5f, 0.5f));
            Texture second = ComposeWithVideo(new Vector4(0.1f, 0.1f, 0.5f, 0.5f));

            Assert.AreSame(first, second);
            Assert.AreEqual(1, compositor.blitCount);
        }

        [Test]
        public void Blit_WhenVideoUpdateCountChanges()
        {
            ComposeWithVideo(new Vector4(0.1f, 0.1f, 0.5f, 0.5f));
            video.IncrementUpdateCount();
            ComposeWithVideo(new Vector4(0.1f, 0.1f, 0.5f, 0.5f));

            Assert.AreEqual(2, compositor.blitCount);
        }

        [Test]
        public void Blit_WhenRectChanges()
        {
            ComposeWithVideo(new Vector4(0.1f, 0.1f, 0.5f, 0.5f));
            ComposeWithVideo(new Vector4(0.2f, 0.1f, 0.5f, 0.5f));

            Assert.AreEqual(2, compositor.blitCount);
        }

        [Test]
        public void RecreateRenderTexture_AfterRelease()
        {
            Texture first = Compose(1920, 1080);
            compositor.Release();
            Texture second = Compose(1920, 1080);

            Assert.AreNotSame(first, second);
            Assert.AreEqual(2, compositor.blitCount);
        }

        private Texture ComposeWithVideo(Vector4 videoRect) =>
            compositor.Compose(1920, 1080, Texture2D.blackTexture, true, videoRect, video, camera, new Vector4(0.02f, 0.7f, 0.15f, 0.27f));

        private Texture Compose(int width, int height) =>
            compositor.Compose(width, height, Texture2D.blackTexture, false, default, null, null, default);
    }
}
