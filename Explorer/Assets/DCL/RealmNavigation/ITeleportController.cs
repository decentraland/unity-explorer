using Cysharp.Threading.Tasks;
using DCL.Utilities;
using ECS.SceneLifeCycle.Reporting;
using System.Threading;
using UnityEngine;
using Utility.Types;

namespace DCL.RealmNavigation
{
    public interface ITeleportController
    {
        UniTask<WaitForSceneReadiness?> TeleportToSceneSpawnPointAsync(Vector2Int parcel, AsyncLoadProcessReport loadReport, CancellationToken ct);

        UniTask TeleportToParcelAsync(Vector2Int parcel, AsyncLoadProcessReport loadReport, CancellationToken ct);
    }

    public static class TeleportControllerExtensions
    {
        public static async UniTask<EnumResult<TaskError>> TryTeleportToSceneSpawnPointAsync(this ITeleportController teleportController, Vector2Int parcel, AsyncLoadProcessReport loadReport, CancellationToken ct)
        {
            WaitForSceneReadiness? waitForSceneReadiness = await teleportController.TeleportToSceneSpawnPointAsync(parcel, loadReport, ct);
            return await waitForSceneReadiness.ToUniTask();
        }
    }
}
