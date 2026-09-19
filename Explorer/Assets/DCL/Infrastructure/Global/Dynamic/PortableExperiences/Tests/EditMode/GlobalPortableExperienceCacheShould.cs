using NUnit.Framework;
using PortableExperiences.Controller;

namespace PortableExperiences.Tests
{
    public class GlobalPortableExperienceCacheShould
    {
        private GlobalPortableExperienceCache cache = null!;

        [SetUp]
        public void Setup()
        {
            cache = new GlobalPortableExperienceCache();
        }

        [Test]
        public void EmptyAllSetsWhenCleared()
        {
            // Arrange
            cache.RunningPortableExperiences.Add("running.dcl.eth");
            cache.KilledPortableExperiences.Add("killed.dcl.eth");

            // Act
            cache.Clear();

            // Assert
            Assert.IsEmpty(cache.RunningPortableExperiences);
            Assert.IsEmpty(cache.KilledPortableExperiences);
        }

        [Test]
        public void MatchPortableExperienceIdsIgnoringCase()
        {
            // Arrange
            cache.RunningPortableExperiences.Add("RunningPx.dcl.eth");
            cache.KilledPortableExperiences.Add("KilledPx.dcl.eth");

            // Assert
            Assert.IsTrue(cache.RunningPortableExperiences.Contains("runningpx.dcl.eth"));
            Assert.IsTrue(cache.KilledPortableExperiences.Contains("killedpx.dcl.eth"));
        }
    }
}
