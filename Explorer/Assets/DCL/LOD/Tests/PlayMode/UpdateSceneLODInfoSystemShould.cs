using System.Collections.Generic;
using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.Ipfs;
using DCL.LOD.Components;
using DCL.LOD.Systems;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData,
    ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.LOD.Tests
{
    public class UpdateSceneLODInfoSystemShould : UnitySystemTestBase<UpdateSceneLODInfoSystem>
    {
        private SceneLODInfo sceneLODInfo;
        private LODAssetsPool lodAssetsPool;
        private PartitionComponent partitionComponent;
        private SceneDefinitionComponent sceneDefinitionComponent;


        private const string fakeHash = "FAKE_HASH";


        [SetUp]
        public void Setup()
        {
            var lodSettings = Substitute.For<ILODSettingsAsset>();
            int[] bucketThresholds =
            {
                2, 4
            };
            lodSettings.LodPartitionBucketThresholds.Returns(bucketThresholds);

            var frameCapBudget = Substitute.For<IPerformanceBudget>();
            frameCapBudget.TrySpendBudget().Returns(true);

            var memoryBudget = Substitute.For<IPerformanceBudget>();
            memoryBudget.TrySpendBudget().Returns(true);

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

            var textureArrayContainerFactory = new TextureArrayContainerFactory(new Dictionary<TextureArrayKey, Texture>());
            system = new UpdateSceneLODInfoSystem(world, lodAssetsPool, lodSettings, memoryBudget, frameCapBudget, scenesCache, sceneReadinessReportQueue, new GameObject("LODS").transform,
                textureArrayContainerFactory.Create(TextureArrayConstants.SCENE_TEX_ARRAY_SHADER, new []
                {
                    new TextureArrayResolutionDescriptor(256, 500)
                }, TextureFormat.BC7, 20));
        }


        [Test]
        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(2, 1)]
        [TestCase(3, 1)]
        [TestCase(4, 2)]
        [TestCase(10, 2)]
        public void ResolveLODLevel(byte bucket, int expectedLODLevel)
        {
            //Arrange
            partitionComponent.IsDirty = true;
            partitionComponent.Bucket = bucket;
            var entity = world.Create(sceneLODInfo, partitionComponent, sceneDefinitionComponent);

            //Act
            system.Update(0);


            //Assert
            Assert.AreEqual(expectedLODLevel, world.Get<SceneLODInfo>(entity).CurrentLODLevel);
        }


        [Test]
        public void ResolveLODPromise()
        {
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
            Assert.NotNull(sceneLODInfoRetrieved.CurrentLOD?.Root);
            Assert.AreEqual(new LODKey(fakeHash, 1), sceneLODInfoRetrieved.CurrentLOD!.LodKey);
            Assert.AreEqual(promiseGenerated.Item1, sceneLODInfoRetrieved.CurrentLOD!.AssetBundleReference);
        }

        [Test]
        public void UpdateCache()
        {
            //Arrange
            var promiseGenerated = GenerateLODPromise();
            sceneLODInfo.CurrentLODPromise = promiseGenerated.Item2;
            sceneLODInfo.CurrentLODLevel = 1;
            sceneLODInfo.IsDirty = true;
            world.Create(sceneLODInfo, sceneDefinitionComponent);
            system.Update(0);

            world.Query(new QueryDescription().WithAll<SceneLODInfo>(),
                (ref SceneLODInfo sceneLODInfo) =>
                {
                    var newPromiseGenerated = GenerateLODPromise();
                    sceneLODInfo.CurrentLODLevel = 2;
                    sceneLODInfo.CurrentLODPromise = newPromiseGenerated.Item2;
                    sceneLODInfo.IsDirty = true;
                });

            //Act
            system.Update(0);

            //Assert
            Assert.AreEqual(lodAssetsPool.vacantInstances.Count, 1);
        }

        private (AssetBundleData, Promise) GenerateLODPromise()
        {
            var promise = Promise.Create(world,
                GetAssetBundleIntention.FromName("Cube"),
                new PartitionComponent());

            var fakeAssetBundleData = new AssetBundleData(null, null, GameObject.CreatePrimitive(PrimitiveType.Cube),
                new AssetBundleData[]
                {
                });

            world.Add(promise.Entity,
                new StreamableLoadingResult<AssetBundleData>(fakeAssetBundleData));
            return (fakeAssetBundleData, promise);
        }
    }
}