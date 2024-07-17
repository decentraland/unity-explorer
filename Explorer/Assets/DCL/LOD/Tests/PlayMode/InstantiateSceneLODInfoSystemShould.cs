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
    public class InstantiateSceneLODInfoSystemShould : UnitySystemTestBase<InstantiateSceneLODInfoSystem>
    {
        private SceneLODInfo sceneLODInfo;
        private LODAssetsPool lodAssetsPool;
        private GameObjectPool<LODGroup> lodGroupPool;
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


            var frameCapBudget = Substitute.For<IPerformanceBudget>();
            frameCapBudget.TrySpendBudget().Returns(true);

            var memoryBudget = Substitute.For<IPerformanceBudget>();
            memoryBudget.TrySpendBudget().Returns(true);

            var scenesCache = Substitute.For<IScenesCache>();
            var sceneReadinessReportQueue = Substitute.For<ISceneReadinessReportQueue>();

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
            system = new InstantiateSceneLODInfoSystem(world,  frameCapBudget, memoryBudget, lodGroupPool, lodAssetsPool, scenesCache, sceneReadinessReportQueue,
                textureArrayContainerFactory.CreateSceneLOD(TextureArrayConstants.SCENE_TEX_ARRAY_SHADER, new []
                {
                    new TextureArrayResolutionDescriptor(256, 500, 1)
                }, TextureFormat.BC7, 20, 1),
                new GameObject("LODS").transform);
        }

        [Test]
        public void ResolvePromiseAndInstantiate()
        {
            //Arrange
            var promiseGenerated = GenerateLODPromise();
            LODAsset testLODAsset = new LODAsset(new LODKey(fakeHash, 1), lodAssetsPool);
            testLODAsset.currentLODLevel = 1;
            sceneLODInfo.LODAssets.Add(testLODAsset);
            testLODAsset.LODPromise = promiseGenerated.Item2;
            var sceneLodInfoEntity = world.Create(sceneLODInfo, sceneDefinitionComponent);

            //Act
            system.Update(0);

            //Assert
            var sceneLODInfoRetrieved = world.Get<SceneLODInfo>(sceneLodInfoEntity);
            Assert.NotNull(sceneLODInfoRetrieved.LODAssets[0]!.lodGO);
            Assert.AreEqual(new LODKey(fakeHash, 1), sceneLODInfoRetrieved.LODAssets[0]!.LodKey);
            Assert.AreEqual(promiseGenerated.Item1, sceneLODInfoRetrieved.LODAssets[0]!.AssetBundleReference);
        }

        [Test]
        public void UpdateCache()
        {
            //Arrange
            LODAsset testLODAsset = new LODAsset(new LODKey(fakeHash, 1), lodAssetsPool);

            testLODAsset.currentLODLevel = 1;
            sceneLODInfo.LODAssets.Add(testLODAsset);

            var promiseGenerated = GenerateLODPromise();
            testLODAsset.LODPromise = promiseGenerated.Item2;

            //sceneLODInfo.IsDirty = true;
            world.Create(sceneLODInfo, sceneDefinitionComponent);
            system.Update(0);
            byte lodPromiseArrayIndex = (byte)(sceneLODInfo.LODAssets[0].currentLODLevel - 1); // We're not using 0 for RAW mesh yet, so it's adjusted

            world.Query(new QueryDescription().WithAll<SceneLODInfo>(),
                (ref SceneLODInfo sceneLODInfo) =>
                {
                    var newPromiseGenerated = GenerateLODPromise();
                    sceneLODInfo.LODAssets[0].currentLODLevel = 2;
                    lodPromiseArrayIndex = (byte)(sceneLODInfo.LODAssets[0].currentLODLevel - 1); // We're not using 0 for RAW mesh yet, so it's adjusted
                    sceneLODInfo.LODAssets[0].LODPromise = newPromiseGenerated.Item2;
                });

            //Act
            system.Update(0);

            //Assert
            Assert.AreEqual(lodAssetsPool.vacantInstances.Count, 1);
        }

        private (AssetBundleData, Promise) GenerateLODPromise()
        {
            var promise = Promise.Create(world,
                GetAssetBundleIntention.FromHash(typeof(GameObject),"Cube"),
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
