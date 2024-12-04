using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Landscape;
using DCL.MapRenderer;
using DCL.MapRenderer.MapLayers;
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
using DCL.FeatureFlags;
using DCL.Ipfs;
using DCL.LOD;
using DCL.Optimization.PerformanceBudgeting;
using DCL.ResourcesUnloading;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using Global.Dynamic.TeleportOperations;
using UnityEngine;
using UnityEngine.Assertions;
using Utility.Types;

namespace Global.Dynamic
{
    public class RealmNavigator : IRealmNavigator
    {
        private const int MAX_REALM_CHANGE_RETRIES = 3;

        private readonly ILoadingScreen loadingScreen;
        private readonly IMapRenderer mapRenderer;
        private readonly IGlobalRealmController realmController;
        private readonly ITeleportController teleportController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly World globalWorld;
        private readonly RoadAssetsPool roadAssetsPool;
        private readonly SatelliteFloor satelliteFloor;
        private readonly ObjectProxy<Entity> cameraEntity;
        private readonly CameraSamplingData cameraSamplingData;
        private readonly FeatureFlagsCache featureFlagsCache;

        private Vector2Int currentParcel;

        private readonly ITeleportOperation[] realmChangeOperations;
        private readonly ITeleportOperation[] teleportInSameRealmOperation;
        private readonly ILoadingStatus loadingStatus;
        private readonly ILandscape landscape;

        public event Action<RealmType>? RealmChanged;

        public RealmNavigator(
            ILoadingScreen loadingScreen,
            IMapRenderer mapRenderer,
            IGlobalRealmController realmController,
            ITeleportController teleportController,
            IRoomHub roomHub,
            IRemoteEntities remoteEntities,
            IDecentralandUrlsSource decentralandUrlsSource,
            World globalWorld,
            RoadAssetsPool roadAssetsPool,
            SatelliteFloor satelliteFloor,
            ObjectProxy<Entity> cameraEntity,
            CameraSamplingData cameraSamplingData,
            ILoadingStatus loadingStatus,
            ICacheCleaner cacheCleaner,
            IMemoryUsageProvider memoryUsageProvider,
            ILandscape landscape,
            FeatureFlagsCache featureFlagsCache)
        {
            this.loadingScreen = loadingScreen;
            this.mapRenderer = mapRenderer;
            this.realmController = realmController;
            this.teleportController = teleportController;
            this.satelliteFloor = satelliteFloor;
            this.cameraEntity = cameraEntity;
            this.cameraSamplingData = cameraSamplingData;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.globalWorld = globalWorld;
            this.loadingStatus = loadingStatus;
            this.landscape = landscape;
            this.roadAssetsPool = roadAssetsPool;
            this.featureFlagsCache = featureFlagsCache;
            var livekitTimeout = TimeSpan.FromSeconds(10f);

            realmChangeOperations = new ITeleportOperation[]
            {
                new RestartLoadingStatus(),
                new RemoveRemoteEntitiesTeleportOperation(remoteEntities, globalWorld),
                new StopRoomAsyncTeleportOperation(roomHub, livekitTimeout),
                new RemoveCameraSamplingDataTeleportOperation(globalWorld, cameraEntity),
                new DestroyAllRoadAssetsTeleportOperation(globalWorld, roadAssetsPool),
                new ChangeRealmTeleportOperation(this),
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
                new MoveToParcelInSameRealmTeleportOperation(this),
                new CompleteLoadingStatus()
            };
        }

        public bool CheckIsNewRealm(URLDomain realm)
        {
            if (!realmController.RealmData.Configured)
                return true;

            if (realm == realmController.CurrentDomain || realm == realmController.RealmData.Ipfs.CatalystBaseUrl)
                return false;

            return true;
        }

        public async UniTask<bool> CheckRealmIsReachableAsync(URLDomain realm, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (!await realmController.IsReachableAsync(realm, ct))
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

            var loadResult
                = await loadingScreen.ShowWhileExecuteTaskAsync(DoChangeRealmAsync(realm, realmController.CurrentDomain, parcelToTeleport), ct);

            if (!loadResult.Success)
            {
                if (!globalWorld.Has<CameraSamplingData>(cameraEntity.Object))
                    globalWorld.Add(cameraEntity.Object, cameraSamplingData);

                ReportHub.LogError(ReportCategory.REALM,
                    $"Error trying to teleport to a realm {realm}: {loadResult.ErrorMessage}");

                return EnumResult<ChangeRealmError>.ErrorResult(ChangeRealmError.MessageError, loadResult.ErrorMessage!);
            }

            return EnumResult<ChangeRealmError>.SuccessResult();
        }

