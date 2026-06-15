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

        /// <param name="landOnParcel">When true, land at <paramref name="parcel" /> itself instead of the scene's spawn point.</param>
        UniTask<WaitForSceneReadiness?> TeleportToSceneSpawnPointAsync(Vector2Int parcel, AsyncLoadProcessReport loadReport, CancellationToken ct, bool landOnParcel = false);

        UniTask TeleportToParcelAsync(Vector2Int parcel, AsyncLoadProcessReport loadReport, CancellationToken ct);
    }

    public static class TeleportControllerExtensions
    {
        public static async UniTask<EnumResult<TaskError>> TryTeleportToSceneSpawnPointAsync(this ITeleportController teleportController, Vector2Int parcel, AsyncLoadProcessReport loadReport, CancellationToken ct, bool landOnParcel = false)
        {
            WaitForSceneReadiness? waitForSceneReadiness = await teleportController.TeleportToSceneSpawnPointAsync(parcel, loadReport, ct, landOnParcel);
            return await waitForSceneReadiness.ToUniTask();
        }
    }

    public class StartParcel
    {
        private Vector2Int value;
        private bool consumed;

        public StartParcel(Vector2Int value)
        {
            this.value = value;
        }

        /// <summary>
        ///     When true, the startup teleport should land at <see cref="value" /> itself rather than the
        ///     scene's spawn point (set by a land-on-parcel deep link).
        /// </summary>
        public bool LandOnParcel { get; private set; }

        public bool IsConsumed() =>
            consumed;

        public AssignResult Assign(Vector2Int newParcel, bool landOnParcel = false)
        {
            if (consumed) return AssignResult.ParcelAlreadyConsumed;
            value = newParcel;
            LandOnParcel = landOnParcel;
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
