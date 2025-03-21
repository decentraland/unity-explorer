using System.Collections;
using System.Collections.Generic;
using Arch.Core;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Ipfs;
using DCL.LOD;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Roads.Components;
using DCL.Roads.Settings;
using DCL.Roads.Systems;
using DCL.Utilities;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Utility;

public class RoadInstantiatorSystemShould : UnitySystemTestBase<RoadInstantiatorSystem>
{
    private static readonly string EXISTING_ROAD_KEY = "EXISTING_ROAD";
    private static readonly string NON_EXISTING_ROAD_KEY = "NON_EXISTING_ROAD";
    private static readonly Quaternion EXISTING_ROTATION = Quaternion.Euler(90, 0, 0);
    private static readonly Vector2Int EXISTING_COORD = new (0, 0);
    private static readonly Vector2Int NON_EXISTING_COORD = new (1, 1);
    private Transform existingInstantiatedRoad;

    [SetUp]
    public void Setup()
    {
        IPerformanceBudget frameCapBudget = Substitute.For<IPerformanceBudget>();
        frameCapBudget.TrySpendBudget().Returns(true);

        IPerformanceBudget memoryBudget = Substitute.For<IPerformanceBudget>();
        memoryBudget.TrySpendBudget().Returns(true);

        IReadOnlyDictionary<Vector2Int, RoadDescription> roadDescriptions
            = new Dictionary<Vector2Int, RoadDescription>
            {
                {
                    EXISTING_COORD, new RoadDescription
                    {
                        RoadCoordinate = EXISTING_COORD, RoadModel = EXISTING_ROAD_KEY, Rotation = EXISTING_ROTATION,
                    }
                },
                {
                    NON_EXISTING_COORD, new RoadDescription
                    {
                        RoadCoordinate = NON_EXISTING_COORD, RoadModel = NON_EXISTING_ROAD_KEY, Rotation = EXISTING_ROTATION,
                    }
                },
            };

        IRoadAssetPool roadAssetPool = Substitute.For<IRoadAssetPool>();
        existingInstantiatedRoad = new GameObject(EXISTING_ROAD_KEY).transform;

        roadAssetPool.Get(EXISTING_ROAD_KEY, out Arg.Any<Transform>())
                     .Returns(x =>
                      {
                          x[1] = existingInstantiatedRoad;
                          return true;
                      });

        roadAssetPool.Get(NON_EXISTING_ROAD_KEY, out Arg.Any<Transform>())
                     .Returns(x =>
                      {
                          x[1] = existingInstantiatedRoad;
                          return false;
                      });

        IScenesCache scenesCache = Substitute.For<IScenesCache>();
        ISceneReadinessReportQueue sceneReadinessReportQueue = Substitute.For<ISceneReadinessReportQueue>();

        system = new RoadInstantiatorSystem(world, frameCapBudget, memoryBudget, roadDescriptions, roadAssetPool, sceneReadinessReportQueue, scenesCache);
    }

    [Test]
    public void InstantiateRoad()
    {
        // Arrange
        var roadInfo = new RoadInfo
        {
        };

        var sceneEntityDefinition = new SceneEntityDefinition
        {
            id = "fakeHash", metadata = new SceneMetadata
            {
                scene = new SceneMetadataScene
                {
                    DecodedBase = EXISTING_COORD, DecodedParcels = new[]
                    {
                        EXISTING_COORD,
                    },
                },
            },
        };

        SceneDefinitionComponent sceneDefinitionComponent = SceneDefinitionComponentFactory.CreateFromDefinition(sceneEntityDefinition, new IpfsPath());

        var partitionComponent = new PartitionComponent
        {
            IsBehind = false,
        };

        Entity roadEntity
            = world.Create(roadInfo, partitionComponent, sceneDefinitionComponent, SceneLoadingState.CreateRoad());

        // Act
        system.Update(0);

        // Assert
        Assert.AreEqual(world.Get<RoadInfo>(roadEntity).CurrentAsset, existingInstantiatedRoad);
        Assert.AreEqual(world.Get<RoadInfo>(roadEntity).CurrentKey, EXISTING_ROAD_KEY);

        //We check against its moved pivot position
        Assert.AreEqual(existingInstantiatedRoad.localPosition.magnitude, (ParcelMathHelper.GetPositionByParcelPosition(EXISTING_COORD) + new Vector3(8, 0, 8)).magnitude);
        Assert.AreEqual(Quaternion.Angle(existingInstantiatedRoad.localRotation, EXISTING_ROTATION), 0);
        Assert.IsTrue(existingInstantiatedRoad.gameObject.activeSelf);
    }

    [Test]
    public void InstantiateDefaultRoad()
    {
        var sceneEntityDefinition = new SceneEntityDefinition
        {
            id = "fakeHash", metadata = new SceneMetadata
            {
                scene = new SceneMetadataScene
                {
                    DecodedBase = NON_EXISTING_COORD, DecodedParcels = new[]
                    {
                        NON_EXISTING_COORD,
                    },
                },
            },
        };

        SceneDefinitionComponent sceneDefinitionComponent = SceneDefinitionComponentFactory.CreateFromDefinition(sceneEntityDefinition, new IpfsPath());

        var partitionComponent = new PartitionComponent
        {
            IsBehind = false,
        };

        Entity roadEntity
            = world.Create(RoadInfo.Create(), partitionComponent, sceneDefinitionComponent, SceneLoadingState.CreateRoad());

        // Act
        system.Update(0);

        // Assert
        Assert.AreEqual(world.Get<RoadInfo>(roadEntity).CurrentAsset, existingInstantiatedRoad);
        Assert.AreEqual(world.Get<RoadInfo>(roadEntity).CurrentKey, NON_EXISTING_ROAD_KEY);

        //We check against its moved pivot position
        Assert.AreEqual(existingInstantiatedRoad.localPosition.magnitude,
            (ParcelMathHelper.GetPositionByParcelPosition(NON_EXISTING_COORD) + new Vector3(8, 0, 8)).magnitude);

        Assert.AreEqual(Quaternion.Angle(existingInstantiatedRoad.localRotation, EXISTING_ROTATION), 0);
        Assert.IsTrue(existingInstantiatedRoad.gameObject.activeSelf);
    }
}
