using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.LOD;
using DCL.Roads.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using System;
using System.Runtime.CompilerServices;

namespace DCL.Roads.Systems
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.ROADS)]
    public partial class UnloadRoadSystem : BaseUnityLoopSystem
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
            //Note: The destruction of all roads on realm change is done on DestroyAllRoadAssetsTeleportOperation.cs
            if (partitionComponent.OutOfRange && loadingState.PromiseCreated)
            {
                roadInfo.Dispose(roadAssetPool);
                loadingState.PromiseCreated = false;
                scenesCache.RemoveNonRealScene(sceneDefinitionComponent.Parcels);
            }
        }

    }
}
