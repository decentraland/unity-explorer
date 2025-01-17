﻿using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Entities;
using DCL.ParcelsService;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.UserInAppInitializationFlow;
using DCL.Utilities;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Realm;
using ECS.SceneLifeCycle.Reporting;
using System;
using System.Linq;
using System.Threading;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.LOD;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.ResourcesUnloading;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using Global.Dynamic.TeleportOperations;
using Segment.Serialization;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Utility;
using Utility.TeleportBus;
using Utility.Types;

namespace Global.Dynamic
{
    public class RealmNavigator : IRealmNavigator
    {
        private const int MAX_REALM_CHANGE_RETRIES = 3;

        private readonly ILoadingScreen loadingScreen;
        private readonly IGlobalRealmController realmController;
        private readonly ITeleportController teleportController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly World globalWorld;
        private readonly ObjectProxy<Entity> cameraEntity;
        private readonly CameraSamplingData cameraSamplingData;
        private readonly ITeleportBusController teleportBusController;

        private Vector2Int currentParcel;

        private readonly ITeleportOperation[] realmChangeOperations;
        private readonly ITeleportOperation[] teleportInSameRealmOperation;
        private readonly ILoadingStatus loadingStatus;
        private readonly IAnalyticsController analyticsController;
        private readonly ILandscape landscape;

        public RealmNavigator(
            ILoadingScreen loadingScreen,
            IGlobalRealmController realmController,
            ITeleportController teleportController,
            IRoomHub roomHub,
            IRemoteEntities remoteEntities,
            IDecentralandUrlsSource decentralandUrlsSource,
            World globalWorld,
            RoadAssetsPool roadAssetsPool,
            ObjectProxy<Entity> cameraEntity,
            CameraSamplingData cameraSamplingData,
            ILoadingStatus loadingStatus,
            ICacheCleaner cacheCleaner,
            IMemoryUsageProvider memoryUsageProvider,
            ITeleportBusController teleportBusController,
            ILandscape landscape,
            IAnalyticsController analyticsController,
            IRealmMisc realmMisc)
        {
            this.loadingScreen = loadingScreen;
            this.realmController = realmController;
            this.teleportController = teleportController;
            this.cameraEntity = cameraEntity;
            this.cameraSamplingData = cameraSamplingData;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.globalWorld = globalWorld;
            this.loadingStatus = loadingStatus;
            this.teleportBusController = teleportBusController;
            this.analyticsController = analyticsController;
            this.landscape = landscape;
            var livekitTimeout = TimeSpan.FromSeconds(10f);

            realmChangeOperations = new ITeleportOperation[]
            {
                new RestartLoadingStatus(),
                new RemoveRemoteEntitiesTeleportOperation(remoteEntities, globalWorld),
                new StopRoomAsyncTeleportOperation(roomHub, livekitTimeout),
                new RemoveCameraSamplingDataTeleportOperation(globalWorld, cameraEntity),
                new DestroyAllRoadAssetsTeleportOperation(globalWorld, roadAssetsPool),
                new ChangeRealmTeleportOperation(realmController, realmMisc),
                new AnalyticsFlushTeleportOperation(analyticsController),
                new LoadLandscapeTeleportOperation(landscape),
                new PrewarmRoadAssetPoolsTeleportOperation(realmController, roadAssetsPool),
                new UnloadCacheImmediateTeleportOperation(cacheCleaner, memoryUsageProvider),
                new MoveToParcelInNewRealmTeleportOperation(this),
                new RestartRoomAsyncTeleportOperation(roomHub, livekitTimeout),
                new CompleteLoadingStatus()
            };

            teleportInSameRealmOperation = new ITeleportOperation[]
            {
                new RestartLoadingStatus(),
                new UnloadCacheImmediateTeleportOperation(cacheCleaner, memoryUsageProvider),
                new MoveToParcelInSameRealmTeleportOperation(teleportController),
                new CompleteLoadingStatus()
            };
        }

        private bool CheckIsNewRealm(URLDomain realm)
        {
            if (!realmController.RealmData.Configured)
                return true;

            if (realm == realmController.CurrentDomain || realm == realmController.RealmData.Ipfs.CatalystBaseUrl)
                return false;

            return true;
        }

