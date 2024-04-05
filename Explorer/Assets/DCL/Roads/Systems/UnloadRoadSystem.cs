using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.LOD;
using DCL.Roads.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle.Components;

namespace DCL.Roads.Systems
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.ROADS)]
    public partial class UnloadRoadSystem : BaseUnityLoopSystem
    {
        private readonly IRoadAssetPool roadAssetPool;

        private static readonly QueryDescription DELETE_ROAD_QUERY = new QueryDescription()
            .WithAll<RoadInfo, VisualSceneState, DeleteEntityIntention>();

        public UnloadRoadSystem(World world, IRoadAssetPool roadAssetPool) : base(world)
        {
            this.roadAssetPool = roadAssetPool;
        }

        protected override void Update(float t)
        {
            World.Query(in DELETE_ROAD_QUERY,
                (Entity e, ref RoadInfo roadInfo) =>
                {
                    roadInfo.Dispose(roadAssetPool);
                    World.Remove<RoadInfo, VisualSceneState, DeleteEntityIntention>(e);
                });
        }
      
    }
}