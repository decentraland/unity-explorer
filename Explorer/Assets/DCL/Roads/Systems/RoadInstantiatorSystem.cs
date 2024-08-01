﻿using System.Collections.Generic;
using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.LOD;
using DCL.LOD.Systems;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Roads.Components;
using DCL.Roads.Settings;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;
using UnityEngine;
using Utility;

namespace DCL.Roads.Systems
{
    [UpdateInGroup(typeof(PreRenderingSystemGroup))]
    [LogCategory(ReportCategory.ROADS)]
    public partial class RoadInstantiatorSystem : BaseUnityLoopSystem
    {
        private readonly IPerformanceBudget frameCapBudget;
        private readonly IPerformanceBudget memoryBudget;
        private readonly IReadOnlyDictionary<Vector2Int, RoadDescription> roadDescriptions;
        private readonly IRoadAssetPool roadAssetPool;
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;
        private readonly IScenesCache scenesCache;

        internal RoadInstantiatorSystem(World world, IPerformanceBudget frameCapBudget, IPerformanceBudget memoryBudget, IReadOnlyDictionary<Vector2Int, RoadDescription> roadDescriptions, IRoadAssetPool roadAssetPool, ISceneReadinessReportQueue sceneReadinessReportQueue, IScenesCache scenesCache) : base(world)
        {
            this.frameCapBudget = frameCapBudget;
            this.memoryBudget = memoryBudget;
            this.roadDescriptions = roadDescriptions;
            this.roadAssetPool = roadAssetPool;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.scenesCache = scenesCache;
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
            scenesCache.AddNonRealScene(sceneDefinitionComponent.Parcels);
            LODUtils.CheckSceneReadiness(sceneReadinessReportQueue, sceneDefinitionComponent);
        }


    }
}
