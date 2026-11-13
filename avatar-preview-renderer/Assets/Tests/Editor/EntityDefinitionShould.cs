using Data;
using NUnit.Framework;

namespace Tests
{
    public class EntityDefinitionShould
    {
        [Test]
        public void KeepAWearableWithoutTheFlagOutlined()
        {
            // Act
            var definition = EntityDefinition.FromDefinitionJson(WearableJson(""));

            // Assert
            Assert.IsTrue(definition.OutlineCompatible);
        }

        [Test]
        public void LeaveAWearableThatOptsOutOfTheOutlineUnoutlined()
        {
            // Act
            var definition = EntityDefinition.FromDefinitionJson(WearableJson("\"outlineCompatible\":false,"));

            // Assert
            Assert.IsFalse(definition.OutlineCompatible);
        }

        private static string WearableJson(string outlineField) =>
            "{\"id\":\"urn:test:earrings\",\"data\":{\"category\":\"earring\"," + outlineField +
            "\"representations\":[{\"bodyShapes\":[\"" + WearablesConstants.BODY_SHAPE_MALE + "\"]," +
            "\"mainFile\":\"m.glb\",\"contents\":[{\"key\":\"m.glb\",\"url\":\"https://example.com/QmFake\"}]}]}}";
    }
}
