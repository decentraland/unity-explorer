using DCL.AsyncLoadReporting;
using DCL.Optimization.Pools;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.SceneLifeCycle.Reporting
{
    public class SceneReadinessReportQueue : ISceneReadinessReportQueue
    {
        private static readonly ListObjectPool<AsyncLoadProcessReport> REPORT_POOL = new (listInstanceDefaultCapacity: 1, maxSize: 10);

        private readonly Dictionary<Vector2Int, PooledLoadReportList> queue = new (1);

        private readonly IScenesCache scenesCache;

        public SceneReadinessReportQueue(IScenesCache scenesCache)
        {
            this.scenesCache = scenesCache;
        }

        public void Enqueue(Vector2Int parcel, AsyncLoadProcessReport report)
        {
            // Shortcut
            if (scenesCache.Contains(parcel))
            {
                // conclude immediately
                report.SetProgress(1f);
            }

            if (!queue.TryGetValue(parcel, out PooledLoadReportList queuedReport))
                queue[parcel] = queuedReport = new PooledLoadReportList(REPORT_POOL);

            queuedReport.reports.Add(report);
        }

        public bool TryDequeue(IReadOnlyList<Vector2Int> parcels, out PooledLoadReportList? report)
        {
            if (queue.Count == 0) // nothing to dequeue
            {
                report = null;
                return false;
            }

            for (var i = 0; i < parcels.Count; i++)
            {
                if (queue.TryGetValue(parcels[i], out PooledLoadReportList list))
                {
                    report = list;
                    queue.Remove(parcels[i]);
                    return true;
                }
            }

            report = null;
            return false;
        }
    }
}
