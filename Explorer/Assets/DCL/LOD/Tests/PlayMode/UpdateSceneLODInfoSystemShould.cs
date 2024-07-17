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

namespace DCL.LOD.Tests
{
    public class UpdateSceneLODInfoSystemShould : UnitySystemTestBase<UpdateSceneLODInfoSystem>
    {
        private SceneLODInfo sceneLODInfo;
        private LODAssetsPool lodAssetsPool;
        private GameObjectPool<LODGroup> lodGroupPool;
        private PartitionComponent partitionComponent;
        private SceneDefinitionComponent sceneDefinitionComponent;

        private const string fakeHash = "FAKE_HASH";

        [SetUp]
        public void Setup()
        {
            var lodSettings = Substitute.For<ILODSettingsAsset>();
            int[] bucketThresholds =
            {
                2
            };
            lodSettings.LodPartitionBucketThresholds.Returns(bucketThresholds);

            var scenesCache = Substitute.For<IScenesCache>();
            var sceneReadinessReportQueue = Substitute.For<ISceneReadinessReportQueue>();

            partitionComponent = new PartitionComponent();

            var sceneEntityDefinition = new SceneEntityDefinition
            {
                id = fakeHash, metadata = new SceneMetadata
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

            sceneLODInfo = SceneLODInfo.Create();
            lodAssetsPool = new LODAssetsPool();

            system = new UpdateSceneLODInfoSystem(world, lodGroupPool, lodAssetsPool, lodSettings, scenesCache, sceneReadinessReportQueue, new GameObject("LODS").transform);
        }


        [Test]
        //Note: Test modified due to LOD level always defaulting to 3 while we rebuild all of them
        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(2, 1)]
        [TestCase(3, 1)]
        [TestCase(4, 1)]
        [TestCase(10, 1)]
        public void ResolveLODLevel(byte bucket, int expectedLODLevel)
        {
            //Arrange
            partitionComponent.IsDirty = true;
            partitionComponent.Bucket = bucket;
            var entity = world.Create(sceneLODInfo, partitionComponent, sceneDefinitionComponent);

            //Act
            system.Update(0);
        }






        /*
   TODO: Uncomment when LOD Async Instantiation is back up
  [Test]
  public void ResolvePromiseAndDontInstantiate()
  {
      var frameCapBudget = Substitute.For<IPerformanceBudget>();
      frameCapBudget.TrySpendBudget().Returns(true, false);

      system.frameCapBudget = frameCapBudget;

      //Arrange
      var promiseGenerated = GenerateLODPromise();
      sceneLODInfo.CurrentLODPromise = promiseGenerated.Item2;
      sceneLODInfo.CurrentLODLevel = 1;
      sceneLODInfo.IsDirty = true;
      var sceneLodInfoEntity = world.Create(sceneLODInfo, sceneDefinitionComponent);

      //Act
      system.Update(0);

      //Assert
      var sceneLODInfoRetrieved = world.Get<SceneLODInfo>(sceneLodInfoEntity);
      Assert.IsTrue(sceneLODInfoRetrieved.IsDirty);
      Assert.Null(sceneLODInfoRetrieved.CurrentLOD?.Root);
      Assert.AreNotEqual(sceneLODInfoRetrieved.CurrentLOD, sceneLODInfoRetrieved.CurrentVisibleLOD);
      Assert.AreEqual(new LODKey(fakeHash, 1), sceneLODInfoRetrieved.CurrentLOD!.LodKey);
      Assert.AreEqual(promiseGenerated.Item1, sceneLODInfoRetrieved.CurrentLOD!.AssetBundleReference);
  }
  */



    }
}
