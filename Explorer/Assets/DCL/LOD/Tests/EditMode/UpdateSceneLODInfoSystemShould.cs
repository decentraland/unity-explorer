using System.Collections.Generic;
using Arch.Core;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.LOD.Components;
using DCL.LOD.Systems;
using DCL.Optimization.PerformanceBudgeting;
using Decentraland.Kernel.Comms.Rfc4;
using ECS.Prioritization.Components;
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

        private string assetBundlePath =>
            $"file://{Application.dataPath + "/../TestResources/AssetBundles/bafkreid3xecd44iujaz5qekbdrt5orqdqj3wivg5zc5mya3zkorjhyrkda"}";


        [SetUp]
        public void Setup()
        {
            var frameCapBudget = Substitute.For<IPerformanceBudget>();
            frameCapBudget.TrySpendBudget().Returns(true);

            var memoryBudget = Substitute.For<IPerformanceBudget>();
            memoryBudget.TrySpendBudget().Returns(true);

            lodAssetsPool = new LODAssetsPool();
            InitializeSceneLODInfo();
            partitionComponent = new PartitionComponent();

            system = new UpdateSceneLODInfoSystem(world, lodAssetsPool, new List<int> { 1, 2, 5 },
                frameCapBudget,
                memoryBudget);
        }

        private void InitializeSceneLODInfo()
        {
            sceneLODInfo.CurrentLODLevel = -1;
            sceneLODInfo.SceneHash = "FakeHash";
            sceneLODInfo.ParcelPosition = new Vector3(0, 0);
        }


        [Test]
        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(2, 2)]
        [TestCase(3, 2)]
        [TestCase(4, 3)]
        [TestCase(10, 3)]
        public void ResolveLODLevel(byte bucket, int expectedLODLevel)
        {
            //Arrange
            partitionComponent.Bucket = bucket;
            partitionComponent.IsDirty = true;
            var entity = world.Create(sceneLODInfo, partitionComponent);

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
            sceneLODInfo.CurrentLODLevel = 2;
            var sceneLodInfoEntity = world.Create(sceneLODInfo);

            //Act
            system.Update(0);

            //Assert
            var sceneLODInfoRetrieved = world.Get<SceneLODInfo>(sceneLodInfoEntity);
            Assert.NotNull(sceneLODInfoRetrieved.CurrentLOD.Root);
            Assert.AreEqual($"{sceneLODInfoRetrieved.SceneHash.ToLower()}_lod2",
                sceneLODInfoRetrieved.CurrentLOD.LodKey);
            Assert.AreEqual(promiseGenerated.Item1, sceneLODInfoRetrieved.CurrentLOD.AssetBundleReference);
        }

        [Test]
        public void UpdateCache()
        {
            //Arrange
            var promiseGenerated = GenerateLODPromise();
            sceneLODInfo.CurrentLODPromise = promiseGenerated.Item2;
            sceneLODInfo.CurrentLODLevel = 2;
            world.Create(sceneLODInfo);
            system.Update(0);

            world.Query(new QueryDescription().WithAll<SceneLODInfo>(),
                (ref SceneLODInfo sceneLODInfo) =>
                {
                    var newPromiseGenerated = GenerateLODPromise();
                    sceneLODInfo.CurrentLODLevel = 3;
                    sceneLODInfo.CurrentLODPromise = newPromiseGenerated.Item2;
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
                new AssetBundleData[] { });

            world.Add(promise.Entity,
                new StreamableLoadingResult<AssetBundleData>(fakeAssetBundleData));
            return (fakeAssetBundleData, promise);
        }

    }
}
