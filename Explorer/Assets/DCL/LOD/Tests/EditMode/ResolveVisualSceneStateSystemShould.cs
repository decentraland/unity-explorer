using System.Collections.Generic;
using DCL.Ipfs;
using ECS;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace DCL.LOD.Tests
{
    public class ResolveVisualSceneStateSystemShould : UnitySystemTestBase<ResolveVisualSceneStateSystem>
    {
        private static readonly Vector2Int ROAD_BASE_PARCEL = new (1, 1);
        private static readonly Vector2Int REGULAR_PARCEL = new (0, 0);

        private static readonly IReadOnlyList<Vector2Int> SAMPLE_PARCELS = new Vector2Int[]
        {
            new (0, 0), new (0, 1), new (1, 0), new (1, 1)
        };

        [SetUp]
        public void Setup()
        {
            var lodSettings = Substitute.For<ILODSettingsAsset>();
            int[] bucketThresholds =
            {
                4, 8
            };
            lodSettings.LodPartitionBucketThresholds.Returns(bucketThresholds);
            lodSettings.SDK7LodThreshold.Returns(2);
            var realmData = Substitute.For<IRealmData>();
            system = new ResolveVisualSceneStateSystem(world, lodSettings, new VisualSceneStateResolver(new HashSet<Vector2Int>
            {
                ROAD_BASE_PARCEL
            }), realmData);
        }

        [Test]
        public void AddDefaultSceneVisualState()
        {
            var sceneEntityDefinition = new SceneEntityDefinition
            {
                metadata = new SceneMetadata
                {
                    scene = new SceneMetadataScene
                    {
                        DecodedParcels = SAMPLE_PARCELS, DecodedBase = REGULAR_PARCEL
                    }
                }
            };
            var sceneDefinitionComponent = new SceneDefinitionComponent(sceneEntityDefinition, new IpfsPath());
            var entity = world.Create( new PartitionComponent
            {
                IsDirty = true
            }, sceneDefinitionComponent);

            system.Update(0);

            var visualSceneState = world.Get<VisualSceneState>(entity);

            Assert.IsFalse(visualSceneState.IsDirty);
            Assert.IsTrue(visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.SHOWING_LOD);
        }

        [Test]
        public void AddRoad()
        {
            var sceneEntityDefinition = new SceneEntityDefinition
            {
                metadata = new SceneMetadata
                {
                    scene = new SceneMetadataScene
                    {
                        DecodedParcels = SAMPLE_PARCELS, DecodedBase = ROAD_BASE_PARCEL
                    }
                }
            };
            var sceneDefinitionComponent = new SceneDefinitionComponent(sceneEntityDefinition, new IpfsPath());
            var entity = world.Create( new PartitionComponent
            {
                IsDirty = true
            }, sceneDefinitionComponent);

            system.Update(0);

            var visualSceneState = world.Get<VisualSceneState>(entity);

            Assert.IsFalse(visualSceneState.IsDirty);
            Assert.IsTrue(visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.ROAD);
        }

        [Test]
        [TestCase(5, VisualSceneStateEnum.SHOWING_LOD)]
        [TestCase(3, VisualSceneStateEnum.SHOWING_LOD)]
        [TestCase(0, VisualSceneStateEnum.SHOWING_SCENE)]
        public void AddDefaultSDK7SceneVisualState(byte bucket, VisualSceneStateEnum expectedVisualSceneState)
        {
            var partitionComponent = new PartitionComponent();
            partitionComponent.Bucket = bucket;
            partitionComponent.IsDirty = true;

            var sceneEntityDefinition = new SceneEntityDefinition
            {
                id = "FAKE_HASH", metadata = new SceneMetadata
                {
                    scene = new SceneMetadataScene
                    {
                        DecodedParcels = SAMPLE_PARCELS
                    },
                    runtimeVersion = "7"
                }
            };

            var sceneDefinitionComponent = new SceneDefinitionComponent(sceneEntityDefinition, new IpfsPath());
            var entity = world.Create( partitionComponent, sceneDefinitionComponent);

            system.Update(0);

            var visualSceneState = world.Get<VisualSceneState>(entity);

            Assert.IsFalse(visualSceneState.IsDirty);
            Assert.IsTrue(visualSceneState.CurrentVisualSceneState == expectedVisualSceneState);
        }
    }
}
