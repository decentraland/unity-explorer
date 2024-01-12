using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SceneReadiness
{
    public interface ISceneReadinessReportQueue
    {
        void Enqueue(Vector2Int parcel, SceneReadinessReport report);

        bool TryDequeue(IReadOnlyList<Vector2Int> parcels, out IReadOnlyList<SceneReadinessReport> report);
    }
}
