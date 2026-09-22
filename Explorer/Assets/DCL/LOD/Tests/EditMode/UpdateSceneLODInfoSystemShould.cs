using Arch.Core;
using DCL.Ipfs;
using DCL.LOD.Components;
using DCL.LOD.Systems;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.Reporting;
using DCL.SceneRunner.Scene;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles.InitialSceneState;
using ECS.TestSuite;
using ECS.Unity.GLTFContainer.Asset.Cache;
using DCL.Utility;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Assert = UnityEngine.Assertions.Assert;

namespace DCL.LOD.Tests
{
    public class UpdateSceneLODInfoSystemShould : UnitySystemTestBase<UpdateSceneLODInfoSystem>
    {
        private const string fakeHash = "FAKE_HASH";
        private SceneLODInfo sceneLODInfo;
        private PartitionComponent partitionComponent;
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

            IScenesCache? scenesCache = Substitute.For<IScenesCache>();
            ISceneReadinessReportQueue? sceneReadinessReportQueue = Substitute.For<ISceneReadinessReportQueue>();

            partitionComponent = new PartitionComponent();

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
            sceneLODInfo.metadata = new LODCacheInfo(new GameObject().AddComponent<LODGroup>(), 2);
            system = new UpdateSceneLODInfoSystem(world, lodSettings);
        }

        [Test]
        //Note: Test modified due to LOD level always defaulting to 3 while we rebuild all of them
        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(2, 1)]
        [TestCase(3, 1)]
        [TestCase(4, 1)]
        [TestCase(10, 1)]
        public void ResolveLODLevelWithUnsupportedISS(byte bucket, int expectedLODLevel)
        {
            //Arrange
            partitionComponent.IsDirty = true;
            partitionComponent.Bucket = bucket;
            Entity entity = world.Create(sceneLODInfo, partitionComponent, sceneDefinitionComponent, SceneLoadingState.CreateBuiltScene(), ISSDescriptor.NONE);

            //Act
            system.Update(0);

            var sceneLODInfoRetrieved = world.Get<SceneLODInfo>(entity);
            Assert.AreEqual(sceneLODInfoRetrieved.CurrentLODLevelPromise, expectedLODLevel);
        }

        [Test]
        public void RequestTheDigestNamedBundleWhenTheManifestNamesIt()
        {
            //Arrange
            const string DIGEST = "0123456789abcdef0123456789abcdef";
            string sceneId = fakeHash.ToLower();
            var manifest = AssetBundleManifestVersion.CreateFromFallback("v49", "2026-09-22");

            manifest.InjectLods(new SceneAbLodsDto
            {
                digest = DIGEST,
                descriptor = $"{sceneId}_{DIGEST}_InitialSceneState.json",
                levels = new[] { new SceneAbLodLevelDto { level = 1, file = $"{sceneId}_{DIGEST}_1" } },
            });

            sceneDefinitionComponent.Definition.assetBundleManifestVersion = manifest;
            partitionComponent.IsDirty = true;
            partitionComponent.Bucket = 2;
            Entity entity = world.Create(sceneLODInfo, partitionComponent, sceneDefinitionComponent, SceneLoadingState.CreateBuiltScene(), ISSDescriptor.NONE);

            //Act
            system.Update(0);

            //Assert
            var sceneLODInfoRetrieved = world.Get<SceneLODInfo>(entity);
            Assert.AreEqual(1, sceneLODInfoRetrieved.CurrentLODLevelPromise);
            Assert.AreEqual($"{sceneId}_{DIGEST}_1{PlatformUtils.GetCurrentPlatform()}", sceneLODInfoRetrieved.CurrentLODPromise.LoadingIntention.Hash);
        }
    }
}
