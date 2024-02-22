using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.LOD.Components;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData,
    ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.LOD
{
    [UpdateInGroup(typeof(RealmGroup))]
    public partial class RoadInstantiatorSystem : BaseUnityLoopSystem
    {
        
        private readonly IPerformanceBudget frameCapBudget;
        private readonly IPerformanceBudget memoryBudget;
        
        public RoadInstantiatorSystem(World world, IPerformanceBudget frameCapBudget, IPerformanceBudget memoryBudget) : base(world)
        {
            this.frameCapBudget = frameCapBudget;
            this.memoryBudget = memoryBudget;
        }

        protected override void Update(float t)
        {
            CreateRoadPromiseQuery(World);
            InstantiateRoadQuery(World);
        }
        
        [Query]
        [None(typeof(DeleteEntityIntention))]
        public void InstantiateRoad(ref RoadInfo roadInfo, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (roadInfo.IsDirty) return;

            if (roadInfo.CurrentRoadPromise.IsConsumed) return;
            
            if (!(frameCapBudget.TrySpendBudget() && memoryBudget.TrySpendBudget())) return;

            if (roadInfo.CurrentRoadPromise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> result))
            {
                if (result.Succeeded)
                {
                    roadInfo.AssetBundleReference = result.Asset;
                    GameObject.Instantiate(result.Asset.GameObject, sceneDefinitionComponent.SceneGeometry.BaseParcelPosition, Quaternion.identity);
                }
                else
                    ReportHub.LogWarning(GetReportCategory(),
                        $"Road request for {roadInfo.CurrentRoadPromise.LoadingIntention.Hash} failed");
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        public void CreateRoadPromise(ref RoadInfo roadInfo, ref PartitionComponent partitionComponent)
        {
            if (!roadInfo.IsDirty) return;
            
            roadInfo.CurrentRoadPromise =
                Promise.Create(World,
                    GetAssetBundleIntention.FromHash("road",
                        permittedSources: AssetSource.EMBEDDED,
                        customEmbeddedSubDirectory: URLSubdirectory.FromString("roads")),
                    partitionComponent); 
            
            roadInfo.IsDirty = false;
        }
    }
}