        public async UniTask<EnumResult<ChangeRealmError>> TryChangeRealmAsync(
            URLDomain realm,
            CancellationToken ct,
            Vector2Int parcelToTeleport = default
        )
        {
            if (ct.IsCancellationRequested)
                return EnumResult<ChangeRealmError>.ErrorResult(ChangeRealmError.ChangeCancelled);

            if (CheckIsNewRealm(realm) == false)
                return EnumResult<ChangeRealmError>.ErrorResult(ChangeRealmError.SameRealm);

            if (await realmController.IsReachableAsync(realm, ct) == false)
                return EnumResult<ChangeRealmError>.ErrorResult(ChangeRealmError.NotReachable);

            var operation = DoChangeRealmAsync(realm, realmController.CurrentDomain, parcelToTeleport);
            var loadResult = await loadingScreen.ShowWhileExecuteTaskAsync(operation, ct);

            if (!loadResult.Success)
            {
                if (!globalWorld.Has<CameraSamplingData>(cameraEntity.Object))
                    globalWorld.Add(cameraEntity.Object, cameraSamplingData);

                ReportHub.LogError(ReportCategory.REALM,
                    $"Error trying to teleport to a realm {realm}: {loadResult.Error.Value.Message}");

                return loadResult.As(ChangeRealmErrors.AsChangeRealmError);
            }

            return EnumResult<ChangeRealmError>.SuccessResult();
        }

