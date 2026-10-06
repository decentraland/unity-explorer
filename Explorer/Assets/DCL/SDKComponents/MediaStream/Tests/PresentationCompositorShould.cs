using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
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
        private List<Texture2D> created = null!;

        [SetUp]
        public void SetUp()
        {
            material = new Material(Shader.Find(SHADER_NAME));
            compositor = new PresentationCompositor(material);
            video = new Texture2D(16, 9);
            camera = new Texture2D(4, 4);
            created = new List<Texture2D>();
        }

        [TearDown]
        public void TearDown()
        {
            compositor.Dispose();
            UnityObjectUtils.SafeDestroy(material);
            UnityObjectUtils.SafeDestroy(video);
            UnityObjectUtils.SafeDestroy(camera);

            foreach (Texture2D texture in created)
                UnityObjectUtils.SafeDestroy(texture);
        }

        [Test]
        public void ReturnRenderTextureOfSlideSize_WhenComposing()
        {
            // Act
            Texture result = Compose(1920, 1080);

            // Assert
            Assert.IsInstanceOf<RenderTexture>(result);
            var rt = (RenderTexture)result;
            Assert.AreEqual(1920, rt.width);
            Assert.AreEqual(1080, rt.height);
            Assert.AreEqual(RenderTextureFormat.BGRA32, rt.format);
        }

        [Test]
        public void RecreateRenderTexture_WhenSizeChanges()
        {
            // Act
            Texture landscape = Compose(1920, 1080);
            Texture portrait = Compose(1080, 1920);

            // Assert
            Assert.AreNotSame(landscape, portrait);
            Assert.AreEqual(1080, portrait.width);
            Assert.AreEqual(1920, portrait.height);
        }

        [Test]
        public void NotMutateSharedMaterial_WhenComposing()
        {
            // Act
            compositor.Compose(1920, 1080, Texture2D.blackTexture, true,
                new Vector4(0.1f, 0.1f, 0.5f, 0.5f), video, camera, new Vector4(0.02f, 0.7f, 0.15f, 0.27f));

            // Assert
            Assert.AreEqual(new Vector4(1f, 1f, 0f, 0f), material.GetVector("_SlideSize"));
            Assert.AreEqual(0f, material.GetFloat("_CameraEnabled"));
        }

        [Test]
        public void SkipBlit_WhenOnlyTheSlideIsShownUnchanged()
        {
            // Act
            Texture first = Compose(1920, 1080);
            Texture second = Compose(1920, 1080);

            // Assert
            Assert.AreSame(first, second);
            Assert.AreEqual(1, compositor.blitCount);
        }

        [Test]
        public void BlitEveryCall_WhenVideoIsShown()
        {
            // Arrange
            var videoRect = new Vector4(0.1f, 0.1f, 0.5f, 0.5f);

            // Act
            compositor.Compose(1920, 1080, Texture2D.blackTexture, true, videoRect, video, null, default);
            compositor.Compose(1920, 1080, Texture2D.blackTexture, true, videoRect, video, null, default);

            // Assert
            Assert.AreEqual(2, compositor.blitCount);
        }

        [Test]
        public void BlitEveryCall_WhenCameraIsShown()
        {
            // Arrange
            Vector4 cameraRect = new Vector4(0.02f, 0.7f, 0.15f, 0.27f);

            // Act
            compositor.Compose(1920, 1080, Texture2D.blackTexture, false, default, null, camera, cameraRect);
            compositor.Compose(1920, 1080, Texture2D.blackTexture, false, default, null, camera, cameraRect);

            // Assert
            Assert.AreEqual(2, compositor.blitCount);
        }

        [Test]
        public void BlitOnce_WhenCameraStopsBeingShown()
        {
            // Arrange
            var cameraRect = new Vector4(0.02f, 0.7f, 0.15f, 0.27f);

            // Act
            compositor.Compose(1920, 1080, Texture2D.blackTexture, false, default, null, camera, cameraRect);
            compositor.Compose(1920, 1080, Texture2D.blackTexture, false, default, null, null, cameraRect);
            compositor.Compose(1920, 1080, Texture2D.blackTexture, false, default, null, null, cameraRect);

            // Assert
            Assert.AreEqual(2, compositor.blitCount);
        }

        [Test]
        public void Blit_WhenRectChanges()
        {
            // Act
            compositor.Compose(1920, 1080, Texture2D.blackTexture, true, new Vector4(0.1f, 0.1f, 0.5f, 0.5f), null, null, default);
            compositor.Compose(1920, 1080, Texture2D.blackTexture, true, new Vector4(0.2f, 0.1f, 0.5f, 0.5f), null, null, default);

            // Assert
            Assert.AreEqual(2, compositor.blitCount);
        }

        [Test]
        public void RecreateRenderTexture_AfterRelease()
        {
            // Act
            Texture first = Compose(1920, 1080);
            compositor.Release();
            Texture second = Compose(1920, 1080);

            // Assert
            Assert.AreNotSame(first, second);
            Assert.AreEqual(2, compositor.blitCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DrawEachLayerInItsRect_WhenComposingKnownColours(bool linearData)
        {
            // Arrange
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("needs a graphics device");

            var red = new Color32(255, 0, 0, 255);
            var blue = new Color32(0, 0, 128, 255);
            var green = new Color32(0, 128, 0, 255);
            Texture2D slide = Solid(2, 2, red, false);
            Texture2D videoFrame = Solid(16, 16, blue, linearData);
            Texture2D cameraFrame = Solid(4, 4, green, linearData);

            // Act
            var composite = (RenderTexture)compositor.Compose(64, 32, slide, true, new Vector4(0f, 0f, 0.5f, 0.5f), videoFrame, cameraFrame,
                new Vector4(0.75f, 0.5f, 0.25f, 0.5f));
            Texture2D pixels = ReadBack(composite);

            // Assert
            AssertPixel(blue, pixels, 16, 8);
            AssertPixel(new Color32(0, 0, 0, 255), pixels, 4, 8);
            AssertPixel(green, pixels, 56, 24);
            AssertPixel(red, pixels, 49, 17);
        }

        private Texture2D Solid(int width, int height, Color32 color, bool linear)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, linear);
            var pixels = new Color32[width * height];
            Array.Fill(pixels, color);
            texture.SetPixels32(pixels);
            texture.Apply();
            created.Add(texture);
            return texture;
        }

        private Texture2D ReadBack(RenderTexture source)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = source;
            var pixels = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            pixels.Apply();
            RenderTexture.active = previous;
            created.Add(pixels);
            return pixels;
        }

        private static void AssertPixel(Color32 expected, Texture2D pixels, int left, int top)
        {
            Color32 actual = pixels.GetPixel(left, pixels.height - 1 - top);

            Assert.IsTrue(Mathf.Abs(actual.r - expected.r) <= 3 && Mathf.Abs(actual.g - expected.g) <= 3 && Mathf.Abs(actual.b - expected.b) <= 3,
                $"Pixel ({left}, {top}) from the top-left is {actual}, expected {expected}");
        }

        private Texture Compose(int width, int height) =>
            compositor.Compose(width, height, Texture2D.blackTexture, false, default, null, null, default);
    }
}
