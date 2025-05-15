using System.Collections.Generic;
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
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
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

        internal RoadInstantiatorSystem(World world, IPerformanceBudget frameCapBudget, IPerformanceBudget memoryBudget, IReadOnlyDictionary<Vector2Int, RoadDescription> roadDescriptions, IRoadAssetPool roadAssetPool,
            ISceneReadinessReportQueue sceneReadinessReportQueue, IScenesCache scenesCache) : base(world)
        {
            this.frameCapBudget = frameCapBudget;
            this.memoryBudget = memoryBudget;
            this.roadDescriptions = roadDescriptions;
            this.roadAssetPool = roadAssetPool;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.scenesCache = scenesCache;
            
            foreach (var keyValuePair in roadDescriptions)
                InstantiateRoad(keyValuePair.Key, keyValuePair.Value);
        }

        protected override void Update(float t)
        {
            InstantiateRoadQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PortableExperienceComponent))]
        private void InstantiateRoad(ref RoadInfo roadInfo, ref SceneDefinitionComponent sceneDefinitionComponent, ref PartitionComponent partitionComponent, ref SceneLoadingState sceneLoadingState)
        {
            if (partitionComponent.OutOfRange) return;

            if (sceneLoadingState.PromiseCreated) return;

            if (partitionComponent.IsBehind) return;

            if (!(frameCapBudget.TrySpendBudget() && memoryBudget.TrySpendBudget())) return;

            /*if (roadDescriptions.TryGetValue(sceneDefinitionComponent.Definition.metadata.scene.DecodedBase, out RoadDescription roadDescription))
            {
                if (!roadAssetPool.Get(roadDescription.RoadModel, out Transform? roadAsset))
                {
                    ReportHub.LogWarning(GetReportData(),
                        $"Road with model for {roadDescription.RoadModel} at {sceneDefinitionComponent.Definition.metadata.scene.DecodedBase.ToString()} does not exist, loading default");
                }

                //HACK: Since all original scene dont have the correct pivot, we move it here
                roadAsset.localPosition = sceneDefinitionComponent.SceneGeometry.BaseParcelPosition + ParcelMathHelper.RoadPivotDeviation;
                roadAsset.localRotation = roadDescription.Rotation;
                roadAsset.gameObject.SetActive(true);

#if UNITY_EDITOR
                roadAsset.gameObject.name = $"{roadAsset.gameObject.name}_{roadDescription.RoadCoordinate.x},{roadDescription.RoadCoordinate.y}";
#endif

                roadInfo.CurrentAsset = roadAsset;
                roadInfo.CurrentKey = roadDescription.RoadModel;
            }
            else
            {
                ReportHub.LogWarning(GetReportData(),
                    $"Road with coords for {sceneDefinitionComponent.Definition.metadata.scene.DecodedBase} do not have a description");
            }*/

            sceneLoadingState.PromiseCreated = true;

            //In case this is a road teleport destination, we need to release the loading screen
            SceneUtils.ReportSceneLoaded(sceneDefinitionComponent, sceneReadinessReportQueue, scenesCache);
        }
    
        
        private void InstantiateRoad(Vector2Int baseParcel, RoadDescription roadDescription)
        {
            roadAssetPool.Get(roadDescription.RoadModel, out var roadAsset);

            //HACK: Since all original scene dont have the correct pivot, we move it here
            roadAsset.localPosition = new Vector3(baseParcel.x * 16, 0, baseParcel.y * 16) + ParcelMathHelper.RoadPivotDeviation;
            roadAsset.localRotation = roadDescription.Rotation;
            roadAsset.gameObject.SetActive(true);

#if UNITY_EDITOR
            roadAsset.gameObject.name = $"{roadAsset.gameObject.name}_{roadDescription.RoadCoordinate.x},{roadDescription.RoadCoordinate.y}";
#endif

        }
    }
}