        private async UniTask<EnumResult<TaskError>> ExecuteTeleportOperationsAsync(
            TeleportParams teleportParams,
            IReadOnlyCollection<ITeleportOperation> ops,
            string logOpName,
            int attemptsCount,
            CancellationToken ct
        )
        {
            var lastOpResult = EnumResult<TaskError>.SuccessResult();

            attemptsCount = Mathf.Max(1, attemptsCount);

            for (var attempt = 0; attempt < attemptsCount; attempt++)
            {
                lastOpResult = EnumResult<TaskError>.SuccessResult();

                foreach (ITeleportOperation op in ops)
                {
                    try
                    {
                        lastOpResult = await op.ExecuteAsync(teleportParams, ct);

                        if (!lastOpResult.Success)
                        {
                            ReportHub.LogError(
                                ReportCategory.REALM,
                                $"Operation failed on {logOpName} attempt {attempt + 1}/{attemptsCount}: {lastOpResult.AsResult().ErrorMessage}"
                            );

                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        lastOpResult = EnumResult<TaskError>.ErrorResult(TaskError.UnexpectedException, $"Unhandled exception on {logOpName} attempt {attempt + 1}/{attemptsCount}: {e}");
                        ReportHub.LogError(ReportCategory.REALM, lastOpResult.AsResult().ErrorMessage!);
                        break;
                    }
                }

                if (lastOpResult.Success)
                    break;

                if (ct.IsCancellationRequested)
                {
                    lastOpResult = EnumResult<TaskError>.CancelledResult(TaskError.Cancelled);
                    break;
                }
            }

            if (lastOpResult.Success == false)
                analyticsController.Track(
                    AnalyticsEvents.General.LOADING_ERROR,
                    new JsonObject
                    {
                        ["type"] = "teleportation",
                        ["message"] = lastOpResult.AsResult().ErrorMessage,
                    }
                );

            return lastOpResult;
        }

        private Func<AsyncLoadProcessReport, CancellationToken, UniTask<EnumResult<TaskError>>> DoChangeRealmAsync(URLDomain realm, URLDomain? fallbackRealm, Vector2Int parcelToTeleport)
        {
            return async (parentLoadReport, ct) =>
            {
                const string LOG_NAME = "Changing Realm";
                const string FALLBACK_LOG_NAME = "Returning to Previous Realm";

                if (ct.IsCancellationRequested)
                    return EnumResult<TaskError>.CancelledResult(TaskError.Cancelled);

                var teleportParams = new TeleportParams(realm, parcelToTeleport, parentLoadReport, loadingStatus);

                EnumResult<TaskError> opResult = await ExecuteTeleportOperationsAsync(teleportParams, realmChangeOperations, LOG_NAME, MAX_REALM_CHANGE_RETRIES, ct);

                if (opResult.Success)
                    return opResult;

                if (!fallbackRealm.HasValue)
                {
                    ReportHub.LogWarning(ReportCategory.REALM, "All attempts failed. No fallback realm is provided.");
                    return opResult;
                }

                // All retries failed, try with the previous realm and parcel
                ReportHub.LogWarning(ReportCategory.REALM, "All attempts failed. Trying with previous realm and parcel.");

                teleportParams.ChangeDestination(fallbackRealm.Value, currentParcel);

                opResult = await ExecuteTeleportOperationsAsync(teleportParams, realmChangeOperations, FALLBACK_LOG_NAME, 1, ct);

                if (!opResult.Success)
                    parentLoadReport.SetProgress(1);

                return opResult;
            };
        }

        public void RemoveCameraSamplingData()
        {
            if (globalWorld.Has<CameraSamplingData>(cameraEntity.Object))
                globalWorld.Remove<CameraSamplingData>(cameraEntity.Object);
        }

        public async UniTask InitializeTeleportToSpawnPointAsync(
            AsyncLoadProcessReport teleportLoadReport,
            CancellationToken ct,
            Vector2Int parcelToTeleport
        )
        {
            bool isWorld = realmController.Type is RealmType.World;
            WaitForSceneReadiness? waitForSceneReadiness;

            if (isWorld)
                waitForSceneReadiness = await TeleportToWorldSpawnPointAsync(parcelToTeleport, teleportLoadReport, ct);
            else
                waitForSceneReadiness = await teleportController.TeleportToSceneSpawnPointAsync(parcelToTeleport, teleportLoadReport, ct);

            // add camera sampling data to the camera entity to start partitioning
            Assert.IsTrue(cameraEntity.Configured);
            globalWorld.Add(cameraEntity.Object, cameraSamplingData);
            await waitForSceneReadiness.ToUniTask();
        }

        public async UniTask<EnumResult<TaskError>> TeleportToParcelAsync(
            Vector2Int parcel,
            CancellationToken ct,
            bool isLocal = false
        )
        {
            if (ct.IsCancellationRequested)
                return EnumResult<TaskError>.CancelledResult(TaskError.Cancelled);

            Result parcelCheckResult = landscape.IsParcelInsideTerrain(parcel, isLocal);

            if (!parcelCheckResult.Success)
                return parcelCheckResult.AsEnumResult(TaskError.MessageError);

            if (!isLocal && !realmController.IsGenesis())
            {
                var enumResult = await TryChangeToGenesisAsync(parcel, ct);
                return enumResult.As(ChangeRealmErrors.AsTaskError);
            }

            EnumResult<TaskError> loadResult = await loadingScreen.ShowWhileExecuteTaskAsync(TeleportToParcelAsyncOperation(parcel), ct);

            if (!loadResult.Success)
                ReportHub.LogError(
                    ReportCategory.SCENE_LOADING,
                    $"Error trying to teleport to a parcel {parcel}: {loadResult.Error!.Value.Message}"
                );

            return loadResult;
        }

        private async UniTask<EnumResult<ChangeRealmError>> TryChangeToGenesisAsync(Vector2Int parcel, CancellationToken ct)
        {
            var genesisUrl = URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.Genesis));
            var enumResult = await TryChangeRealmAsync(genesisUrl, ct, parcel);
            return enumResult;
        }

        private Func<AsyncLoadProcessReport, CancellationToken, UniTask<EnumResult<TaskError>>> TeleportToParcelAsyncOperation(Vector2Int parcel) =>
            async (parentLoadReport, ct) =>
            {
                const string LOG_NAME = "Teleporting to Parcel";

                if (ct.IsCancellationRequested)
                    return EnumResult<TaskError>.CancelledResult(TaskError.Cancelled);

                var teleportParams = new TeleportParams(
                    currentDestinationParcel: parcel,
                    loadingStatus: loadingStatus,
                    parentReport: parentLoadReport,
                    currentDestinationRealm: URLDomain.EMPTY
                );

                EnumResult<TaskError> result = await ExecuteTeleportOperationsAsync(teleportParams, teleportInSameRealmOperation, LOG_NAME, 1, ct);
                parentLoadReport.SetProgress(1);
                return result;
            };

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
    }
}
