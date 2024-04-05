using System.Collections.Generic;
using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.LOD;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Roads.Components;
using DCL.Roads.Settings;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.SceneDefinition;
using UnityEngine;
using Utility;

namespace DCL.Roads.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [LogCategory(ReportCategory.ROADS)]
    public partial class RoadInstantiatorSystem : BaseUnityLoopSystem
    {
        private readonly IPerformanceBudget frameCapBudget;
        private readonly IPerformanceBudget memoryBudget;
        private readonly IReadOnlyDictionary<Vector2Int, RoadDescription> roadDescriptions;
        private readonly IRoadAssetPool roadAssetPool;

        internal RoadInstantiatorSystem(World world, IPerformanceBudget frameCapBudget, IPerformanceBudget memoryBudget, IReadOnlyDictionary<Vector2Int, RoadDescription> roadDescriptions, IRoadAssetPool roadAssetPool) : base(world)
        {
            this.frameCapBudget = frameCapBudget;
            this.memoryBudget = memoryBudget;
            this.roadDescriptions = roadDescriptions;
            this.roadAssetPool = roadAssetPool;
        }

        protected override void Update(float t)
        {
            InstantiateRoadQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void InstantiateRoad(ref RoadInfo roadInfo, ref SceneDefinitionComponent sceneDefinitionComponent, ref PartitionComponent partitionComponent)
        {
            if (!roadInfo.IsDirty) return;

            if (partitionComponent.IsBehind) return;

            if (!(frameCapBudget.TrySpendBudget() && memoryBudget.TrySpendBudget())) return;

            if (roadDescriptions.TryGetValue(sceneDefinitionComponent.Definition.metadata.scene.DecodedBase, out var roadDescription))
            {
                if (!roadAssetPool.Get(roadDescription.RoadModel, out var roadAsset))
                {
                    ReportHub.LogWarning(GetReportCategory(),
                        $"Road with model for {roadDescription.RoadModel} at {sceneDefinitionComponent.Definition.metadata.scene.DecodedBase.ToString()} does not exist, loading default");
                }

                //HACK: Since all original scene dont have the correct pivot, we move it here
                roadAsset.localPosition = sceneDefinitionComponent.SceneGeometry.BaseParcelPosition + ParcelMathHelper.RoadPivotDeviation;
                ;
                roadAsset.localRotation = roadDescription.Rotation;
                roadAsset.gameObject.SetActive(true);

                roadInfo.CurrentAsset = roadAsset;
                roadInfo.CurrentKey = roadDescription.RoadModel;
            }
            else
            {
                ReportHub.LogWarning(GetReportCategory(),
                    $"Road with coords for {sceneDefinitionComponent.Definition.metadata.scene.DecodedBase} do not have a description");
            }
            roadInfo.IsDirty = false;
        }


    }
}
