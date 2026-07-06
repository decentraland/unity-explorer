using DCL.Ipfs;
using Newtonsoft.Json;
using NUnit.Framework;

namespace ECS.SceneLifeCycle.Tests
{
    public class SceneMetadataShould
    {
        [Test]
        public void DeserializeLandscapeTerrainAsNullWhenAbsent()
        {
            // Arrange
            const string JSON = @"{""main"": ""bin/index.js""}";

            // Act
            SceneMetadata metadata = JsonConvert.DeserializeObject<SceneMetadata>(JSON)!;

            // Assert
            Assert.IsNull(metadata.landscapeTerrain);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DeserializeLandscapeTerrainValue(bool value)
        {
            // Arrange
            string json = $@"{{""main"": ""bin/index.js"", ""landscapeTerrain"": {value.ToString().ToLowerInvariant()}}}";

            // Act
            SceneMetadata metadata = JsonConvert.DeserializeObject<SceneMetadata>(json)!;

            // Assert
            Assert.AreEqual(value, metadata.landscapeTerrain);
        }
    }
}
