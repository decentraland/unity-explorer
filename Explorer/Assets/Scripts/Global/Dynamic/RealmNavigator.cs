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
using DCL.Ipfs;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using Global.Dynamic.TeleportOperations;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using Utility;
using Utility.Types;
using static DCL.UserInAppInitializationFlow.RealFlowLoadingStatus.Stage;

namespace Global.Dynamic
{
    public class RealmNavigator : IRealmNavigator
    {
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

        private readonly ITeleportOperation[] realmChangeOperations;
        private readonly ITeleportOperation teleportInSameRealmOperation;

        private URLDomain? CurrentRealm;

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
            CameraSamplingData cameraSamplingData)
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
            this.globalWorld = globalWorld;

            var livekitTimeout = TimeSpan.FromSeconds(10f);

            realmChangeOperations = new ITeleportOperation[]
            {
                new RemoveRemoteEntitiesTeleportOperation(remoteEntities, globalWorld),
                new StopRoomAsyncTeleportOperation(roomHub, livekitTimeout),
                new RemoveCameraSamplingDataTeleportOperation(globalWorld, cameraEntity),
                new DestroyAllRoadAssetsTeleportOperation(globalWorld, roadsPlugin),
                new ChangeRealmTeleportOperation(this),
                new LoadLandscapeTeleportOperation(this),
                new PrewarmRoadAssetPoolsTeleportOperation(realmController, roadsPlugin),
                new MoveToParcelInNewRealmTeleportOperation(this),
                new RestartRoomAsyncTeleportOperation(roomHub, livekitTimeout),
            };

            teleportInSameRealmOperation = new MoveToParcelInSameRealmTeleportOperation(this);
        }

        public bool CheckIsNewRealm(URLDomain realm)
        {
            if (realm == CurrentRealm || realm == realmController.RealmData.Ipfs.CatalystBaseUrl)
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

            Result loadResult
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
                    CurrentDestinationRealm = realm,
                    ParentReport = parentLoadReport,
                };

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

                return Result.SuccessResult();
            };
        }

        public async UniTask InitializeTeleportToSpawnPointAsync(AsyncLoadProcessReport teleportLoadReport,
            CancellationToken ct, Vector2Int parcelToTeleport)
        {
            bool isGenesis = !realmController.RealmData.ScenesAreFixed;
            UniTask waitForSceneReadiness;

            if (isGenesis)
                waitForSceneReadiness = await TeleportToParcelAsync(parcelToTeleport, teleportLoadReport, ct);
            else
                waitForSceneReadiness = await TeleportToWorldSpawnPointAsync(parcelToTeleport, teleportLoadReport, ct);

            // add camera sampling data to the camera entity to start partitioning
            Assert.IsTrue(cameraEntity.Configured);
            globalWorld.Add(cameraEntity.Object, cameraSamplingData);
            await waitForSceneReadiness;
        }

        public async UniTask<Result> TryInitializeTeleportToParcelAsync(Vector2Int parcel, CancellationToken ct,
            bool isLocal = false)
        {
            ct.ThrowIfCancellationRequested();

            bool isGenesis = !realmController.RealmData.ScenesAreFixed;

            if (!isLocal && !isGenesis)
            {
                if(genesisTerrain.IsTerrainGenerated && !genesisTerrain.Contains(parcel))
                    return Result.ErrorResult($"Parcel {parcel} is outside of the bounds.");

                var url = URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.Genesis));
                return await TryChangeRealmAsync(url, ct, parcel);
            }

            {
                if (isLocal)
                    switch (isGenesis)
                    {
                        case false when worldsTerrain.IsInitialized && !worldsTerrain.Contains(parcel):
                            return Result.ErrorResult($"Parcel {parcel} is outside of the bounds.");
                        case true when genesisTerrain.IsTerrainGenerated && !genesisTerrain.Contains(parcel):
                            return Result.ErrorResult($"Parcel {parcel} is outside of the bounds.");
                    }
                else
                {
                    if (genesisTerrain.IsTerrainGenerated && !genesisTerrain.Contains(parcel))
                        return Result.ErrorResult($"Parcel {parcel} is outside of the bounds.");
                }
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
                parentLoadReport.SetProgress(RealFlowLoadingStatus.PROGRESS[LandscapeLoaded]);

                var teleportParams = new TeleportParams
                {
                    ParentReport = parentLoadReport,
                    CurrentDestinationParcel = parcel,
                };

                try
                {
                    Result currentOperationResult = await teleportInSameRealmOperation.ExecuteAsync(teleportParams, ct);

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

                return Result.SuccessResult();
            };
        }

        public async UniTask LoadTerrainAsync(AsyncLoadProcessReport landscapeLoadReport, CancellationToken ct)
        {
            if (landscapeEnabled)
            {
                bool isGenesis = !realmController.RealmData.ScenesAreFixed;

                if (isGenesis)
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

                    await GenerateWorldTerrainAsync((uint)realmController.RealmData.GetHashCode(), landscapeLoadReport,
                        ct);
                }
            }
        }

        public async UniTask SwitchMiscVisibilityAsync()
        {
            bool isGenesis = !realmController.RealmData.ScenesAreFixed;

            RealmChanged?.Invoke(isGenesis);
            mapRenderer.SetSharedLayer(MapLayer.PlayerMarker, isGenesis);
            await satelliteFloor.SwitchVisibilityAsync(isGenesis);
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
            CurrentRealm = realm;
            await SwitchMiscVisibilityAsync();
        }

        private async UniTask GenerateWorldTerrainAsync(uint worldSeed, AsyncLoadProcessReport processReport,
            CancellationToken ct)
        {
            if (!worldsTerrain.IsInitialized)
                return;

            AssetPromise<SceneEntityDefinition, GetSceneDefinition>[]? promises = await realmController.WaitForFixedScenePromisesAsync(ct);

            var decodedParcelsAmount = 0;

            foreach (AssetPromise<SceneEntityDefinition, GetSceneDefinition> promise in promises)
                decodedParcelsAmount += promise.Result!.Value.Asset!.metadata.scene.DecodedParcels.Count;

            using (var ownedParcels =
                   new NativeParallelHashSet<int2>(decodedParcelsAmount, AllocatorManager.Persistent))
            {
                foreach (AssetPromise<SceneEntityDefinition, GetSceneDefinition> promise in promises)
                {
                    foreach (Vector2Int parcel in promise.Result!.Value.Asset!.metadata.scene.DecodedParcels)
                        ownedParcels.Add(parcel.ToInt2());
                }

                await worldsTerrain.GenerateTerrainAsync(ownedParcels, worldSeed, processReport, cancellationToken: ct);
            }
        }
    }
}
