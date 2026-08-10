using DCL.Ipfs;
using ECS.SceneLifeCycle;
using NUnit.Framework;
using System;

namespace DCL.SceneLifeCycle.Tests
{
    public class ECSReloadSceneShould
    {
        [Test]
        public void TreatDefinitionWithoutManifestAsRawGltf()
        {
            //Arrange: no asset-bundle manifest -> the container cache is keyed by the bare hash
            SceneEntityDefinition definition = CreateDefinition();

            //Act & Assert
            Assert.That(ECSReloadScene.IsRawGltfModel(definition, "b64-somehash"), Is.True);
        }

        [Test]
        public void NotTreatMissingDefinitionOrEmptyHashAsRawGltf()
        {
            SceneEntityDefinition definition = CreateDefinition();

            Assert.That(ECSReloadScene.IsRawGltfModel(null, "b64-somehash"), Is.False);
            Assert.That(ECSReloadScene.IsRawGltfModel(definition, string.Empty), Is.False);
            Assert.That(ECSReloadScene.IsRawGltfModel(definition, null!), Is.False);
        }

        private static SceneEntityDefinition CreateDefinition() =>
            new ("test-scene", new SceneMetadata())
            {
                pointers = new[] { "0,0" },
                content = Array.Empty<ContentDefinition>(),
            };
    }
}
