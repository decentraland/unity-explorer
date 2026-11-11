using Arch.Core;
using DCL.McpServer.Utils;
using NSubstitute;
using NUnit.Framework;
using Utility;

namespace DCL.McpServer.Tests
{
    public class ScreenCaptureServiceShould
    {
        private World world = null!;
        private ScreenCaptureService service = null!;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            service = new ScreenCaptureService(Substitute.For<ICoroutineRunner>(), world);
        }

        [TearDown]
        public void TearDown()
        {
            service.Dispose();
            world.Dispose();
        }

        [Test]
        public void AcquireOnceAndReportTheOwner()
        {
            // Act
            bool acquired = service.TryAcquire("screenshot");

            // Assert
            Assert.That(acquired, Is.True);
            Assert.That(service.BusyOwner, Is.EqualTo("screenshot"));
        }

        [Test]
        public void RefuseASecondOwnerUntilReleased()
        {
            // Arrange
            service.TryAcquire("screenshot");

            // Act
            bool secondAcquired = service.TryAcquire("capture_sequence");
            service.Release("screenshot");
            bool acquiredAfterRelease = service.TryAcquire("capture_sequence");

            // Assert
            Assert.That(secondAcquired, Is.False);
            Assert.That(acquiredAfterRelease, Is.True);
            Assert.That(service.BusyOwner, Is.EqualTo("capture_sequence"));
        }

        [Test]
        public void IgnoreAReleaseFromAnotherOwner()
        {
            // Arrange
            service.TryAcquire("screenshot");

            // Act
            service.Release("capture_sequence");

            // Assert
            Assert.That(service.BusyOwner, Is.EqualTo("screenshot"));
        }

        [TestCase(3396, 1910, 1280, 1280, 720)]
        [TestCase(1280, 720, 1280, 1280, 720)]
        [TestCase(640, 480, 1280, 640, 480)]
        [TestCase(1920, 1080, 64, 64, 36)]
        public void ComputeTheDownscaledSize(int sourceWidth, int sourceHeight, int maxWidth, int expectedWidth, int expectedHeight)
        {
            // Act
            (int width, int height) = ScreenCaptureService.OutputSize(sourceWidth, sourceHeight, maxWidth);

            // Assert
            Assert.That(width, Is.EqualTo(expectedWidth));
            Assert.That(height, Is.EqualTo(expectedHeight));
        }
    }
}
