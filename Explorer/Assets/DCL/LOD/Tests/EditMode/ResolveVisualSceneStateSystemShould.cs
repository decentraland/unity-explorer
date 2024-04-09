using DCL.Ipfs;
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

        public void Setup()
        {
            var lodSettings = Substitute.For<ILODSettingsAsset>();
            int[] bucketThresholds =
            {
                2, 4
            };
            lodSettings.LodPartitionBucketThresholds.Returns(bucketThresholds);
            system = new ResolveVisualSceneStateSystem(world, lodSettings);
        }

        /*
         TODO: Commented until we decide what we do with SDK6 scenes
        public void AddDefaultSceneVisualState()
        {
            var entity = world.Create( new PartitionComponent(), new SceneDefinitionComponent());

            system.Update(0);

            var visualSceneState = world.Get<VisualSceneState>(entity);

            Assert.IsFalse(visualSceneState.IsDirty);
            Assert.IsTrue(visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.SHOWING_LOD);
        }*/




        public void AddDefaultSDK7SceneVisualState(byte bucket, VisualSceneStateEnum expectedVisualSceneState)
        {
            var partitionComponent = new PartitionComponent();
            partitionComponent.Bucket = bucket;

            var sceneEntityDefinition = new SceneEntityDefinition
            {
                id = "FAKE_HASH", metadata = new SceneMetadata
                {
                    scene = new SceneMetadataScene
                    {
                        DecodedParcels = new Vector2Int[]
                        {
                            new (0, 0), new (0, 1), new (1, 0), new (2, 0), new (2, 1), new (3, 0), new (3, 1)
                        }
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
