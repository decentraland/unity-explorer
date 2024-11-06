using DCL.AsyncLoadReporting;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;

namespace ECS.SceneLifeCycle
{
    public static class SceneUtils
    {
        public static void ReportSceneLoaded(SceneDefinitionComponent sceneDefinitionComponent,
            ISceneReadinessReportQueue sceneReadinessReportQueue, IScenesCache scenesCache)
        {
            scenesCache.AddNonRealScene(sceneDefinitionComponent.Parcels);

            if (sceneReadinessReportQueue.TryDequeue(sceneDefinitionComponent.Parcels, out PooledLoadReportList? reports))
            {
                for (var i = 0; i < reports!.Value.Count; i++)
                {
                    AsyncLoadProcessReport report = reports.Value[i];
                    report.SetProgress(1f);
                }
            }
        }
    }
}
