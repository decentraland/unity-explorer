using NUnit.Framework;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class MediaOpenThrottleShould
    {
        [SetUp]
        [TearDown]
        public void ResetThrottle()
        {
            MediaOpenThrottle.MarkOpened(float.NegativeInfinity);
        }

        [Test]
        public void AllowOpen_WhenNothingWasOpened()
        {
            // Act & Assert
            Assert.IsTrue(MediaOpenThrottle.CanOpen(0f));
        }

        [Test]
        public void BlockOpen_WhenLastOpenWasLessThanHalfASecondAgo()
        {
            // Act
            MediaOpenThrottle.MarkOpened(10f);

            // Assert
            Assert.IsFalse(MediaOpenThrottle.CanOpen(10.4f));
        }

        [Test]
        public void AllowOpen_WhenHalfASecondPassed()
        {
            // Act
            MediaOpenThrottle.MarkOpened(10f);

            // Assert
            Assert.IsTrue(MediaOpenThrottle.CanOpen(10.5f));
        }

        [Test]
        public void ReportSecondsSinceLastOpen()
        {
            // Arrange
            MediaOpenThrottle.MarkOpened(10f);

            // Act
            float elapsed = MediaOpenThrottle.SecondsSinceLastOpen(10.25f);

            // Assert
            Assert.AreEqual(0.25f, elapsed);
        }

        [Test]
        public void KeepAllowingOpen_WhenOnlyCheckedWithoutMarking()
        {
            // Act
            MediaOpenThrottle.MarkOpened(10f);
            MediaOpenThrottle.CanOpen(10.5f);

            // Assert
            Assert.IsTrue(MediaOpenThrottle.CanOpen(10.6f));
        }
    }
}
