using NUnit.Framework;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class UpdateMediaPlayerPrioritizationSystemShould
    {
        [Test]
        [TestCase(0f)]
        [TestCase(-1f)]
        public void NotResumeSeekWhileDurationIsUnknown(float duration)
        {
            // Act
            bool hasResumeTime = UpdateMediaPlayerPrioritizationSystem.TryGetResumeTime(12.5d, duration, out double resumeTime);

            // Assert
            Assert.That(hasResumeTime, Is.False);
            Assert.That(resumeTime, Is.Zero);
        }

        [Test]
        public void WrapResumeTimeAroundDuration()
        {
            // Act
            bool hasResumeTime = UpdateMediaPlayerPrioritizationSystem.TryGetResumeTime(25d, 10f, out double resumeTime);

            // Assert
            Assert.That(hasResumeTime, Is.True);
            Assert.That(resumeTime, Is.EqualTo(5d).Within(1e-6));
        }

        [Test]
        public void NotResumeSeekWhenPauseDurationIsNotFinite()
        {
            // Act
            bool hasResumeTime = UpdateMediaPlayerPrioritizationSystem.TryGetResumeTime(double.NaN, 10f, out double _);

            // Assert
            Assert.That(hasResumeTime, Is.False);
        }
    }
}
