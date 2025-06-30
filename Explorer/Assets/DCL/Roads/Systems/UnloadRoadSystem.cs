using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.LOD;
using DCL.Roads.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;

namespace DCL.Roads.Systems
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.ROADS)]
    public partial class UnloadRoadSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly IRoadAssetPool roadAssetPool;
        private readonly IScenesCache scenesCache;

        public UnloadRoadSystem(World world, IRoadAssetPool roadAssetPool, IScenesCache scenesCache) : base(world)
        {
            this.roadAssetPool = roadAssetPool;
            this.scenesCache = scenesCache;
        }

        protected override void Update(float t)
        {
            UnloadRoadQuery(World);
        }

        [Query]
        private void UnloadRoad(ref RoadInfo roadInfo, ref SceneDefinitionComponent sceneDefinitionComponent, ref PartitionComponent partitionComponent, ref SceneLoadingState loadingState)
        {
            if (partitionComponent.OutOfRange)
                Unload(roadInfo, sceneDefinitionComponent, loadingState);
        }

        private void Unload(RoadInfo roadInfo, SceneDefinitionComponent sceneDefinitionComponent,
            SceneLoadingState loadingState)
        {
            if (loadingState.PromiseCreated)
            {
                roadInfo.Dispose(roadAssetPool);
                loadingState.PromiseCreated = false;
                scenesCache.RemoveNonRealScene(sceneDefinitionComponent.Parcels);
            }
        }

        [Query]
        private void UnloadAllRoads(ref RoadInfo roadInfo, ref SceneDefinitionComponent sceneDefinitionComponent, ref SceneLoadingState loadingState)
        {
            Unload(roadInfo, sceneDefinitionComponent, loadingState);
        }

        public void FinalizeComponents(in Query query)
        {
            UnloadAllRoadsQuery(World);
        }
    }
}
