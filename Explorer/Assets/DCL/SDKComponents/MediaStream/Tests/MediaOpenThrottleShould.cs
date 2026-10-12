using NUnit.Framework;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class MediaOpenThrottleShould
    {
        [SetUp]
        public void SetUp()
        {
            MediaOpenThrottle.MarkOpened(float.NegativeInfinity);
        }

        [Test]
        public void AllowOpen_WhenNothingWasOpened()
        {
            Assert.IsTrue(MediaOpenThrottle.CanOpen(0f));
        }

        [Test]
        public void BlockOpen_WhenLastOpenWasLessThanHalfASecondAgo()
        {
            MediaOpenThrottle.MarkOpened(10f);

            Assert.IsFalse(MediaOpenThrottle.CanOpen(10.4f));
        }

        [Test]
        public void AllowOpen_WhenHalfASecondPassed()
        {
            MediaOpenThrottle.MarkOpened(10f);

            Assert.IsTrue(MediaOpenThrottle.CanOpen(10.5f));
        }

        [Test]
        public void KeepAllowingOpen_WhenOnlyCheckedWithoutMarking()
        {
            MediaOpenThrottle.MarkOpened(10f);
            MediaOpenThrottle.CanOpen(10.5f);

            Assert.IsTrue(MediaOpenThrottle.CanOpen(10.6f));
        }
    }
}
