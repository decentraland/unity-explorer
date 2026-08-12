using Arch.Core;
using System;
using System.Collections.Generic;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.LOD.Components;
using DCL.LOD.Systems;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System.Linq;
using UnityEngine;
using UnityEngine.TestTools;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData,
    ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.LOD.Tests
{
    public class InstantiateSceneLODInfoSystemShould : UnitySystemTestBase<InstantiateSceneLODInfoSystem>
    {
        private const string FAKE_HASH = "FAKE_HASH";

        private static readonly Vector2Int[] DECODED_PARCELS =
        {
            new (0, 0),
        };

        private SceneLODInfo sceneLODInfo;
        private GameObjectPool<LODGroup>? lodGroupPool;
        private SceneDefinitionComponent sceneDefinitionComponent;
                private IScenesCache scenesCache = null!;

        [SetUp]
        public void Setup()
        {
            var lodSettings = Substitute.For<ILODSettingsAsset>();

            int[] bucketThresholds =
            {
                2,
            };

            lodSettings.LodPartitionBucketThresholds.Returns(bucketThresholds);

            var frameCapBudget = Substitute.For<IPerformanceBudget>();
            frameCapBudget.TrySpendBudget().Returns(true);

            IPerformanceBudget? memoryBudget = Substitute.For<IPerformanceBudget>();
            memoryBudget.TrySpendBudget().Returns(true);

            scenesCache = Substitute.For<IScenesCache>();
            var sceneReadinessReportQueue = Substitute.For<ISceneReadinessReportQueue>();

            var sceneEntityDefinition = new SceneEntityDefinition
            {
                id = FAKE_HASH, metadata = new SceneMetadata
                {
                    scene = new SceneMetadataScene
                    {
                        DecodedBase = new Vector2Int(0, 0), DecodedParcels = DECODED_PARCELS
                    }
                }
            };

            sceneDefinitionComponent = SceneDefinitionComponentFactory.CreateFromDefinition(sceneEntityDefinition, new IpfsPath());

            sceneLODInfo = SceneLODInfo.Create();
            sceneLODInfo.metadata = new LODCacheInfo(new GameObject().AddComponent<LODGroup>(), 2);

            var textureArrayContainerFactory = new TextureArrayContainerFactory(new Dictionary<TextureArrayKey, Texture>());

            system = new InstantiateSceneLODInfoSystem(world, frameCapBudget, memoryBudget, scenesCache, sceneReadinessReportQueue,
                textureArrayContainerFactory.CreateSceneLOD(TextureArrayConstants.SCENE_TEX_ARRAY_SHADER, new[]
                {
                    new TextureArrayResolutionDescriptor(256, 500, 1)
                }, TextureFormat.BC7, 20, 1), Substitute.For<IRealmPartitionSettings>());
        }

        [Test]
        public void ResolveSuccessfullPromiseAndInstantiate()
        {
            LogAssert.ignoreFailingMessages = true;

            //Arrange
            var promiseGenerated = GenerateSuccessfullPromise();
            sceneLODInfo.CurrentLODPromise = promiseGenerated.Item2;
            sceneLODInfo.CurrentLODLevelPromise = 0;
            sceneLODInfo.id = "scene";
            Entity sceneLodInfoEntity = world.Create(sceneLODInfo, sceneDefinitionComponent);

            //Act
            system!.Update(0);

            //Assert
            var sceneLODInfoRetrieved = world.Get<SceneLODInfo>(sceneLodInfoEntity);
            Assert.NotNull(sceneLODInfoRetrieved.metadata.LODAssets[0]!.Root);
            Assert.AreEqual(promiseGenerated.Item1, sceneLODInfoRetrieved.metadata.LODAssets[0]!.AssetBundleReference);
            Assert.AreEqual(sceneLODInfoRetrieved.metadata.LODLoadedCount(), 1);
            Assert.AreEqual(SceneLODInfoUtils.HasLODResult(sceneLODInfoRetrieved.metadata.SuccessfullLODs, 0), true);
            Assert.AreEqual(SceneLODInfoUtils.HasLODResult(sceneLODInfoRetrieved.metadata.FailedLODs, 0), false);
            scenesCache.Received().AddNonRealScene(Arg.Is<Vector2Int[]>(arr => arr.SequenceEqual(DECODED_PARCELS)));
        }

        [Test]
        public void ResolveFailedPromise()
        {
            LogAssert.ignoreFailingMessages = true;

            //Arrange
            sceneLODInfo.CurrentLODPromise = GenerateFailedPromise();
            sceneLODInfo.CurrentLODLevelPromise = 0;
            sceneLODInfo.id = "scene";
            Entity sceneLodInfoEntity = world.Create(sceneLODInfo, sceneDefinitionComponent);

            //Act
            system!.Update(0);

            //Assert
            var sceneLODInfoRetrieved = world.Get<SceneLODInfo>(sceneLodInfoEntity);
            Assert.AreEqual(sceneLODInfoRetrieved.metadata.LODLoadedCount(), 1);
            Assert.AreEqual(SceneLODInfoUtils.HasLODResult(sceneLODInfoRetrieved.metadata.FailedLODs, 0), true);
            Assert.AreEqual(SceneLODInfoUtils.HasLODResult(sceneLODInfoRetrieved.metadata.SuccessfullLODs, 0), false);
            scenesCache.Received().AddNonRealScene(Arg.Is<Vector2Int[]>(arr => arr.SequenceEqual(DECODED_PARCELS)));
        }

        // The injected budgets (FrameTimeCapBudget/MemoryBudget) are stateless per-frame threshold reads, so the reorder
        // is behaviour-preserving; this asserts the resulting call-count property: TrySpendBudget() is invoked only for
        // scenes with a promise mid-flight, not for every loaded scene.
        [Test]
        public void SpendsBudgetOnlyForActivePromises_Performance()
        {
            LogAssert.ignoreFailingMessages = true;

            int frameCalls = 0;
            var frameBudget = Substitute.For<IPerformanceBudget>();
            frameBudget.TrySpendBudget().Returns(_ => { frameCalls++; return true; });

            var memBudget = Substitute.For<IPerformanceBudget>();
            memBudget.TrySpendBudget().Returns(true);

            var textureArrayContainerFactory = new TextureArrayContainerFactory(new Dictionary<TextureArrayKey, Texture>());
            var countingSystem = new InstantiateSceneLODInfoSystem(world, frameBudget, memBudget, scenesCache,
                Substitute.For<ISceneReadinessReportQueue>(),
                textureArrayContainerFactory.CreateSceneLOD(TextureArrayConstants.SCENE_TEX_ARRAY_SHADER, new[]
                {
                    new TextureArrayResolutionDescriptor(256, 500, 1)
                }, TextureFormat.BC7, 20, 1), Substitute.For<IRealmPartitionSettings>());

            const int M = 3;    // scenes with an active, unresolved LOD promise
            const int IDLE = 7; // loaded scenes with no active promise (steady-state majority)

            for (int i = 0; i < M; i++)
            {
                var info = SceneLODInfo.Create();
                info.metadata = new LODCacheInfo(new GameObject().AddComponent<LODGroup>(), 2);
                info.id = $"active_{i.ToString()}";
                info.CurrentLODLevelPromise = 1; // active (!= byte.MaxValue) and not yet instantiated
                info.CurrentLODPromise = Promise.Create(world,
                    GetAssetBundleIntention.FromHash("h", AssetBundleManifestVersion.CreateForLOD("LOD/1", "dummyDate"), typeof(GameObject)),
                    new PartitionComponent()); // no result added -> TryConsume returns false -> resolve is a no-op
                world.Create(info, sceneDefinitionComponent);
            }

            for (int i = 0; i < IDLE; i++)
            {
                var info = SceneLODInfo.Create(); // CurrentLODLevelPromise == byte.MaxValue -> HasActiveLODPromise() == false
                info.metadata = new LODCacheInfo(new GameObject().AddComponent<LODGroup>(), 2);
                info.id = $"idle_{i.ToString()}";
                world.Create(info, sceneDefinitionComponent);
            }

            countingSystem.Update(0);

            // Only the M active scenes reach the budget spend.
            Assert.AreEqual(M, frameCalls);

            countingSystem.Dispose();
        }

        private Promise GenerateFailedPromise()
        {
            var promise = Promise.Create(world,
                GetAssetBundleIntention.FromHash("Cube", AssetBundleManifestVersion.CreateForLOD("LOD/0", "dummyDate"), typeof(GameObject)),
                new PartitionComponent());

            world.Add(promise.Entity,
                new StreamableLoadingResult<AssetBundleData>(ReportData.UNSPECIFIED, new Exception()));

            return promise;
        }

        private (AssetBundleData, Promise) GenerateSuccessfullPromise()
        {
            var promise = Promise.Create(world,
                GetAssetBundleIntention.FromHash("Cube", AssetBundleManifestVersion.CreateForLOD("LOD/0", "dummyDate"), typeof(GameObject)),
                new PartitionComponent());

            var fakeAssetBundleData = new AssetBundleData(null!, new []{GameObject.CreatePrimitive(PrimitiveType.Cube)},
                typeof(GameObject), new AssetBundleData[] { });

            world.Add(promise.Entity,
                new StreamableLoadingResult<AssetBundleData>(fakeAssetBundleData));

            return (fakeAssetBundleData, promise);
        }
    }
}
