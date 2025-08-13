﻿using System;
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
using ECS.Unity.GLTFContainer.Asset.Cache;
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
        private IScenesCache scenesCache;

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
            var sceneLodInfoEntity = world.Create(sceneLODInfo, sceneDefinitionComponent);

            //Act
            system.Update(0);

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
            var sceneLodInfoEntity = world.Create(sceneLODInfo, sceneDefinitionComponent);

            //Act
            system.Update(0);

            //Assert
            var sceneLODInfoRetrieved = world.Get<SceneLODInfo>(sceneLodInfoEntity);
            Assert.AreEqual(sceneLODInfoRetrieved.metadata.LODLoadedCount(), 1);
            Assert.AreEqual(SceneLODInfoUtils.HasLODResult(sceneLODInfoRetrieved.metadata.FailedLODs, 0), true);
            Assert.AreEqual(SceneLODInfoUtils.HasLODResult(sceneLODInfoRetrieved.metadata.SuccessfullLODs, 0), false);
            scenesCache.Received().AddNonRealScene(Arg.Is<Vector2Int[]>(arr => arr.SequenceEqual(DECODED_PARCELS)));
        }

        private Promise GenerateFailedPromise()
        {
            var promise = Promise.Create(world,
                GetAssetBundleIntention.FromHash(typeof(GameObject), "Cube"),
                new PartitionComponent());

            world.Add(promise.Entity,
                new StreamableLoadingResult<AssetBundleData>(ReportData.UNSPECIFIED, new Exception()));

            return promise;
        }

        private (AssetBundleData, Promise) GenerateSuccessfullPromise()
        {
            var promise = Promise.Create(world,
                GetAssetBundleIntention.FromHash(typeof(GameObject), "Cube"),
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
