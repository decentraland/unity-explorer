using DCL.WebRequests;
using NSubstitute;
using NUnit.Framework;
using PortableExperiences.Controller;
using SceneRuntime.ScenePermissions;

namespace PortableExperiences.Tests
{
    public class LocalPortableExperienceCacheShould
    {
        private LocalPortableExperienceCache cache;

        [SetUp]
        public void Setup()
        {
            cache = new LocalPortableExperienceCache(Substitute.For<IWebRequestController>());
        }

        [TestCase(ScenePermissionNames.USE_WEB3_API)]
        [TestCase(ScenePermissionNames.OPEN_EXTERNAL_LINK)]
        [TestCase(ScenePermissionNames.USE_WEBSOCKET)]
        [TestCase(ScenePermissionNames.PORTABLE_EXPERIENCE)]
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
        public void EmptyAuthorizationSetsWhenCleared()
        {
            // Arrange
            cache.AuthorizedPortableExperiences.Add("authorized.dcl.eth");
            cache.DeniedPortableExperiences.Add("denied.dcl.eth");

            // Act
            cache.Clear();

            // Assert
            Assert.IsEmpty(cache.AuthorizedPortableExperiences);
            Assert.IsEmpty(cache.DeniedPortableExperiences);
        }

        [Test]
        public void MatchPortableExperienceIdsIgnoringCase()
        {
            // Arrange
            cache.AuthorizedPortableExperiences.Add("SomePx.dcl.eth");

            // Assert
            Assert.IsTrue(cache.AuthorizedPortableExperiences.Contains("somepx.dcl.eth"));
        }
    }
}
