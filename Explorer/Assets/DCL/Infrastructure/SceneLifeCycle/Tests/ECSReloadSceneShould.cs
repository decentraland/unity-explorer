using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.ResourcesUnloading;
using DCL.WebRequests;
using ECS;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Utility;

namespace DCL.SceneLifeCycle.Tests
{
    public class ECSReloadSceneShould
    {
        private World world = null!;
        private ECSReloadScene reloadScene = null!;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();

            reloadScene = new ECSReloadScene(Substitute.For<IScenesCache>(), world, world.Create(),
                localSceneDevelopment: true, Substitute.For<ICacheCleaner>(),
                Substitute.For<IWebRequestController>(), Substitute.For<IDecentralandUrlsSource>());
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void AdoptRefreshedContentWhenParcelsUnchanged()
        {
            //Arrange
            SceneEntityDefinition cached = CreateDefinition(new[] { "0,0" }, new[] { new ContentDefinition { file = "old.glb", hash = "b64-old" } });
            Entity entity = world.Create(CreateDefinitionComponent(cached));

            SceneEntityDefinition refreshed = CreateDefinition(new[] { "0,0" }, new[] { new ContentDefinition { file = "new.glb", hash = "b64-new" } });

            //Act
            reloadScene.ApplyRefreshedDefinition(entity, refreshed);

            //Assert
            Assert.That(world.IsAlive(entity), Is.True);
            Assert.That(cached.content, Is.SameAs(refreshed.content));
        }

        [Test]
        public void KeepCachedContentWhenRefreshFailed()
        {
            //Arrange
            ContentDefinition[] cachedContent = { new () { file = "old.glb", hash = "b64-old" } };
            SceneEntityDefinition cached = CreateDefinition(new[] { "0,0" }, cachedContent);
            Entity entity = world.Create(CreateDefinitionComponent(cached));

            //Act
            reloadScene.ApplyRefreshedDefinition(entity, null);

            //Assert
            Assert.That(world.IsAlive(entity), Is.True);
            Assert.That(cached.content, Is.SameAs(cachedContent));
        }

        [Test]
        public void FallBackToRediscoveryWhenParcelsChanged()
        {
            //Arrange
            var promise = AssetPromise<SceneDefinitions, GetSceneDefinitionList>.Create(world,
                new GetSceneDefinitionList(new List<SceneEntityDefinition>(), new List<int2>(),
                    new CommonLoadingArguments(URLAddress.FromString("http://localhost/entities/active"))),
                PartitionComponent.TOP_PRIORITY);

            Entity realmEntity = world.Create(new RealmComponent(new RealmData(new TestIpfsRealm())),
                new StaticScenePointers(new List<int2>()) { Promise = promise });

            SceneEntityDefinition cached = CreateDefinition(new[] { "0,0" }, new[] { new ContentDefinition { file = "old.glb", hash = "b64-old" } });
            Entity entity = world.Create(CreateDefinitionComponent(cached));

            SceneEntityDefinition refreshed = CreateDefinition(new[] { "0,0", "0,1" }, new[] { new ContentDefinition { file = "old.glb", hash = "b64-old" } });

            //Act
            reloadScene.ApplyRefreshedDefinition(entity, refreshed);

            //Assert
            Assert.That(world.IsAlive(entity), Is.False);
            Assert.That(world.Get<StaticScenePointers>(realmEntity).Promise, Is.Null);
        }

        [Test]
        public void DetectContentVersionedIds()
        {
            //Arrange: a NUL byte separates path from mtime in version-capable server ids
            string versionedId = "b64-" + EncodeBase64("/home/user/scene/assets/tree.glb\u00001786032377138-my-host.local");
            SceneEntityDefinition definition = CreateDefinition(new[] { "0,0" }, new[] { new ContentDefinition { file = "tree.glb", hash = versionedId } });

            //Act & Assert
            Assert.That(ECSReloadScene.HasContentVersionedIds(definition), Is.True);
        }

