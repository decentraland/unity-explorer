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

    public static class TeleportControllerExtensions
    {
        public static async UniTask TryTeleportToSceneSpawnPointAsync(this ITeleportController teleportController, Vector2Int parcel, AsyncLoadProcessReport loadReport, CancellationToken ct)
        {
            WaitForSceneReadiness? waitForSceneReadiness = await teleportController.TeleportToSceneSpawnPointAsync(parcel, loadReport, ct);
            await waitForSceneReadiness.ToUniTask();
        }
    }
}
