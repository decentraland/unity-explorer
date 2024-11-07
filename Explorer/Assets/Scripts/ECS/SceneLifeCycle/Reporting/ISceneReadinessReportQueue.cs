using DCL.AsyncLoadReporting;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.SceneLifeCycle.Reporting
{
    public partial interface ISceneReadinessReportQueue
    {
        void Enqueue(Vector2Int parcel, AsyncLoadProcessReport report);

        bool TryDequeue(IReadOnlyList<Vector2Int> parcels, out PooledLoadReportList? report);

        bool TryDequeue(Vector2Int parcel, out PooledLoadReportList? report);
    }
}
