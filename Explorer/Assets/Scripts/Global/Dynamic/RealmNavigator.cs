using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Ipfs;
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
using DCL.Utilities.Extensions;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Realm;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using System;
using System.Linq;
using System.Threading;
using DCL.Diagnostics;
using DCL.Roads.Components;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using Utility;
using static DCL.UserInAppInitializationFlow.RealFlowLoadingStatus.Stage;

namespace Global.Dynamic
{
    public class RealmNavigator : IRealmNavigator
    {
        private readonly ILoadingScreen loadingScreen;
        private readonly IMapRenderer mapRenderer;
        private readonly IGlobalRealmController realmController;
        private readonly ITeleportController teleportController;
        private readonly IRoomHub roomHub;
        private readonly IRemoteEntities remoteEntities;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly World globalWorld;
        private readonly RoadPlugin roadsPlugin;
        private readonly TerrainGenerator genesisTerrain;
        private readonly WorldTerrainGenerator worldsTerrain;
        private readonly SatelliteFloor satelliteFloor;
        private readonly bool landscapeEnabled;

        private readonly ObjectProxy<Entity> cameraEntity;
        private readonly CameraSamplingData cameraSamplingData;

        public URLDomain? CurrentRealm { get; private set; }

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
            this.roomHub = roomHub;
            this.remoteEntities = remoteEntities;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.globalWorld = globalWorld;
        }

        public async UniTask<bool> TryChangeRealmAsync(URLDomain realm, CancellationToken ct,
            Vector2Int parcelToTeleport = default)
        {
            if (realm == CurrentRealm || realm == realmController.RealmData.Ipfs.CatalystBaseUrl)
                return false;

            ct.ThrowIfCancellationRequested();

            if (!await realmController.IsReachableAsync(realm, ct))
                return false;

            ct.ThrowIfCancellationRequested();

            var loadResult = await loadingScreen.ShowWhileExecuteTaskAsync(async parentLoadReport =>
                {
                    ct.ThrowIfCancellationRequested();

                    remoteEntities.ForceRemoveAll(globalWorld);
                    await roomHub.StopIfNotAsync();

                    // By removing the CameraSamplingData, we stop the ring calculation
                    globalWorld.Remove<CameraSamplingData>(cameraEntity.Object);

                    // Releases all the road infos, which returns all road assets to the pool and then destroys all the road assets
                    globalWorld.Query(new QueryDescription().WithAll<RoadInfo>(), (entity) => globalWorld.Get<RoadInfo>(entity).Dispose(roadsPlugin.RoadAssetPool));
                    roadsPlugin.RoadAssetPool.Unload();

                    await ChangeRealmAsync(realm, ct);
                    parentLoadReport.SetProgress(RealFlowLoadingStatus.PROGRESS[ProfileLoaded]);

                    var landscapeLoadReport
                        = parentLoadReport.CreateChildReport(RealFlowLoadingStatus.PROGRESS[LandscapeLoaded]);

                    await LoadTerrainAsync(landscapeLoadReport, ct);
                    parentLoadReport.SetProgress(RealFlowLoadingStatus.PROGRESS[LandscapeLoaded]);

                    var teleportLoadReport
                        = parentLoadReport.CreateChildReport(RealFlowLoadingStatus.PROGRESS[PlayerTeleported]);

                    // When Genesis is loaded, road asset pools must pre-allocate some instances to reduce allocations while playing
                    if(!realmController.RealmData.ScenesAreFixed) // Is Genesis
                        roadsPlugin.RoadAssetPool.Prewarm();

                    await InitializeTeleportToSpawnPointAsync(teleportLoadReport, ct, parcelToTeleport);
                    parentLoadReport.SetProgress(RealFlowLoadingStatus.PROGRESS[PlayerTeleported]);

                    await roomHub.StartAsync();
                    parentLoadReport.SetProgress(RealFlowLoadingStatus.PROGRESS[Completed]);
                },
                ct
            );
            if (!loadResult.Success)
            {
                if (!globalWorld.Has<CameraSamplingData>(cameraEntity.Object))
                    globalWorld.Add(cameraEntity.Object, cameraSamplingData);

                ReportHub.LogError(ReportCategory.REALM,
                    $"Error trying to teleport to a realm {realm}: {loadResult.ErrorMessage}");

                return false;
            }


            return true;
        }

