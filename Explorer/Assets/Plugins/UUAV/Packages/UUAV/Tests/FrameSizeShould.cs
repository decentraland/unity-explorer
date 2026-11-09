using NUnit.Framework;

namespace UUAV.Tests
{
    public class FrameSizeShould
    {
        [TestCase(0u, 0u)]
        [TestCase(0u, 720u)]
        [TestCase(1280u, 0u)]
        [TestCase(1u, 720u)]
        [TestCase(1280u, 1u)]
        public void RejectSizesUnityCannotWrap(uint width, uint height)
        {
            // Act
            bool wrappable = UUAVPlayer.IsWrappableSize(new VideoSize { Width = width, Height = height });

            // Assert
            Assert.That(wrappable, Is.False);
        }

        [TestCase(2u, 2u)]
        [TestCase(1280u, 720u)]
        public void AcceptRealFrameSizes(uint width, uint height)
        {
            // Act
            bool wrappable = UUAVPlayer.IsWrappableSize(new VideoSize { Width = width, Height = height });

            // Assert
            Assert.That(wrappable, Is.True);
        }
    }
}
