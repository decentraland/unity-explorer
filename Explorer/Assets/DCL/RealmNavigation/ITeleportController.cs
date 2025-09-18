﻿using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using DCL.Utilities;
using ECS.SceneLifeCycle.Reporting;
using System.Threading;
using UnityEngine;
using Utility.Types;

namespace DCL.RealmNavigation
{
    public interface ITeleportController
    {
        void StartTeleportToSpawnPoint(SceneEntityDefinition sceneDataSceneEntityDefinition, CancellationToken ct);
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

    public class StartParcel
    {
        private Vector2Int value;
        private bool consumed;

        public StartParcel(Vector2Int value)
        {
            this.value = value;
        }

        public bool IsConsumed() =>
            consumed;

        public AssignResult Assign(Vector2Int newParcel)
        {
            if (consumed) return AssignResult.ParcelAlreadyConsumed;
            value = newParcel;
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
