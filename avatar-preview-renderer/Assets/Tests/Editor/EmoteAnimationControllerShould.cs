using NUnit.Framework;

namespace Tests
{
    public class EmoteAnimationControllerShould
    {
        private const float TOLERANCE = 0.0001f;

        [TestCase(0f, 2f, 0, 0f)]
        [TestCase(1.5f, 2f, 0, 1.5f)]
        [TestCase(2f, 2f, 1, 0f)]
        [TestCase(7f, 3f, 2, 1f)]
        public void SplitAClipTimeIntoWrapsAndPosition(float time, float length, int loops, float clipTime)
        {
            // Act
            var wrapped = EmoteAnimationController.WrapClipTime(time, length);

            // Assert
            Assert.AreEqual(loops, wrapped.loops);
            Assert.AreEqual(clipTime, wrapped.clipTime, TOLERANCE);
        }

        [Test]
        public void ReportNothingForAnEmptyClip()
        {
            // Act
            var wrapped = EmoteAnimationController.WrapClipTime(3f, 0f);

            // Assert
            Assert.AreEqual(0, wrapped.loops);
            Assert.AreEqual(0f, wrapped.clipTime, TOLERANCE);
        }
    }
}
