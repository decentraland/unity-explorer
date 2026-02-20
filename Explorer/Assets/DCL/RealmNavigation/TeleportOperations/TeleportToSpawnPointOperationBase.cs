using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.RealmNavigation.LoadingOperation;
using DCL.Utilities;
using DCL.Utility.Types;
using DCL.Utility.Exceptions;
using ECS;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Reporting;
using Microsoft.ClearScript;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Assertions;

namespace DCL.RealmNavigation.TeleportOperations
{
    public abstract class TeleportToSpawnPointOperationBase<TParams> : ILoadingOperation<TParams> where TParams: ILoadingOperationParams
    {
        private readonly ILoadingStatus loadingStatus;
        protected readonly IGlobalRealmController realmController;
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

        protected async UniTask<EnumResult<TaskError>> InternalExecuteAsync(TParams args, Vector2Int parcel, CancellationToken ct, bool allowsPositionOverride = false)
        {
            float finalizationProgress = loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.PlayerTeleporting);
            AsyncLoadProcessReport teleportLoadReport = args.Report.CreateChildReport(finalizationProgress);
            EnumResult<TaskError> res = await InitializeTeleportToSpawnPointAsync(teleportLoadReport, ct, parcel, allowsPositionOverride);
            args.Report.SetProgress(finalizationProgress);

            // See https://github.com/decentraland/unity-explorer/issues/4470: we should teleport the player even if the scene has javascript errors
            // See https://github.com/decentraland/unity-explorer/issues/6124 we should teleport the player even if the scene cannot be loaded due to a missing manifest
            // We need to prevent the error propagation, otherwise the load state remains invalid which provokes issues like the incapability of typing another command in the chat
            if (res.Error is { Exception: ScriptEngineException } or { Exception: ManifestNotFoundException })
            {
                ReportHub.LogError(ReportCategory.SCENE_LOADING, $"Error on teleport to spawn point {parcel}: {res.Error.Value.Exception}");
                return EnumResult<TaskError>.SuccessResult();
            }

            return res;
        }

        private async UniTask<EnumResult<TaskError>> InitializeTeleportToSpawnPointAsync(
            AsyncLoadProcessReport teleportLoadReport,
            CancellationToken ct,
            Vector2Int parcelToTeleport,
            bool allowsPositionOverride = false
        )
        {
            bool isWorld = realmController.RealmData.IsWorld();
            WaitForSceneReadiness? waitForSceneReadiness;

            try
            {
                if (isWorld)
                    waitForSceneReadiness = await TeleportToWorldSpawnPointAsync(parcelToTeleport, teleportLoadReport, allowsPositionOverride, ct);
                else
                    waitForSceneReadiness = await teleportController.TeleportToSceneSpawnPointAsync(parcelToTeleport, teleportLoadReport, ct);
            }
            catch (OperationCanceledException) { return EnumResult<TaskError>.CancelledResult(TaskError.Cancelled); }
            catch (TimeoutException e)
            {
                ReportHub.LogException(e, reportCategory);
                return EnumResult<TaskError>.ErrorResult(TaskError.Timeout, e.Message);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, reportCategory);
                return EnumResult<TaskError>.ErrorResult(TaskError.MessageError, e.Message, e);
            }

            // add camera sampling data to the camera entity to start partitioning
            Assert.IsTrue(cameraEntity.Configured);
            realmController.GlobalWorld.EcsWorld.Add(cameraEntity.Object, cameraSamplingData);
            return await waitForSceneReadiness.ToUniTask();
        }

        private async UniTask<WaitForSceneReadiness?> TeleportToWorldSpawnPointAsync(
            Vector2Int parcelToTeleport,
            AsyncLoadProcessReport processReport,
            bool allowsPositionOverride,
            CancellationToken ct
        )
        {
            //Wait for all scenes definition to be fetched before trying to teleport
            List<SceneEntityDefinition> scenes = await realmController.WaitForFixedScenePromisesAsync(ct);

            WorldManifest manifest = realmController.RealmData.WorldManifest;
            if (!manifest.IsEmpty)
            {
                // If the WorldManifest defines an explicit spawn coordinate,
                // use it when its allowed by the teleport params (meaning that no explicit coordinate has been defined)
                // It will always be in bound
                if (allowsPositionOverride)
                    parcelToTeleport = new Vector2Int(manifest.spawn_coordinate.x, manifest.spawn_coordinate.y);
            }
            else
            {
                bool isSceneContained = false;
                // Check if result contains the requested parcel.
                foreach (var sceneEntityDefinition in scenes)
                {
                    if (sceneEntityDefinition.Contains(parcelToTeleport))
                    {
                        isSceneContained = true;
                        break;
                    }
                }

                // If no parcel is present on any scene, teleport to the first Decoded Base
                if (!isSceneContained)
                    parcelToTeleport = scenes[0].metadata.scene.DecodedBase;
            }

            WaitForSceneReadiness? waitForSceneReadiness =
                await teleportController.TeleportToSceneSpawnPointAsync(parcelToTeleport, processReport, ct);

            return waitForSceneReadiness;
        }

        public abstract UniTask<EnumResult<TaskError>> ExecuteAsync(TParams args, CancellationToken ct);
    }
}
