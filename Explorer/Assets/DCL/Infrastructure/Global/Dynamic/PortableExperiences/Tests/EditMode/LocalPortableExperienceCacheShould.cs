using DCL.WebRequests;
using NSubstitute;
using NUnit.Framework;
using PortableExperiences.Controller;
using SceneRuntime.ScenePermissions;

namespace PortableExperiences.Tests
{
    public class LocalPortableExperienceCacheShould
    {
        private LocalPortableExperienceCache cache = null!;

        [SetUp]
        public void Setup()
        {
            cache = new LocalPortableExperienceCache(Substitute.For<IWebRequestController>());
        }

        [TestCase(ScenePermissionNames.USE_WEB3_API)]
        [TestCase(ScenePermissionNames.OPEN_EXTERNAL_LINK)]
        [TestCase(ScenePermissionNames.USE_WEBSOCKET)]
        [TestCase(ScenePermissionNames.SPAWN_PORTABLE_EXPERIENCE)]
        [TestCase(ScenePermissionNames.USE_FETCH)]
        public void RequireAuthorizationForGatedPermissions(string permission)
        {
            Assert.IsTrue(LocalPortableExperienceCache.PermissionRequiresUserAuthorization(permission));
        }

        [TestCase(ScenePermissionNames.ALLOW_MEDIA_HOSTNAMES)]
        [TestCase("some-unknown-permission")]
        public void NotRequireAuthorizationForUngatedPermissions(string permission)
        {
            Assert.IsFalse(LocalPortableExperienceCache.PermissionRequiresUserAuthorization(permission));
        }

        [Test]
        public void EmptyAllSetsWhenCleared()
        {
            // Arrange
            cache.AuthorizedPortableExperiences.Add("authorized.dcl.eth");
            cache.DeniedPortableExperiences.Add("denied.dcl.eth");
            cache.RunningPortableExperiences.Add("running.dcl.eth");
            cache.KilledPortableExperiences.Add("killed.dcl.eth");

            // Act
            cache.Clear();

            // Assert
            Assert.IsEmpty(cache.AuthorizedPortableExperiences);
            Assert.IsEmpty(cache.DeniedPortableExperiences);
            Assert.IsEmpty(cache.RunningPortableExperiences);
            Assert.IsEmpty(cache.KilledPortableExperiences);
        }

        [Test]
        public void MatchPortableExperienceIdsIgnoringCase()
        {
            // Arrange
            cache.AuthorizedPortableExperiences.Add("SomePx.dcl.eth");
            cache.RunningPortableExperiences.Add("RunningPx.dcl.eth");
            cache.KilledPortableExperiences.Add("KilledPx.dcl.eth");

            // Assert
            Assert.IsTrue(cache.AuthorizedPortableExperiences.Contains("somepx.dcl.eth"));
            Assert.IsTrue(cache.RunningPortableExperiences.Contains("runningpx.dcl.eth"));
            Assert.IsTrue(cache.KilledPortableExperiences.Contains("killedpx.dcl.eth"));
        }
    }
}