        private static async UniTask<Result> ExecuteTeleportOperationsAsync(TeleportParams teleportParams, ITeleportOperation[] ops, string logOpName, int attemptsCount, CancellationToken ct)
        {
            var lastOpResult = Result.SuccessResult();

            attemptsCount = Mathf.Max(1, attemptsCount);

            for (var attempt = 0; attempt < attemptsCount; attempt++)
            {
                lastOpResult = Result.SuccessResult();

                foreach (ITeleportOperation op in ops)
                {
                    try
                    {
                        lastOpResult = await op.ExecuteAsync(teleportParams, ct);

                        if (!lastOpResult.Success)
                        {
                            ReportHub.LogError(ReportCategory.REALM, $"Operation failed on {logOpName} attempt {attempt + 1}/{attemptsCount}: {lastOpResult.ErrorMessage}");
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        lastOpResult = Result.ErrorResult($"Unhandled exception on {logOpName} attempt {attempt + 1}/{attemptsCount}: {e}");
                        ReportHub.LogError(ReportCategory.REALM, lastOpResult.ErrorMessage!);
                        break;
                    }
                }

                if (lastOpResult.Success)
                    break;

                if (ct.IsCancellationRequested)
                {
                    lastOpResult = Result.CancelledResult();
                    break;
                }
            }

            return lastOpResult;
        }

        private Func<AsyncLoadProcessReport, CancellationToken, UniTask<Result>> DoChangeRealmAsync(URLDomain realm, URLDomain? fallbackRealm, Vector2Int parcelToTeleport)
        {
            return async (parentLoadReport, ct) =>
            {
                const string LOG_NAME = "Changing Realm";
                const string FALLBACK_LOG_NAME = "Returning to Previous Realm";

                if (ct.IsCancellationRequested)
                    return Result.CancelledResult();

                var teleportParams = new TeleportParams
                {
                    CurrentDestinationParcel = parcelToTeleport,
                    CurrentDestinationRealm = realm, ParentReport = parentLoadReport, LoadingStatus = loadingStatus
                };

                Result opResult = await ExecuteTeleportOperationsAsync(teleportParams, realmChangeOperations, LOG_NAME, MAX_REALM_CHANGE_RETRIES, ct);

                if (opResult.Success)
                    return opResult;

                if (!fallbackRealm.HasValue)
                {
                    ReportHub.LogWarning(ReportCategory.REALM, "All attempts failed. No fallback realm is provided.");
                    return opResult;
                }

                // All retries failed, try with the previous realm and parcel
                ReportHub.LogWarning(ReportCategory.REALM, "All attempts failed. Trying with previous realm and parcel.");

                teleportParams.CurrentDestinationRealm = fallbackRealm.Value;
                teleportParams.CurrentDestinationParcel = currentParcel;

                opResult = await ExecuteTeleportOperationsAsync(teleportParams, realmChangeOperations, FALLBACK_LOG_NAME, 1, ct);

                if (!opResult.Success)
                    parentLoadReport.SetProgress(1);

                return opResult;
            };
        }

        public async UniTask InitializeTeleportToSpawnPointAsync(AsyncLoadProcessReport teleportLoadReport,
            CancellationToken ct, Vector2Int parcelToTeleport)
        {
            bool isWorld = realmController.RealmData.ScenesAreFixed;
            UniTask waitForSceneReadiness;

            if (isWorld)
                waitForSceneReadiness = await TeleportToWorldSpawnPointAsync(parcelToTeleport, teleportLoadReport, ct);
            else
            {
                if (parcelToTeleport == Vector2Int.zero &&
                    featureFlagsCache.Configuration.IsEnabled(FeatureFlagsStrings.GENESIS_STARTING_PARCEL) &&
                    featureFlagsCache.Configuration.TryGetTextPayload(FeatureFlagsStrings.GENESIS_STARTING_PARCEL, FeatureFlagsStrings.STRING_VARIANT, out string? parcelCoords))
                    RealmHelper.TryParseParcelFromString(parcelCoords, out parcelToTeleport);

                waitForSceneReadiness = await TeleportToParcelAsync(parcelToTeleport, teleportLoadReport, ct);
            }

            // add camera sampling data to the camera entity to start partitioning
            Assert.IsTrue(cameraEntity.Configured);
            globalWorld.Add(cameraEntity.Object, cameraSamplingData);
            await waitForSceneReadiness;
        }

        public async UniTask<Result> TryInitializeTeleportToParcelAsync(
            Vector2Int parcel,
            CancellationToken ct,
            bool isLocal = false,
            bool forceChangeRealm = false
        )
        {
            if (ct.IsCancellationRequested)
                return Result.CancelledResult();

            Result parcelCheckResult = landscape.IsParcelInsideTerrain(parcel, isLocal);

            if (!parcelCheckResult.Success)
                return parcelCheckResult;

            if (forceChangeRealm || (!isLocal && !realmController.IsGenesis()))
            {
                var url = URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.Genesis));
                var enumResult = await TryChangeRealmAsync(url, ct, parcel);
                return enumResult.AsResult();
            }

