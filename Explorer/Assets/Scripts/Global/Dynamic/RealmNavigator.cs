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
using DCL.Roads.Systems;
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
using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiling;
using DCL.ResourcesUnloading;
using DCL.Web3;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using Global.Dynamic.TeleportOperations;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using Utility;
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
        private readonly RoadPlugin roadsPlugin;
        private readonly TerrainGenerator genesisTerrain;
        private readonly WorldTerrainGenerator worldsTerrain;
        private readonly SatelliteFloor satelliteFloor;
        private readonly bool landscapeEnabled;
        private readonly ObjectProxy<Entity> cameraEntity;
        private readonly CameraSamplingData cameraSamplingData;
        private readonly bool isLocalSceneDevelopment;
        private readonly FeatureFlagsCache featureFlagsCache;

        private URLDomain currentRealm;
        private Vector2Int currentParcel;

        private readonly ITeleportOperation[] realmChangeOperations;
        private readonly ITeleportOperation[] teleportInSameRealmOperation;
        private readonly ILoadingStatus loadingStatus;

        public event Action<bool>? RealmChanged;

        public RealmNavigator(
            ILoadingScreen loadingScreen,
            IMapRenderer mapRenderer,
            IGlobalRealmController realmController,
            ITeleportController teleportController,
            IRoomHub roomHub,
            IRemoteEntities remoteEntities,
            IDecentralandUrlsSource decentralandUrlsSource,
            World globalWorld,
            RoadPlugin roadsPlugin,
            TerrainGenerator genesisTerrain,
            WorldTerrainGenerator worldsTerrain,
            SatelliteFloor satelliteFloor,
            bool landscapeEnabled,
            ObjectProxy<Entity> cameraEntity,
            CameraSamplingData cameraSamplingData,
            bool isLocalSceneDevelopment,
            ILoadingStatus loadingStatus,
            ICacheCleaner cacheCleaner,
            IMemoryUsageProvider memoryUsageProvider,
            FeatureFlagsCache featureFlagsCache)
        {
            this.loadingScreen = loadingScreen;
            this.mapRenderer = mapRenderer;
            this.realmController = realmController;
            this.teleportController = teleportController;
            this.roadsPlugin = roadsPlugin;
            this.genesisTerrain = genesisTerrain;
            this.worldsTerrain = worldsTerrain;
            this.satelliteFloor = satelliteFloor;
            this.landscapeEnabled = landscapeEnabled;
            this.cameraEntity = cameraEntity;
            this.cameraSamplingData = cameraSamplingData;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.isLocalSceneDevelopment = isLocalSceneDevelopment;
            this.globalWorld = globalWorld;
            this.loadingStatus = loadingStatus;
            this.featureFlagsCache = featureFlagsCache;
            var livekitTimeout = TimeSpan.FromSeconds(10f);

            realmChangeOperations = new ITeleportOperation[]
            {
                new RestartLoadingStatus(),
                new RemoveRemoteEntitiesTeleportOperation(remoteEntities, globalWorld),
                new StopRoomAsyncTeleportOperation(roomHub, livekitTimeout),
                new RemoveCameraSamplingDataTeleportOperation(globalWorld, cameraEntity),
                new DestroyAllRoadAssetsTeleportOperation(globalWorld, roadsPlugin),
                new ChangeRealmTeleportOperation(this),
                new LoadLandscapeTeleportOperation(this),
                new PrewarmRoadAssetPoolsTeleportOperation(realmController, roadsPlugin),
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
            if (realm == currentRealm || realm == realmController.RealmData.Ipfs.CatalystBaseUrl)
                return false;

            return true;
        }

        public async UniTask<bool> CheckRealmIsReacheableAsync(URLDomain realm, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (!await realmController.IsReachableAsync(realm, ct))
                return false;

            return true;
        }

        public async UniTask<Result> TryChangeRealmAsync(URLDomain realm, CancellationToken ct,
            Vector2Int parcelToTeleport = default)
        {
            ct.ThrowIfCancellationRequested();

            currentRealm = realmController.RealmData.Ipfs.CatalystBaseUrl;
            var loadResult
                = await loadingScreen.ShowWhileExecuteTaskAsync(DoChangeRealmAsync(realm, parcelToTeleport, ct), ct);

            if (!loadResult.Success)
            {
                if (!globalWorld.Has<CameraSamplingData>(cameraEntity.Object))
                    globalWorld.Add(cameraEntity.Object, cameraSamplingData);

                ReportHub.LogError(ReportCategory.REALM,
                    $"Error trying to teleport to a realm {realm}: {loadResult.ErrorMessage}");
            }

            return loadResult;
        }

        private Func<AsyncLoadProcessReport, UniTask<Result>> DoChangeRealmAsync(URLDomain realm,
            Vector2Int parcelToTeleport,
            CancellationToken ct)
        {
            return async parentLoadReport =>
            {
                ct.ThrowIfCancellationRequested();

                var teleportParams = new TeleportParams
                {
                    CurrentDestinationParcel = parcelToTeleport,
                    CurrentDestinationRealm = realm, ParentReport = parentLoadReport, LoadingStatus = loadingStatus
                };

                for (int attempt = 0; attempt < MAX_REALM_CHANGE_RETRIES; attempt++)
                {
                    bool success = true;
                    foreach (var realmChangeOperation in realmChangeOperations)
                    {
                        try
                        {
                            var currentOperationResult = await realmChangeOperation.ExecuteAsync(teleportParams, ct);
                            if (!currentOperationResult.Success)
                            {
                                success = false;
                                ReportHub.LogError(ReportCategory.REALM, $"Operation failed on realm change attempt {attempt + 1}: {currentOperationResult.ErrorMessage}");
                                break;
                            }
                        }
                        catch (Exception e)
                        {
                            success = false;
                            ReportHub.LogError(ReportCategory.REALM, $"Unhandled exception on realm change attempt {attempt + 1}: {e}");
                            break;
                        }
                    }

                    if (success)
                    {
                        return Result.SuccessResult();
                    }
                }

                // All retries failed, try with the previous realm and parcel
                ReportHub.LogWarning(ReportCategory.REALM, "All attempts failed. Trying with previous realm and parcel.");
                teleportParams.CurrentDestinationRealm = currentRealm;
                teleportParams.CurrentDestinationParcel = currentParcel;

                foreach (ITeleportOperation realmChangeOperation in realmChangeOperations)
                {
                    try
                    {
                        Result currentOperationResult = await realmChangeOperation.ExecuteAsync(teleportParams, ct);

                        if (!currentOperationResult.Success)
                        {
                            parentLoadReport.SetProgress(1);
                            return currentOperationResult;
                        }
                    }
                    catch (Exception e)
                    {
                        parentLoadReport.SetProgress(1);
                        return Result.ErrorResult($"Unhandled exception while changing realm {e}");
                    }
                }

                return Result.ErrorResult("Change realm failed, returned to previous realm");
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
                    featureFlagsCache.Configuration.TryGetTextPayload(FeatureFlagsStrings.GENESIS_STARTING_PARCEL, FeatureFlagsStrings.STRING_VARIANT, out string parcelCoords))
                {
                    //RealmHelper.TryParseParcelFromString(parcelCoords, out parcelToTeleport);
                }
                waitForSceneReadiness = await TeleportToParcelAsync(parcelToTeleport, teleportLoadReport, ct);
            }
            // add camera sampling data to the camera entity to start partitioning
            Assert.IsTrue(cameraEntity.Configured);
            globalWorld.Add(cameraEntity.Object, cameraSamplingData);
            await waitForSceneReadiness;
        }

        private Result IsParcelInsideTerrain(Vector2Int parcel, bool isLocal, bool isGenesis)
        {
            IContainParcel terrain = isLocal && !isGenesis ? worldsTerrain : genesisTerrain;

            return !terrain.Contains(parcel)
                ? Result.ErrorResult($"Parcel {parcel} is outside of the bounds.")
                : Result.SuccessResult();
        }

        public async UniTask<Result> TryInitializeTeleportToParcelAsync(Vector2Int parcel, CancellationToken ct,
            bool isLocal = false, bool forceChangeRealm = false)
        {
            ct.ThrowIfCancellationRequested();

            Result parcelCheckResult = IsParcelInsideTerrain(parcel, isLocal, IsGenesisRealm());
            if (!parcelCheckResult.Success)
                return parcelCheckResult;

            if (forceChangeRealm || (!isLocal && !IsGenesisRealm()))
            {
                var url = URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.Genesis));
                return await TryChangeRealmAsync(url, ct, parcel);
            }

            Result loadResult = await loadingScreen.ShowWhileExecuteTaskAsync(TryTeleportAsync(parcel, ct), ct);

            if (!loadResult.Success)
            {
                ReportHub.LogError(ReportCategory.SCENE_LOADING,
                    $"Error trying to teleport to a parcel {parcel}: {loadResult.ErrorMessage}");
            }

            return loadResult;
        }

        private Func<AsyncLoadProcessReport, UniTask<Result>> TryTeleportAsync(Vector2Int parcel, CancellationToken ct)
        {
            return async parentLoadReport =>
            {
                ct.ThrowIfCancellationRequested();
                var teleportParams = new TeleportParams
                {
                    ParentReport = parentLoadReport, CurrentDestinationParcel = parcel, LoadingStatus = loadingStatus
                };
                foreach (var realmChangeOperation in teleportInSameRealmOperation)
                {
                    try
                    {
                        var currentOperationResult = await realmChangeOperation.ExecuteAsync(teleportParams, ct);
                        if (!currentOperationResult.Success)
                        {
                            parentLoadReport.SetProgress(1);
                            return currentOperationResult;
                        }
                    }
                    catch (Exception e)
                    {
                        parentLoadReport.SetProgress(1);
                        return Result.ErrorResult($"Unhandled exception while teleporting in same realm {e}");
                    }
                }
                return Result.SuccessResult();
            };
        }

        public async UniTask LoadTerrainAsync(AsyncLoadProcessReport landscapeLoadReport, CancellationToken ct)
        {
            if (landscapeEnabled)
            {
                if (IsGenesisRealm())
                {
                    //TODO (Juani): The globalWorld terrain would be hidden. We need to implement the re-usage when going back
                    worldsTerrain.SwitchVisibility(false);

                    if (!genesisTerrain.IsTerrainGenerated)
                        await genesisTerrain.GenerateTerrainAndShowAsync(processReport: landscapeLoadReport,
                            cancellationToken: ct);
                    else
                        await genesisTerrain.ShowAsync(landscapeLoadReport);
                }
                else
                {
                    genesisTerrain.Hide();

                    if (isLocalSceneDevelopment)
                        await GenerateStaticScenesTerrainAsync(landscapeLoadReport, ct);
                    else // World Fixed Scenes
                        await GenerateFixedScenesTerrainAsync(landscapeLoadReport, ct);
                }
            }
        }

        private async UniTask GenerateStaticScenesTerrainAsync(AsyncLoadProcessReport landscapeLoadReport, CancellationToken ct)
        {
            if (!worldsTerrain.IsInitialized)
                return;

            var staticScenesEntityDefinitions = await realmController.WaitForStaticScenesEntityDefinitionsAsync(ct);
            if (!staticScenesEntityDefinitions.HasValue) return;

            int parcelsAmount = staticScenesEntityDefinitions.Value.Value.Count;
            using (var parcels = new NativeParallelHashSet<int2>(parcelsAmount, AllocatorManager.Persistent))
            {
                foreach (var staticScene in staticScenesEntityDefinitions.Value.Value)
                {
                    foreach (Vector2Int parcel in staticScene.metadata.scene.DecodedParcels)
                    {
                        parcels.Add(parcel.ToInt2());
                    }
                }

                await worldsTerrain.GenerateTerrainAsync(parcels, (uint)realmController.RealmData.GetHashCode(), landscapeLoadReport, cancellationToken: ct);
            }
        }

        private async UniTask GenerateFixedScenesTerrainAsync(AsyncLoadProcessReport landscapeLoadReport, CancellationToken ct)
        {
            if (!worldsTerrain.IsInitialized)
                return;

            AssetPromise<SceneEntityDefinition, GetSceneDefinition>[]? promises = await realmController.WaitForFixedScenePromisesAsync(ct);

            var parcelsAmount = 0;
            foreach (AssetPromise<SceneEntityDefinition, GetSceneDefinition> promise in promises)
                parcelsAmount += promise.Result!.Value.Asset!.metadata.scene.DecodedParcels.Count;

            using (var parcels = new NativeParallelHashSet<int2>(parcelsAmount, AllocatorManager.Persistent))
            {
                foreach (AssetPromise<SceneEntityDefinition, GetSceneDefinition> promise in promises)
                {
                    foreach (Vector2Int parcel in promise.Result!.Value.Asset!.metadata.scene.DecodedParcels)
                        parcels.Add(parcel.ToInt2());
                }

                await worldsTerrain.GenerateTerrainAsync(parcels, (uint)realmController.RealmData.GetHashCode(), landscapeLoadReport, cancellationToken: ct);
            }
        }

        public void SwitchMiscVisibilityAsync()
        {
            bool isGenesis = IsGenesisRealm();

            RealmChanged?.Invoke(isGenesis);
            mapRenderer.SetSharedLayer(MapLayer.PlayerMarker, isGenesis);
            satelliteFloor.SetCurrentlyInGenesis(isGenesis);
            roadsPlugin.RoadAssetPool?.SwitchVisibility(isGenesis);
        }

        public async UniTask<UniTask> TeleportToParcelAsync(Vector2Int parcel, AsyncLoadProcessReport processReport,
            CancellationToken ct)
        {
            WaitForSceneReadiness? waitForSceneReadiness =
                await teleportController.TeleportToSceneSpawnPointAsync(parcel, processReport, ct);

            return waitForSceneReadiness.ToUniTask(ct);
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

            return waitForSceneReadiness.ToUniTask(ct);
        }

        public async UniTask ChangeRealmAsync(URLDomain realm, CancellationToken ct)
        {
            await realmController.SetRealmAsync(realm, ct);
            currentRealm = realm;
            SwitchMiscVisibilityAsync();
        }

        private bool IsGenesisRealm() =>
            !isLocalSceneDevelopment && !realmController.RealmData.ScenesAreFixed;
    }
}
