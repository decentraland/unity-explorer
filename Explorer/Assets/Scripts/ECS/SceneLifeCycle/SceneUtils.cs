using DCL.AsyncLoadReporting;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;
using System;
using System.Collections.Generic;
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

        public static void ReportException(Exception exception, IReadOnlyList<Vector2Int> parcels, ISceneReadinessReportQueue sceneReadinessReportQueue)
        {
            if (sceneReadinessReportQueue.TryDequeue(parcels, out PooledLoadReportList? reports))
            {
                using PooledLoadReportList reportsValue = reports!.Value;

                for (var i = 0; i < reportsValue.Count; i++)
                {
                    AsyncLoadProcessReport report = reportsValue[i];
                    report.SetException(exception);
                }
            }
        }

        private static void ReportProgressFinished(PooledLoadReportList? reports)
        {
            using PooledLoadReportList reportsValue = reports!.Value;

            for (var i = 0; i < reportsValue.Count; i++)
            {
                AsyncLoadProcessReport report = reportsValue[i];
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
