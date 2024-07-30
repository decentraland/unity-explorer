using DCL.Ipfs;
using DCL.LOD.Components;
using DCL.LOD.Systems;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Assert = UnityEngine.Assertions.Assert;

namespace DCL.LOD.Tests
{
    public class InitializeSceneLODInfoShould : UnitySystemTestBase<InitializeSceneLODInfoSystem>
    {
        private ILODCache lodCache;
        private const int LOD_LEVELS = 2;
        private const string fakeSceneIDCached = "fakeSceneIDCached";
        private const string fakeSceneIDMisssing = "fakeSceneIDMissing";

        private LODCacheInfo cahedInfo;
        private LODCacheInfo newInfo;


        private SceneLODInfo sceneLODInfo;
        private PartitionComponent partitionComponent;
        private SceneDefinitionComponent sceneDefinitionComponent;

        [SetUp]
        public void SetUp()
        {
            cahedInfo = new LODCacheInfo();
            lodCache = Substitute.For<ILODCache>();
            lodCache.Get(fakeSceneIDCached, LOD_LEVELS).Returns(cahedInfo);
            lodCache.Get(fakeSceneIDMisssing, LOD_LEVELS).Returns(newInfo);

            var sceneEntityDefinition = new SceneEntityDefinition
            {
                metadata = new SceneMetadata
                {
                    scene = new SceneMetadataScene
                    {
                        DecodedBase = new Vector2Int(0, 0), DecodedParcels = new Vector2Int[]
                        {
                            new (0, 0), new (0, 1), new (1, 0), new (2, 0), new (2, 1), new (3, 0), new (3, 1)
                        }
                    }
                }
            };
            sceneDefinitionComponent = new SceneDefinitionComponent(sceneEntityDefinition, new IpfsPath());
            partitionComponent = new PartitionComponent();
            system = new InitializeSceneLODInfoSystem(world, lodCache, LOD_LEVELS);
        }

        [Test]
        public void GotSceneMetadataFromCache()
        {
            //Arrange
            partitionComponent.IsDirty = true;
            partitionComponent.Bucket = 0;
            sceneDefinitionComponent.Definition.id = fakeSceneIDCached;
            var entity = world.Create(sceneLODInfo, partitionComponent, sceneDefinitionComponent);

            //Act
            system.Update(0);

            //Assert
            var sceneLODInfoRetrieved = world.Get<SceneLODInfo>(entity);
            Assert.AreEqual(sceneLODInfoRetrieved.id, fakeSceneIDCached);
            Assert.AreEqual(sceneLODInfoRetrieved.metadata, cahedInfo);
        }

        [Test]
        public void GotNewSceneMetadataFromCache()
        {
            //Arrange
            partitionComponent.IsDirty = true;
            partitionComponent.Bucket = 0;
            sceneDefinitionComponent.Definition.id = fakeSceneIDMisssing;
            var entity = world.Create(sceneLODInfo, partitionComponent, sceneDefinitionComponent);

            //Act
            system.Update(0);

            //Assert
            var sceneLODInfoRetrieved = world.Get<SceneLODInfo>(entity);
            Assert.AreEqual(sceneLODInfoRetrieved.id, fakeSceneIDMisssing);
            Assert.AreEqual(sceneLODInfoRetrieved.metadata, newInfo);
        }
    }
}