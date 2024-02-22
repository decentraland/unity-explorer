using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using DCL.Diagnostics;
using DCL.LOD.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;

namespace DCL.LOD.Systems
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.LOD)]
    public partial class UnloadRoadSystem : BaseUnityLoopSystem
    {
        public UnloadRoadSystem(World world) : base(world)
        {
        }


        protected override void Update(float t)
        {
            UnloadRoadQuery(World);
        }
        
        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void UnloadRoad(in Entity entity, ref RoadInfo roadInfo)
        {
            roadInfo.Dispose(World);
            World.Remove<RoadInfo, VisualSceneState, DeleteEntityIntention>(entity);
        }
    }
}