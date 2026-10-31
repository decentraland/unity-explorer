using DCL.Ipfs;
using Newtonsoft.Json;
using NUnit.Framework;

namespace ECS.Tests
{
    public class SceneMetadataShould
    {
        [Test]
        public void DefaultRequiredPermissionsToEmptyWhenAbsent()
        {
            // Act
            SceneMetadata metadata = JsonConvert.DeserializeObject<SceneMetadata>(@"{ ""runtimeVersion"": ""7"" }")!;

            // Assert
            Assert.IsNotNull(metadata.requiredPermissions);
            Assert.IsEmpty(metadata.requiredPermissions);
        }

        [Test]
        public void DefaultRequiredPermissionsToEmptyWhenNull()
        {
            // Act
            SceneMetadata metadata = JsonConvert.DeserializeObject<SceneMetadata>(@"{ ""runtimeVersion"": ""7"", ""requiredPermissions"": null }")!;

            // Assert
            Assert.IsNotNull(metadata.requiredPermissions);
            Assert.IsEmpty(metadata.requiredPermissions);
        }

        [Test]
        public void KeepDeclaredRequiredPermissions()
        {
            // Act
            SceneMetadata metadata = JsonConvert.DeserializeObject<SceneMetadata>(@"{ ""requiredPermissions"": [""USE_WEB3_API"", ""USE_FETCH""] }")!;

            // Assert
            CollectionAssert.AreEqual(new[] { "USE_WEB3_API", "USE_FETCH" }, metadata.requiredPermissions);
        }
    }
}
