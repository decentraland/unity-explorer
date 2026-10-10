using DCL.Ipfs;
using DCL.LOD.Components;
using DCL.LOD.Systems;
using DCL.Optimization.Pools;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Reporting;
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

        private LODCacheInfo cachedInfo;

        private SceneLODInfo sceneLODInfo;
        private PartitionComponent partitionComponent;
        private SceneDefinitionComponent sceneDefinitionComponent;

        [SetUp]
        public void SetUp()
        {
            IComponentPool<LODGroup> lodGroupPool = new GameObjectPool<LODGroup>(new GameObject().transform);

            cachedInfo = new LODCacheInfo(lodGroupPool.Get(), 2);
            cachedInfo.SuccessfullLODs = 1;
            lodCache = Substitute.For<ILODCache>();

            lodCache.TryGet(fakeSceneIDCached, out Arg.Any<LODCacheInfo>()).Returns(call =>
            {
                call[1] = cachedInfo;
                return true;
            });

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

            sceneDefinitionComponent = SceneDefinitionComponentFactory.CreateFromDefinition(sceneEntityDefinition, new IpfsPath());
            partitionComponent = new PartitionComponent();
            system = new InitializeSceneLODInfoSystem(world, lodCache, LOD_LEVELS, lodGroupPool,
                new GameObject().transform, Substitute.For<ISceneReadinessReportQueue>(),
                Substitute.For<IScenesCache>());
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
            Assert.AreEqual(sceneLODInfoRetrieved.metadata, cachedInfo);
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
            Assert.AreEqual(sceneLODInfoRetrieved.metadata.SuccessfullLODs, (byte)0);
            Assert.AreEqual(sceneLODInfoRetrieved.metadata.FailedLODs, (byte)0);
        }
    }
}
