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
