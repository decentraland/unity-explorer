using NUnit.Framework;
using Unity.Collections;

namespace Preview.Tests
{
    public class ScreenshotCaptureShould
    {
        [Test]
        public void LeaveFullyTransparentPixelsAlone()
        {
            // Arrange
            using var pixels = new NativeArray<byte>(new byte[] { 0, 0, 0, 0 }, Allocator.Temp);

            // Act
            ScreenshotCapture.RecoverStraightAlpha(pixels);

            // Assert
            Assert.AreEqual(new byte[] { 0, 0, 0, 0 }, pixels.ToArray());
        }

        [Test]
        public void LeaveOpaquePixelsAlone()
        {
            // Arrange
            using var pixels = new NativeArray<byte>(new byte[] { 10, 128, 200, 255 }, Allocator.Temp);

            // Act
            ScreenshotCapture.RecoverStraightAlpha(pixels);

            // Assert
            Assert.AreEqual(new byte[] { 10, 128, 200, 255 }, pixels.ToArray());
        }

        [Test]
        public void DividePremultipliedColourByAlpha()
        {
            // Arrange: half-transparent white, premultiplied.
            using var pixels = new NativeArray<byte>(new byte[] { 128, 128, 128, 128 }, Allocator.Temp);

            // Act
            ScreenshotCapture.RecoverStraightAlpha(pixels);

            // Assert
            Assert.AreEqual(new byte[] { 255, 255, 255, 128 }, pixels.ToArray());
        }

        [Test]
        public void RaiseAlphaToTheBrightestChannel()
        {
            // Arrange: bloom added colour without alpha.
            using var pixels = new NativeArray<byte>(new byte[] { 200, 50, 100, 0 }, Allocator.Temp);

            // Act
            ScreenshotCapture.RecoverStraightAlpha(pixels);

            // Assert
            Assert.AreEqual(new byte[] { 255, 64, 128, 200 }, pixels.ToArray());
        }

        [Test]
        public void ProcessEveryPixel()
        {
            // Arrange
            using var pixels = new NativeArray<byte>(new byte[] { 0, 0, 0, 0, 64, 64, 64, 64 }, Allocator.Temp);

            // Act
            ScreenshotCapture.RecoverStraightAlpha(pixels);

            // Assert
            Assert.AreEqual(new byte[] { 0, 0, 0, 0, 255, 255, 255, 64 }, pixels.ToArray());
        }
    }
}