        [Test]
        public void DetectContentVersionedIdsWithUrlSafeAlphabet()
        {
            //Arrange
            string versionedId = "b64-" + EncodeBase64("/home/user/scene/assets/tree.glb\u00001786032377138-my-host.local")
               .Replace('+', '-').Replace('/', '_').TrimEnd('=');

            SceneEntityDefinition definition = CreateDefinition(new[] { "0,0" }, new[] { new ContentDefinition { file = "tree.glb", hash = versionedId } });

            //Act & Assert
            Assert.That(ECSReloadScene.HasContentVersionedIds(definition), Is.True);
        }

        [Test]
        public void NotDetectVersionedIdsOnLegacyPathOnlyIds()
        {
            //Arrange: legacy ids are base64(path-machineId) — no NUL byte can occur
            string legacyId = "b64-" + EncodeBase64("/home/user/scene/assets/tree.glb-my-host.local");
            SceneEntityDefinition definition = CreateDefinition(new[] { "0,0" }, new[] { new ContentDefinition { file = "tree.glb", hash = legacyId } });

            //Act & Assert
            Assert.That(ECSReloadScene.HasContentVersionedIds(definition), Is.False);
        }

        [TestCase("bafybeigdyrzt5sfp7udm7hu76uh7y26nf3efuylqabf3oclgtqy55fbzdi")]
        [TestCase("b64-%%%not-base64%%%")]
        [TestCase("")]
        public void NotDetectVersionedIdsOnMalformedOrForeignIds(string hash)
        {
            //Arrange
            SceneEntityDefinition definition = CreateDefinition(new[] { "0,0" }, new[] { new ContentDefinition { file = "tree.glb", hash = hash } });

            //Act & Assert
            Assert.That(ECSReloadScene.HasContentVersionedIds(definition), Is.False);
        }

        [Test]
        public void NotDetectVersionedIdsOnMissingDefinitionOrContent()
        {
            Assert.That(ECSReloadScene.HasContentVersionedIds(null), Is.False);
            Assert.That(ECSReloadScene.HasContentVersionedIds(CreateDefinition(new[] { "0,0" }, System.Array.Empty<ContentDefinition>())), Is.False);
        }

        [Test]
        public void TreatDefinitionWithoutManifestAsRawGltf()
        {
            //Arrange: no asset-bundle manifest -> the container cache is keyed by the bare hash
            SceneEntityDefinition definition = CreateDefinition(new[] { "0,0" }, System.Array.Empty<ContentDefinition>());

            //Act & Assert
            Assert.That(ECSReloadScene.IsRawGltfModel(definition, "b64-somehash"), Is.True);
        }

        [Test]
        public void NotTreatMissingDefinitionOrEmptyHashAsRawGltf()
        {
            SceneEntityDefinition definition = CreateDefinition(new[] { "0,0" }, System.Array.Empty<ContentDefinition>());

            Assert.That(ECSReloadScene.IsRawGltfModel(null, "b64-somehash"), Is.False);
            Assert.That(ECSReloadScene.IsRawGltfModel(definition, string.Empty), Is.False);
            Assert.That(ECSReloadScene.IsRawGltfModel(definition, null!), Is.False);
        }

        private static string EncodeBase64(string value) =>
            System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));

        private static SceneEntityDefinition CreateDefinition(string[] pointers, ContentDefinition[] content) =>
            new ("test-scene", new SceneMetadata())
            {
                pointers = pointers,
                content = content,
            };

        private static SceneDefinitionComponent CreateDefinitionComponent(SceneEntityDefinition definition) =>
            new (definition, new List<Vector2Int>(), new List<ParcelMathHelper.ParcelCorners>(),
                default(ParcelMathHelper.SceneGeometry), default(IpfsPath), isSDK7: true, isPortableExperience: false);
    }
}
