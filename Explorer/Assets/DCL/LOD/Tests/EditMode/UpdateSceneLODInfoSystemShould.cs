using Arch.Core;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.LOD.Components;
using DCL.LOD.Systems;
using DCL.Optimization.PerformanceBudgeting;
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
        private LODAssetCache lodAssetCache;
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

            lodAssetCache = new LODAssetCache();
            InitializeSceneLODInfo();
            partitionComponent = new PartitionComponent();

            system = new UpdateSceneLODInfoSystem(world, lodAssetCache, new Vector2Int[] { new(1, 3), new(3, 5) },
                frameCapBudget,
                memoryBudget);
        }

        private void InitializeSceneLODInfo()
        {
            sceneLODInfo.CurrentLODLevel = -1;
            sceneLODInfo.SceneHash = "FakeHash";
            sceneLODInfo.ParcelPosition = new Vector3(0, 0);
            sceneLODInfo.LodCache = lodAssetCache;
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

        /*
        [Test]
        public void ResolveLODPromise()
        {
            //Arrange
            string gameobjectName = "Cube";
            GenerateLODPromise(gameobjectName, out var resultGameobject, out var promise);
            sceneLODInfo.CurrentLODPromise = promise;
            sceneLODInfo.CurrentLODLevel = 2;

            //Act
            system.Update(0);

            //Assert
            Assert.AreEqual(resultGameobject, world.Get<SceneLODInfo>(promise.Entity).CurrentLOD.Root);
            Assert.AreEqual(gameobjectName, world.Get<SceneLODInfo>(promise.Entity).CurrentLOD.Root.name);
            Assert.AreEqual("Cube_lod2", world.Get<SceneLODInfo>(promise.Entity).CurrentLOD.LodKey);
        }

        [Test]
        public void UpdateCache()
        {
            //Arrange
            string gameobjectName = "Cube";
            GenerateLODPromise(gameobjectName, out var resultGameobject, out var promise);
            sceneLODInfo.CurrentLODPromise = promise;
            sceneLODInfo.CurrentLODLevel = 2;

            system.Update(0);

            sceneLODInfo = world.Get<SceneLODInfo>(promise.Entity);
            string newGameObjectName = "Sphere";
            GenerateLODPromise(newGameObjectName, out var newResultGameobject, out var newPromise);
            sceneLODInfo.CurrentLODPromise = newPromise;
            sceneLODInfo.CurrentLODLevel = 3;

            //Act
            system.Update(0);

            //Assert
            Assert.AreEqual(lodAssetCache.cache.Count, 1);
        }

        private void GenerateLODPromise(string gameobjectName, out GameObject resultGameobject, out Promise promise)
        {
            resultGameobject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            resultGameobject.name = gameobjectName;
            promise = Promise.Create(world,
                GetAssetBundleIntention.FromName("Cube"),
                new PartitionComponent());
            world.Add(promise.Entity,
                new StreamableLoadingResult<AssetBundleData>(new AssetBundleData(null, null, resultGameobject,
                    new AssetBundleData[] { })));
        }
        */
    }
}