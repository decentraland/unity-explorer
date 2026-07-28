using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using DCL.Utilities;
using DCL.Utility.Types;
using ECS.SceneLifeCycle.Reporting;
using System.Threading;
using UnityEngine;

namespace DCL.RealmNavigation
{
    public interface ITeleportController
    {
        void StartTeleportToSpawnPoint(SceneEntityDefinition sceneDataSceneEntityDefinition, CancellationToken ct);

        /// <param name="parcel">The parcel to teleport to.</param>
        /// <param name="loadReport">Reports the progress of the scene load.</param>
        /// <param name="ct">Cancellation token for the teleport operation.</param>
        /// <param name="landOnParcel">When true, land at <paramref name="parcel" /> itself instead of the scene's spawn point.</param>
        /// <param name="spawnPointName">When set, land at the scene's spawn point with this name (case-insensitive); an unmatched name falls back to the default selection.</param>
        UniTask<WaitForSceneReadiness?> TeleportToSceneSpawnPointAsync(Vector2Int parcel, AsyncLoadProcessReport loadReport, CancellationToken ct, bool landOnParcel = false, string? spawnPointName = null);

        UniTask TeleportToParcelAsync(Vector2Int parcel, AsyncLoadProcessReport loadReport, CancellationToken ct);
    }

    public static class TeleportControllerExtensions
    {
        public static async UniTask<EnumResult<TaskError>> TryTeleportToSceneSpawnPointAsync(this ITeleportController teleportController, Vector2Int parcel, AsyncLoadProcessReport loadReport, CancellationToken ct, bool landOnParcel = false, string? spawnPointName = null)
        {
            WaitForSceneReadiness? waitForSceneReadiness = await teleportController.TeleportToSceneSpawnPointAsync(parcel, loadReport, ct, landOnParcel, spawnPointName);
            return await waitForSceneReadiness.ToUniTask();
        }
    }

    public class StartParcel
    {
        private Vector2Int value;
        private bool consumed;

        public StartParcel(Vector2Int value, string? spawnPointName = null)
        {
            this.value = value;
            SpawnPointName = spawnPointName;
        }

        public string? SpawnPointName { get; private set; }

        public bool IsConsumed() =>
            consumed;

        public AssignResult Assign(Vector2Int newParcel, string? newSpawnPointName = null)
        {
            if (consumed) return AssignResult.ParcelAlreadyConsumed;
            value = newParcel;
            SpawnPointName = newSpawnPointName;
            return AssignResult.Ok;
        }

        public Vector2Int ConsumeByTeleportOperation()
        {
            consumed = true;
            return value;
        }

        public Vector2Int Peek() =>
            value;
    }

    public enum AssignResult
    {
        Ok,
        ParcelAlreadyConsumed,
    }
}
