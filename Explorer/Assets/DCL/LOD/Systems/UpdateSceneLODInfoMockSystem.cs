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
        private readonly IScenesCache scenesCache;


        public UpdateSceneLODInfoMockSystem(World world, ISceneReadinessReportQueue sceneReadinessReportQueue, IScenesCache scenesCache) : base(world)
        {
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.scenesCache = scenesCache;
        }

        protected override void Update(float t)
        {
            UpdateLODLevelQuery(World);
            UnloadLODQuery(World);
        }

        [Query]
        [All(typeof(SceneLODInfo), typeof(PartitionComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateLODLevel(ref SceneLODInfo sceneLODInfo, SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (sceneLODInfo.CurrentLODLevel != byte.MaxValue) return;
            
            //If LODs are not enabled, we can consider the scene as ready,
            //and check scene readiness so not to block the loading screen
            scenesCache.AddNonRealScene(sceneDefinitionComponent.Parcels);
            LODUtils.CheckSceneReadiness(sceneReadinessReportQueue, sceneDefinitionComponent);
            sceneLODInfo.CurrentLODLevel = 0;
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void UnloadLOD(in Entity entity, ref SceneLODInfo sceneLODInfo, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            World.Remove<SceneLODInfo, VisualSceneState, DeleteEntityIntention>(entity);
        }
    }
}