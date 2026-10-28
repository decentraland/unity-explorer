using DCL.Backpack.AvatarSection.Outfits.Services;
using NUnit.Framework;

namespace DCL.Backpack.Tests
{
    [TestFixture]
    public class AvatarScreenshotServiceShould
    {
        [Test]
        public void KeepASquareFrameUntouched()
        {
            // Arrange
            const int SIDE = 480;

            // Act
            AvatarScreenshotService.ThumbnailFraming framing = AvatarScreenshotService.SquareFraming(SIDE, SIDE);

            // Assert
            Assert.AreEqual(SIDE, framing.Side);
            Assert.AreEqual(0, framing.SourceX);
            Assert.AreEqual(SIDE, framing.CopyWidth);
            Assert.AreEqual(0, framing.DestinationX);
        }

        [TestCase(288, 443, 77)]
        [TestCase(384, 590, 103)]
        public void CentreANarrowFrameBetweenPaddingColumns(int width, int height, int expectedDestinationX)
        {
            // Act
            AvatarScreenshotService.ThumbnailFraming framing = AvatarScreenshotService.SquareFraming(width, height);

            // Assert
            Assert.AreEqual(height, framing.Side);
            Assert.AreEqual(0, framing.SourceX);
            Assert.AreEqual(width, framing.CopyWidth);
            Assert.AreEqual(expectedDestinationX, framing.DestinationX);
        }

        [Test]
        public void CropAWideFrameAroundItsCentre()
        {
            // Act
            AvatarScreenshotService.ThumbnailFraming framing = AvatarScreenshotService.SquareFraming(600, 400);

            // Assert
            Assert.AreEqual(400, framing.Side);
            Assert.AreEqual(100, framing.SourceX);
            Assert.AreEqual(400, framing.CopyWidth);
            Assert.AreEqual(0, framing.DestinationX);
        }
    }
}
