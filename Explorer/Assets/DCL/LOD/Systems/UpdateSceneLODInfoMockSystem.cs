using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.LOD.Components;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;

namespace DCL.LOD.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [LogCategory(ReportCategory.LOD)]
    public partial class UpdateSceneLODInfoMockSystem : BaseUnityLoopSystem
    {
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;


        public UpdateSceneLODInfoMockSystem(World world, ISceneReadinessReportQueue sceneReadinessReportQueue) : base(world)
        {
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
        }

        protected override void Update(float t)
        {
            UpdateLODLevelQuery(World);
            UnloadLODQuery(World);
        }

        [Query]
        [All(typeof(SceneLODInfo), typeof(PartitionComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateLODLevel(SceneDefinitionComponent sceneDefinitionComponent)
        {
            //If LODs are not enabled, we can consider the scene as ready,
            //and check scene readiness so not to block the loading screen
            LODUtils.CheckSceneReadiness(sceneReadinessReportQueue, sceneDefinitionComponent);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void UnloadLOD(in Entity entity, ref SceneLODInfo sceneLODInfo, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            World.Remove<SceneLODInfo, VisualSceneState, DeleteEntityIntention>(entity);
        }
    }
}