using DCL.Ipfs;
using Newtonsoft.Json;
using NUnit.Framework;

namespace DCL.SceneLifeCycle.Tests
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

        [Test]
        public void DeserializeNullSpawnPointCoordinateAsUnset()
        {
            // Arrange
            const string JSON = @"{""main"": ""bin/index.js"", ""spawnPoints"": [{""name"": ""spawn"", ""position"": {""x"": null, ""y"": 0, ""z"": [1, 2]}}]}";

            // Act
            SceneMetadata metadata = JsonConvert.DeserializeObject<SceneMetadata>(JSON)!;

            // Assert
            SceneMetadata.SpawnPoint.Position position = metadata.spawnPoints![0].position;
            Assert.IsNull(position.x.SingleValue);
            Assert.IsNull(position.x.MultiValue);
            Assert.AreEqual(0f, position.y.SingleValue);
            CollectionAssert.AreEqual(new[] { 1f, 2f }, position.z.MultiValue);
        }

        [TestCase("[null, 5]")]
        [TestCase("[1, \"abc\"]")]
        [TestCase("\"abc\"")]
        [TestCase("{}")]
        public void DeserializeMalformedSpawnPointCoordinateAsUnset(string coordinate)
        {
            // Arrange
            string json = $@"{{""main"": ""bin/index.js"", ""spawnPoints"": [{{""name"": ""spawn"", ""position"": {{""x"": {coordinate}, ""y"": 0, ""z"": [1, 2]}}}}]}}";

            // Act
            SceneMetadata metadata = JsonConvert.DeserializeObject<SceneMetadata>(json)!;

            // Assert
            SceneMetadata.SpawnPoint.Position position = metadata.spawnPoints![0].position;
            Assert.IsNull(position.x.SingleValue);
            Assert.IsNull(position.x.MultiValue);
            Assert.AreEqual(0f, position.y.SingleValue);
            CollectionAssert.AreEqual(new[] { 1f, 2f }, position.z.MultiValue);
        }

        [Test]
        public void DeserializeEmptySpawnPointCoordinateRangeAsEmpty()
        {
            // Arrange
            const string JSON = @"{""main"": ""bin/index.js"", ""spawnPoints"": [{""name"": ""spawn"", ""position"": {""x"": [], ""y"": 1.5, ""z"": 2}}]}";

            // Act
            SceneMetadata metadata = JsonConvert.DeserializeObject<SceneMetadata>(JSON)!;

            // Assert
            SceneMetadata.SpawnPoint.Position position = metadata.spawnPoints![0].position;
            Assert.IsNull(position.x.SingleValue);
            CollectionAssert.IsEmpty(position.x.MultiValue);
            Assert.AreEqual(1.5f, position.y.SingleValue);
            Assert.AreEqual(2f, position.z.SingleValue);
        }
    }
}
