using DCL.Utilities;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.SceneLifeCycle.Reporting
{
    public partial interface ISceneReadinessReportQueue
    {
        void Enqueue(Vector2Int parcel, AsyncLoadProcessReport report);

        bool TryDequeue(IReadOnlyList<Vector2Int> parcels, out PooledLoadReportList? report);

        bool TryDequeue(Vector2Int parcel, out PooledLoadReportList? report);

        /// <summary>
        ///     Non-consuming check: true when someone is waiting to land on any of the given parcels
        /// </summary>
        bool HasReport(IReadOnlyList<Vector2Int> parcels);
    }
}
