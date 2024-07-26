using System.Collections.Generic;
using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.Ipfs;
using DCL.LOD.Components;
using DCL.LOD.Systems;
using DCL.Optimization.PerformanceBudgeting;
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
        private const string fakeHash = "FAKE_HASH";
        private SceneLODInfo sceneLODInfo;
        private LODAssetsPool lodAssetsPool;
        private SceneDefinitionComponent sceneDefinitionComponent;

        [SetUp]
        public void Setup()
        {
            ILODSettingsAsset? lodSettings = Substitute.For<ILODSettingsAsset>();

            int[] bucketThresholds =
            {
                2,
            };

            lodSettings.LodPartitionBucketThresholds.Returns(bucketThresholds);

            IPerformanceBudget? frameCapBudget = Substitute.For<IPerformanceBudget>();
            frameCapBudget.TrySpendBudget().Returns(true);

            IPerformanceBudget? memoryBudget = Substitute.For<IPerformanceBudget>();
            memoryBudget.TrySpendBudget().Returns(true);

            IScenesCache? scenesCache = Substitute.For<IScenesCache>();
            ISceneReadinessReportQueue? sceneReadinessReportQueue = Substitute.For<ISceneReadinessReportQueue>();

            var sceneEntityDefinition = new SceneEntityDefinition
            {
                id = fakeHash, metadata = new SceneMetadata
                {
                    scene = new SceneMetadataScene
                    {
                        DecodedBase = new Vector2Int(0, 0), DecodedParcels = new Vector2Int[]
                        {
                            new (0, 0), new (0, 1), new (1, 0), new (2, 0), new (2, 1), new (3, 0), new (3, 1),
                        },
                    },
                },
            };

            sceneDefinitionComponent = SceneDefinitionComponentFactory.CreateFromDefinition(sceneEntityDefinition, new IpfsPath());

            sceneLODInfo = SceneLODInfo.Create();
            lodAssetsPool = new LODAssetsPool();

            var textureArrayContainerFactory = new TextureArrayContainerFactory(new Dictionary<TextureArrayKey, Texture>());

            system = new InstantiateSceneLODInfoSystem(world, frameCapBudget, memoryBudget, lodAssetsPool, scenesCache, sceneReadinessReportQueue,
                textureArrayContainerFactory.CreateSceneLOD(TextureArrayConstants.SCENE_TEX_ARRAY_SHADER, new[]
                {
                    new TextureArrayResolutionDescriptor(256, 500, 1),
                }, TextureFormat.BC7, 20, 1),
                new GameObject("LODS").transform);
        }

        [Test]
        public void ResolvePromiseAndInstantiate()
        {
            //Arrange
            (AssetBundleData, Promise) promiseGenerated = GenerateLODPromise();
            sceneLODInfo.CurrentLODPromise = promiseGenerated.Item2;
            sceneLODInfo.CurrentLODLevel = 1;
            sceneLODInfo.IsDirty = true;
            Entity sceneLodInfoEntity = world.Create(sceneLODInfo, sceneDefinitionComponent);

            //Act
            system.Update(0);

            //Assert
            SceneLODInfo sceneLODInfoRetrieved = world.Get<SceneLODInfo>(sceneLodInfoEntity);
            Assert.IsFalse(sceneLODInfoRetrieved.IsDirty);
            Assert.NotNull(sceneLODInfoRetrieved.CurrentLOD?.Root);
            Assert.AreEqual(sceneLODInfoRetrieved.CurrentLOD, sceneLODInfoRetrieved.CurrentVisibleLOD);
            Assert.AreEqual(new LODKey(fakeHash, 1), sceneLODInfoRetrieved.CurrentLOD!.LodKey);
            Assert.AreEqual(promiseGenerated.Item1, sceneLODInfoRetrieved.CurrentLOD!.AssetBundleReference);
        }

        [Test]
        public void UpdateCache()
        {
            //Arrange
            (AssetBundleData, Promise) promiseGenerated = GenerateLODPromise();
            sceneLODInfo.CurrentLODPromise = promiseGenerated.Item2;
            sceneLODInfo.CurrentLODLevel = 1;
            sceneLODInfo.IsDirty = true;
            world.Create(sceneLODInfo, sceneDefinitionComponent);
            system.Update(0);

            world.Query(new QueryDescription().WithAll<SceneLODInfo>(),
                (ref SceneLODInfo sceneLODInfo) =>
                {
                    (AssetBundleData, Promise) newPromiseGenerated = GenerateLODPromise();
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
                GetAssetBundleIntention.FromHash(typeof(GameObject),"Cube"),
                new PartitionComponent());

            var fakeAssetBundleData = new AssetBundleData(null, null, GameObject.CreatePrimitive(PrimitiveType.Cube),
                new AssetBundleData[]
                    { });

            world.Add(promise.Entity,
                new StreamableLoadingResult<AssetBundleData>(fakeAssetBundleData));

            return (fakeAssetBundleData, promise);
        }
    }
}