            Result loadResult = await loadingScreen.ShowWhileExecuteTaskAsync(TryTeleportAsync(parcel), ct);

            if (!loadResult.Success)
                ReportHub.LogError(
                    ReportCategory.SCENE_LOADING,
                    $"Error trying to teleport to a parcel {parcel}: {loadResult.ErrorMessage}"
                );

            return loadResult;
        }

        private Func<AsyncLoadProcessReport, CancellationToken, UniTask<Result>> TryTeleportAsync(Vector2Int parcel)
        {
            return async (parentLoadReport, ct) =>
            {
                const string LOG_NAME = "Teleporting to Parcel";

                if (ct.IsCancellationRequested)
                    return Result.CancelledResult();

                var teleportParams = new TeleportParams
                {
                    ParentReport = parentLoadReport, CurrentDestinationParcel = parcel, LoadingStatus = loadingStatus
                };

                Result result = await ExecuteTeleportOperationsAsync(teleportParams, teleportInSameRealmOperation, LOG_NAME, 1, ct);
                parentLoadReport.SetProgress(1);
                return result;
            };
        }

        public void SwitchMiscVisibilityAsync()
        {
            var type = realmController.Type;
            bool isGenesis = type is RealmType.GenesisCity;

            RealmChanged?.Invoke(type);
            mapRenderer.SetSharedLayer(MapLayer.PlayerMarker, isGenesis);
            satelliteFloor.SetCurrentlyInGenesis(isGenesis);
            roadAssetsPool.SwitchVisibility(isGenesis);
        }

        public async UniTask<UniTask> TeleportToParcelAsync(Vector2Int parcel, AsyncLoadProcessReport processReport,
            CancellationToken ct)
        {
            WaitForSceneReadiness? waitForSceneReadiness =
                await teleportController.TeleportToSceneSpawnPointAsync(parcel, processReport, ct);

            return waitForSceneReadiness.ToUniTask();
        }

        private async UniTask<UniTask> TeleportToWorldSpawnPointAsync(Vector2Int parcelToTeleport,
            AsyncLoadProcessReport processReport, CancellationToken ct)
        {
            AssetPromise<SceneEntityDefinition, GetSceneDefinition>[]? promises = await realmController.WaitForFixedScenePromisesAsync(ct);

            if (!promises.Any(p =>
                    p.Result.HasValue
                    && (p.Result.Value.Asset?.metadata.scene.DecodedParcels.Contains(parcelToTeleport) ?? false)
                ))
                parcelToTeleport = promises[0].Result!.Value.Asset!.metadata.scene.DecodedBase;

            WaitForSceneReadiness? waitForSceneReadiness =
                await teleportController.TeleportToSceneSpawnPointAsync(parcelToTeleport, processReport, ct);

            return waitForSceneReadiness.ToUniTask();
        }

        public async UniTask ChangeRealmAsync(URLDomain realm, CancellationToken ct)
        {
            await realmController.SetRealmAsync(realm, ct);
            SwitchMiscVisibilityAsync();
        }
    }
}
