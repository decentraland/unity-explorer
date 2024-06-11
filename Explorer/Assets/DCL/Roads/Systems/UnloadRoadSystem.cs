using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.LOD;
using DCL.Roads.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;

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
        [All(typeof(DeleteEntityIntention), typeof(VisualSceneState))]
        private void UnloadRoad(in Entity entity, ref RoadInfo roadInfo, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            roadInfo.Dispose(roadAssetPool);
            scenesCache.RemoveNonRealScene(sceneDefinitionComponent.Parcels);
            World.Remove<RoadInfo, VisualSceneState, DeleteEntityIntention>(entity);
        }
    }
}
