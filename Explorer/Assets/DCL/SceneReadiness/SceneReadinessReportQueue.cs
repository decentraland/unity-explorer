using DCL.Optimization.Pools;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SceneReadiness
{
    public class SceneReadinessReportQueue : ISceneReadinessReportQueue
    {
        private static readonly ListObjectPool<SceneReadinessReport> REPORT_POOL = new (listInstanceDefaultCapacity: 1, maxSize: 10);

        private readonly Dictionary<Vector2Int, List<SceneReadinessReport>> queue = new (1);

        private readonly IScenesCache scenesCache;

        public SceneReadinessReportQueue(IScenesCache scenesCache)
        {
            this.scenesCache = scenesCache;
        }

        public void Enqueue(Vector2Int parcel, SceneReadinessReport report)
        {
            // Shortcut
            if (scenesCache.Contains(parcel))
            {
                // conclude immediately
                report.CompletionSource.TrySetResult();
            }

            if (!queue.TryGetValue(parcel, out List<SceneReadinessReport> list))
                queue[parcel] = list = REPORT_POOL.Get();

            list.Add(report);
        }

        public bool TryDequeue(IReadOnlyList<Vector2Int> parcels, out IReadOnlyList<SceneReadinessReport> report)
        {
            if (queue.Count == 0) // nothing to dequeue
            {
                report = null;
                return false;
            }

            for (var i = 0; i < parcels.Count; i++)
            {
                if (queue.TryGetValue(parcels[i], out List<SceneReadinessReport> list))
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
