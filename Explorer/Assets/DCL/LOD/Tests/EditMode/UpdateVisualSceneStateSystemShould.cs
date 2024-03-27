using System.Collections.Generic;
using DCL.Ipfs;
using DCL.LOD;
using DCL.LOD.Components;
using ECS;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.Common;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;

public class UpdateVisualSceneStateSystemShould : UnitySystemTestBase<UpdateVisualSceneStateSystem>
{
    private PartitionComponent partitionComponent;
    private SceneDefinitionComponent sceneDefinitionComponent;
    private VisualSceneState visualSceneState;

    [SetUp]
    public void Setup()
    {
        var lodSettings = Substitute.For<ILODSettingsAsset>();
        int[] bucketThresholds =
        {
            2, 4
        };
        lodSettings.LodPartitionBucketThresholds.Returns(bucketThresholds);

        var scenesCahce = Substitute.For<IScenesCache>();
        var lodAssetsPool = Substitute.For<ILODAssetsPool>();
        var realmData = Substitute.For<IRealmData>();

        partitionComponent = new PartitionComponent();
        partitionComponent.IsDirty = true;

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

        sceneDefinitionComponent = new SceneDefinitionComponent(sceneEntityDefinition, new IpfsPath());
        visualSceneState = new VisualSceneState();

        system = new UpdateVisualSceneStateSystem(world, realmData, scenesCahce, lodAssetsPool, lodSettings, new VisualSceneStateResolver(new HashSet<Vector2Int>()));
    }

    [Test]
    public void UpdateFromSceneLODInfoToScenePromise()
    {
        visualSceneState.CurrentVisualSceneState = VisualSceneStateEnum.SHOWING_LOD;
        partitionComponent.Bucket = 0;
        var entityReference = world.Create(visualSceneState, partitionComponent, sceneDefinitionComponent, new SceneLODInfo());

        system.Update(0);

        Assert.IsTrue(world.Has<AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(entityReference));
        Assert.IsFalse(world.Has<SceneLODInfo>(entityReference));
    }

    [Test]
    public void UpdateFromSceneFacadeToSceneLOD()
    {
        visualSceneState.CurrentVisualSceneState = VisualSceneStateEnum.SHOWING_SCENE;
        partitionComponent.Bucket = 5;
        var entityReference = world.Create(visualSceneState, partitionComponent, sceneDefinitionComponent, Substitute.For<ISceneFacade>());

        system.Update(0);

        Assert.IsTrue(world.Has<SceneLODInfo>(entityReference));
        Assert.IsFalse(world.Has<ISceneFacade>(entityReference));
    }

    [Test]
    public void UpdateFromScenePromiseToSceneLOD()
    {
        visualSceneState.CurrentVisualSceneState = VisualSceneStateEnum.SHOWING_SCENE;
        partitionComponent.Bucket = 5;
        var entityReference = world.Create(visualSceneState, partitionComponent, sceneDefinitionComponent, new AssetPromise<ISceneFacade, GetSceneFacadeIntention>());

        system.Update(0);

        Assert.IsTrue(world.Has<SceneLODInfo>(entityReference));
        Assert.IsFalse(world.Has<AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(entityReference));
    }

    [Test]
    public void KeepSceneLODWhenSDK6()
    {
        visualSceneState.CurrentVisualSceneState = VisualSceneStateEnum.SHOWING_LOD;
        partitionComponent.Bucket = 0;

        var sdk6SceneDefinitionComponent = new SceneEntityDefinition
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
                runtimeVersion = "6"
            }
        };
        var entityReference = world.Create(visualSceneState, partitionComponent, sdk6SceneDefinitionComponent, new SceneLODInfo());

        system.Update(0);

        Assert.IsTrue(world.Has<SceneLODInfo>(entityReference));
        Assert.IsFalse(world.Has<ISceneFacade>(entityReference));
    }



}