        public async UniTask InitializeTeleportToSpawnPointAsync(AsyncLoadProcessReport teleportLoadReport,
            CancellationToken ct, Vector2Int parcelToTeleport)
        {
            var isGenesis = !realmController.RealmData.ScenesAreFixed;
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

        public async UniTask<bool> TryInitializeTeleportToParcelAsync(Vector2Int parcel, CancellationToken ct,
            bool isLocal = false)
        {
            ct.ThrowIfCancellationRequested();

            var isGenesis = !realmController.RealmData.ScenesAreFixed;

                if (!isLocal && !isGenesis)
                {
                    var url = URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.Genesis));
                    await TryChangeRealmAsync(url, ct, parcel);
                }
                else
                {
                    var loadResult = await loadingScreen.ShowWhileExecuteTaskAsync(async parentLoadReport =>
                    {
                        ct.ThrowIfCancellationRequested();
                        parentLoadReport.SetProgress(RealFlowLoadingStatus.PROGRESS[LandscapeLoaded]);

                        var teleportLoadReport
                            = parentLoadReport.CreateChildReport(RealFlowLoadingStatus.PROGRESS[PlayerTeleported]);

                        var waitForSceneReadiness = await TeleportToParcelAsync(parcel, teleportLoadReport, ct);
                        await waitForSceneReadiness;

                        parentLoadReport.SetProgress(RealFlowLoadingStatus.PROGRESS[Completed]);
                    }, ct);
                    if (!loadResult.Success)
                    {
                        ReportHub.LogError(ReportCategory.SCENE_LOADING,
                            $"Error trying to teleport to a parcel {parcel}: {loadResult.ErrorMessage}");
                        return false;
                    }
                }

                return true;
        }

        public async UniTask LoadTerrainAsync(AsyncLoadProcessReport landscapeLoadReport, CancellationToken ct)
        {
            if (landscapeEnabled)
            {
                var isGenesis = !realmController.RealmData.ScenesAreFixed;

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
            var isGenesis = !realmController.RealmData.ScenesAreFixed;

            RealmChanged?.Invoke(isGenesis);
            mapRenderer.SetSharedLayer(MapLayer.PlayerMarker, isGenesis);
            await satelliteFloor.SwitchVisibilityAsync(isGenesis);
            roadsPlugin.RoadAssetPool?.SwitchVisibility(isGenesis);
        }

        private async UniTask<UniTask> TeleportToParcelAsync(Vector2Int parcel, AsyncLoadProcessReport processReport,
            CancellationToken ct)
        {
            var waitForSceneReadiness =
                await teleportController.TeleportToSceneSpawnPointAsync(parcel, processReport, ct);
            return waitForSceneReadiness.ToUniTask(ct);
        }

        private async UniTask<UniTask> TeleportToWorldSpawnPointAsync(Vector2Int parcelToTeleport,
            AsyncLoadProcessReport processReport, CancellationToken ct)
        {
            var promises = await realmController.WaitForFixedScenePromisesAsync(ct);

            if (!promises.Any(p =>
                    p.Result.HasValue
                    && (p.Result.Value.Asset?.metadata.scene.DecodedParcels.Contains(parcelToTeleport) ?? false)
                ))
                parcelToTeleport = promises[0].Result!.Value.Asset!.metadata.scene.DecodedBase;

            var waitForSceneReadiness =
                await teleportController.TeleportToSceneSpawnPointAsync(parcelToTeleport, processReport, ct);
            return waitForSceneReadiness.ToUniTask(ct);
        }

        private async UniTask ChangeRealmAsync(URLDomain realm, CancellationToken ct)
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

            var promises = await realmController.WaitForFixedScenePromisesAsync(ct);

            var decodedParcelsAmount = 0;

            foreach (var promise in promises)
                decodedParcelsAmount += promise.Result!.Value.Asset!.metadata.scene.DecodedParcels.Count;

            using (var ownedParcels =
                   new NativeParallelHashSet<int2>(decodedParcelsAmount, AllocatorManager.Persistent))
            {
                foreach (var promise in promises)
                {
                    foreach (var parcel in promise.Result!.Value.Asset!.metadata.scene.DecodedParcels)
                        ownedParcels.Add(parcel.ToInt2());
                }

                await worldsTerrain.GenerateTerrainAsync(ownedParcels, worldSeed, processReport, cancellationToken: ct);
            }
        }
    }
}
