using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using ECS.SceneLifeCycle.Reporting;
using System.Threading;
using UnityEngine;

namespace DCL.ParcelsService
{
    public interface ITeleportController
    {
        UniTask<WaitForSceneReadiness?> TeleportToSceneSpawnPointAsync(Vector2Int parcel, AsyncLoadProcessReport loadReport, CancellationToken ct);

        UniTask TeleportToParcelAsync(Vector2Int parcel, AsyncLoadProcessReport loadReport, CancellationToken ct);
    }
}
