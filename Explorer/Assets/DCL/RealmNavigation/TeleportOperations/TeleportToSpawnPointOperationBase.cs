using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.RealmNavigation.LoadingOperation;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using ECS;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using Microsoft.ClearScript;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Assertions;
using Utility.Types;

namespace DCL.RealmNavigation.TeleportOperations
{
    public abstract class TeleportToSpawnPointOperationBase<TParams> : ILoadingOperation<TParams> where TParams: ILoadingOperationParams
    {
        private readonly ILoadingStatus loadingStatus;
        private readonly IGlobalRealmController realmController;
        private readonly ObjectProxy<Entity> cameraEntity;
        private readonly CameraSamplingData cameraSamplingData;
        private readonly string reportCategory;
        private readonly ITeleportController teleportController;

        protected TeleportToSpawnPointOperationBase(ILoadingStatus loadingStatus, IGlobalRealmController realmController, ObjectProxy<Entity> cameraEntity, ITeleportController teleportController, CameraSamplingData cameraSamplingData,
            string reportCategory = ReportCategory.SCENE_LOADING)
        {
            this.loadingStatus = loadingStatus;
            this.realmController = realmController;
            this.cameraEntity = cameraEntity;
            this.teleportController = teleportController;
            this.cameraSamplingData = cameraSamplingData;
            this.reportCategory = reportCategory;
        }

        protected async UniTask<EnumResult<TaskError>> InternalExecuteAsync(TParams args, Vector2Int parcel, CancellationToken ct)
        {
            float finalizationProgress = loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.PlayerTeleporting);
            AsyncLoadProcessReport teleportLoadReport = args.Report.CreateChildReport(finalizationProgress);
            EnumResult<TaskError> res = await InitializeTeleportToSpawnPointAsync(teleportLoadReport, ct, parcel);
            args.Report.SetProgress(finalizationProgress);

            // See https://github.com/decentraland/unity-explorer/issues/4470: we should teleport the player even if the scene has javascript errors
            // We need to prevent the error propagation, otherwise the load state remains invalid which provokes issues like the incapability of typing another command in the chat
            if (res.Error is { Exception: ScriptEngineException })
            {
                ReportHub.LogError(ReportCategory.SCENE_LOADING, $"Error on teleport to spawn point {parcel}: {res.Error.Value.Exception}");
                return EnumResult<TaskError>.SuccessResult();
            }

            return res;
        }

        private async UniTask<EnumResult<TaskError>> InitializeTeleportToSpawnPointAsync(
            AsyncLoadProcessReport teleportLoadReport,
            CancellationToken ct,
            Vector2Int parcelToTeleport
        )
        {
            bool isWorld = realmController.RealmData.IsWorld();
            Result<WaitForSceneReadiness?> waitForSceneReadiness;

            if (isWorld)
                waitForSceneReadiness = await TeleportToWorldSpawnPointAsync(parcelToTeleport, teleportLoadReport, ct).SuppressToResultAsync(reportCategory);
            else
                waitForSceneReadiness = await teleportController.TeleportToSceneSpawnPointAsync(parcelToTeleport, teleportLoadReport, ct).SuppressToResultAsync(reportCategory);

            if (!waitForSceneReadiness.Success)
                return waitForSceneReadiness.AsEnumResult(TaskError.MessageError);

            // add camera sampling data to the camera entity to start partitioning
            Assert.IsTrue(cameraEntity.Configured);
            realmController.GlobalWorld.EcsWorld.Add(cameraEntity.Object, cameraSamplingData);
            return await waitForSceneReadiness.Value.ToUniTask();
        }

        private async UniTask<WaitForSceneReadiness?> TeleportToWorldSpawnPointAsync(
            Vector2Int parcelToTeleport,
            AsyncLoadProcessReport processReport,
            CancellationToken ct
        )
        {
            AssetPromise<SceneEntityDefinition, GetSceneDefinition>[]? promises = await realmController.WaitForFixedScenePromisesAsync(ct);

            if (!promises.Any(p =>
                    p.Result.HasValue
                    && (p.Result.Value.Asset?.metadata.scene.DecodedParcels.Contains(parcelToTeleport) ?? false)
                ))
                parcelToTeleport = promises[0].Result!.Value.Asset!.metadata.scene.DecodedBase;

            WaitForSceneReadiness? waitForSceneReadiness =
                await teleportController.TeleportToSceneSpawnPointAsync(parcelToTeleport, processReport, ct);

            return waitForSceneReadiness;
        }

        public abstract UniTask<EnumResult<TaskError>> ExecuteAsync(TParams args, CancellationToken ct);
    }
}
