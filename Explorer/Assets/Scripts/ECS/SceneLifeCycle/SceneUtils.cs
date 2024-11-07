using Arch.Core;
using DCL.AsyncLoadReporting;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;
using UnityEngine;

namespace ECS.SceneLifeCycle
{
    public static class SceneUtils
    {
        public static void ReportSceneLoaded(SceneDefinitionComponent sceneDefinitionComponent,
            ISceneReadinessReportQueue sceneReadinessReportQueue, IScenesCache scenesCache)
        {
            scenesCache.AddNonRealScene(sceneDefinitionComponent.Parcels);

            if (sceneReadinessReportQueue.TryDequeue(sceneDefinitionComponent.Parcels, out PooledLoadReportList? reports))
                ReportProgressFinished(reports);
        }

        private static void ReportProgressFinished(PooledLoadReportList? reports)
        {
            for (var i = 0; i < reports!.Value.Count; i++)
            {
                AsyncLoadProcessReport report = reports.Value[i];
                report.SetProgress(1f);
            }
        }

        public static EmptySceneComponent CreateEmptyScene(Vector2Int parcel, ISceneReadinessReportQueue sceneReadinessReportQueue, IScenesCache scenesCache)
        {
            scenesCache.AddNonRealScene(parcel);

            if (sceneReadinessReportQueue.TryDequeue(parcel, out PooledLoadReportList? reports))
                ReportProgressFinished(reports);

            return EmptySceneComponent.Create();
        }
    }
}
