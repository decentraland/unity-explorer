using DCL.Ipfs;
using ECS.SceneLifeCycle;
using NUnit.Framework;
using System;
using System.Text;

namespace DCL.SceneLifeCycle.Tests
{
    public class ECSReloadSceneShould
    {
        [Test]
        public void TreatHashWithEmbeddedMtimeAsContentVersioned()
        {
            //Arrange: the dev server encodes "{path}\0{mtimeMs}-{machineId}" — the NUL marks the versioned format
            SceneEntityDefinition definition = CreateDefinition(
                new ContentDefinition { file = "models/shark.glb", hash = VersionedHash("/project/models/shark.glb", 1_699_999_999_999, "machine-1") });

            //Act & Assert
            Assert.That(ECSReloadScene.IsContentVersioned(definition), Is.True);
        }

        [Test]
        public void NotTreatPathOnlyHashAsContentVersioned()
        {
            //Arrange: older dev servers encode "{path}-{machineId}" with no NUL — an edit keeps the hash
            SceneEntityDefinition definition = CreateDefinition(
                new ContentDefinition { file = "models/shark.glb", hash = PathOnlyHash("/project/models/shark.glb", "machine-1") });

            //Act & Assert
            Assert.That(ECSReloadScene.IsContentVersioned(definition), Is.False);
        }

        [Test]
        public void NotTreatMissingOrNonB64ContentAsContentVersioned()
        {
            Assert.That(ECSReloadScene.IsContentVersioned(null), Is.False);
            Assert.That(ECSReloadScene.IsContentVersioned(CreateDefinition()), Is.False);

            //production content-addressed hashes are not b64- prefixed
            Assert.That(ECSReloadScene.IsContentVersioned(
                CreateDefinition(new ContentDefinition { file = "models/shark.glb", hash = "bafkreihdwdcefgh4dqkjv67uzcmw7ojee6xedzdetojuzjevtenxquvyku" })), Is.False);
        }

        private static string PathOnlyHash(string path, string machineId) =>
            "b64-" + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{path}-{machineId}"));

        private static string VersionedHash(string path, long mtimeMs, string machineId) =>
            "b64-" + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{path}\0{mtimeMs}-{machineId}"));

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

        [Test]
        public void ResolveContentHashBySrcIgnoringCase()
        {
            //Arrange
            SceneEntityDefinition definition = CreateDefinition(
                new ContentDefinition { file = "models/shark.glb", hash = "b64-content-hash" });

            //Act
            bool resolved = ECSReloadScene.TryResolveContentHash(definition, "Models/Shark.GLB", out string hash);

            //Assert
            Assert.That(resolved, Is.True);
            Assert.That(hash, Is.EqualTo("b64-content-hash"));
        }

        [Test]
        public void ResolveContentHashWhenSrcUsesWindowsSeparators()
        {
            //Arrange: content mappings always spell paths with '/', but the local dev server's file
            //watcher reports the platform separator — on Windows that is '\'.
            SceneEntityDefinition definition = CreateDefinition(
                new ContentDefinition { file = "assets/models/out/models/BenchStreet.glb", hash = "b64-content-hash" });

            //Act
            bool resolved = ECSReloadScene.TryResolveContentHash(definition, @"assets\models\out\models\BenchStreet.glb", out string hash);

            //Assert
            Assert.That(resolved, Is.True);
            Assert.That(hash, Is.EqualTo("b64-content-hash"));
        }

        [Test]
        public void NotResolveContentHashWhenSrcIsUnknownOrMissing()
        {
            //Arrange
            SceneEntityDefinition definition = CreateDefinition(
                new ContentDefinition { file = "models/shark.glb", hash = "b64-content-hash" });

            //Act & Assert
            Assert.That(ECSReloadScene.TryResolveContentHash(definition, "models/monster.glb", out _), Is.False);
            Assert.That(ECSReloadScene.TryResolveContentHash(definition, string.Empty, out _), Is.False);
            Assert.That(ECSReloadScene.TryResolveContentHash(null, "models/shark.glb", out _), Is.False);
        }

        private static SceneEntityDefinition CreateDefinition(params ContentDefinition[] content) =>
            new ("test-scene", new SceneMetadata())
            {
                pointers = new[] { "0,0" },
                content = content,
            };
    }
}
