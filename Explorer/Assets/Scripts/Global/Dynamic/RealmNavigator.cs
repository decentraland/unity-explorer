using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Ipfs;
using DCL.Landscape;
using DCL.MapRenderer;
using DCL.MapRenderer.MapLayers;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Entities;
using DCL.ParcelsService;
using DCL.Roads.Systems;
using ECS.SceneLifeCycle.Components;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using ECS.SceneLifeCycle.Realm;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using System;
using System.Threading;
using DCL.UserInAppInitializationFlow;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Utility;
using static DCL.UserInAppInitializationFlow.RealFlowLoadingStatus.Stage;


namespace Global.Dynamic
{
    public class RealmNavigator : IRealmNavigator
    {
        private readonly URLDomain genesisDomain = URLDomain.FromString(IRealmNavigator.GENESIS_URL);

        private readonly ILoadingScreen loadingScreen;
        private readonly IMapRenderer mapRenderer;
        private readonly IRealmController realmController;
        private readonly ITeleportController teleportController;
        private readonly IRoomHub roomHub;
        private readonly IRemoteEntities remoteEntities;
        private readonly ObjectProxy<World> globalWorldProxy;
        private readonly RoadPlugin roadsPlugin;
        private readonly TerrainGenerator genesisTerrain;
        private readonly WorldTerrainGenerator worldsTerrain;
        private readonly SatelliteFloor satelliteFloor;
        private readonly bool landscapeEnabled;

        public RealmNavigator(
            ILoadingScreen loadingScreen,
            IMapRenderer mapRenderer,
            IRealmController realmController,
            ITeleportController teleportController,
            IRoomHub roomHub,
            IRemoteEntities remoteEntities,
            ObjectProxy<World> globalWorldProxy,
            RoadPlugin roadsPlugin,
            TerrainGenerator genesisTerrain,
            WorldTerrainGenerator worldsTerrain,
            SatelliteFloor satelliteFloor,
            bool landscapeEnabled)
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
            this.roomHub = roomHub;
            this.remoteEntities = remoteEntities;
            this.globalWorldProxy = globalWorldProxy;
        }

        public async UniTask<bool> TryChangeRealmAsync(URLDomain realm, CancellationToken ct, Vector2Int parcelToTeleport = default)
        {
            World world = globalWorldProxy.Object.EnsureNotNull();

            ct.ThrowIfCancellationRequested();

            if (!await realmController.IsReachableAsync(realm, ct))
                return false;

            ct.ThrowIfCancellationRequested();

            try
            {
                await loadingScreen.ShowWhileExecuteTaskAsync(async loadReport =>
                    {
                        remoteEntities.ForceRemoveAll(world);
                        await roomHub.StopIfNotAsync();
                        await ChangeRealm(realm, loadReport, ct);
                        loadReport.ProgressCounter.Value = RealFlowLoadingStatus.PROGRESS[ProfileLoaded];
                        await LoadTerrainAsync(loadReport, ct);
                        loadReport.ProgressCounter.Value = RealFlowLoadingStatus.PROGRESS[LandscapeLoaded];
                        var waitForSceneReadiness = await InitializeTeleportToSpawnPointAsync(loadReport, ct, parcelToTeleport);
                        await waitForSceneReadiness;
                        loadReport.ProgressCounter.Value = RealFlowLoadingStatus.PROGRESS[PlayerTeleported];
                        ct.ThrowIfCancellationRequested();
                        await roomHub.StartAsync();
                        loadReport.ProgressCounter.Value = 1f;
                    },
                    ct
                );
            }
            catch (TimeoutException) { }

            return true;
        }

        public async UniTask<UniTask> InitializeTeleportToSpawnPointAsync(AsyncLoadProcessReport loadReport, CancellationToken ct, Vector2Int parcelToTeleport)
        {
            bool isGenesis = !realmController.GetRealm().ScenesAreFixed;
            if (isGenesis)
            {
                var waitForSceneReadiness = await TeleportToParcelAsync(parcelToTeleport, loadReport, ct);
                return waitForSceneReadiness;
            }
            else
            {
                var waitForSceneReadiness = await TeleportToWorldSpawnPoint(loadReport, ct);
                return waitForSceneReadiness;
            }
        }


        public async UniTask TryInitializeTeleportToParcelAsync(Vector2Int parcel, CancellationToken ct, bool isLocal = false)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                bool isGenesis = !realmController.GetRealm().ScenesAreFixed;
                if (!isLocal && !isGenesis)
                {
                    await TryChangeRealmAsync(genesisDomain, ct, parcel);
                }
                else
                {
                    await loadingScreen.ShowWhileExecuteTaskAsync(async loadReport =>
                    {
                        loadReport.ProgressCounter.Value = RealFlowLoadingStatus.PROGRESS[LandscapeLoaded];
                        var waitForSceneReadiness = await TeleportToParcelAsync(parcel, loadReport, ct);
                        await waitForSceneReadiness;
                        loadReport.ProgressCounter.Value = 1f;
                        ct.ThrowIfCancellationRequested();
                    }, ct);
                }
            }
            catch (TimeoutException) { }
        }
        
        public async UniTask LoadTerrainAsync(AsyncLoadProcessReport loadReport, CancellationToken ct)
        {
            if (landscapeEnabled)
            {
                bool isGenesis = !realmController.GetRealm().ScenesAreFixed;
                var postRealmLoadReport = AsyncLoadProcessReport.Create();
                await UniTask.WhenAll(postRealmLoadReport.PropagateAsync(loadReport, ct, loadReport.ProgressCounter.Value, timeout: TimeSpan.FromSeconds(30)),
                    isGenesis
                        ? genesisTerrain.IsTerrainGenerated ? genesisTerrain.ShowAsync(postRealmLoadReport) : genesisTerrain.GenerateTerrainAsync(cancellationToken: ct)
                        : GenerateWorldTerrainAsync((uint)realmController.GetRealm().GetHashCode(), postRealmLoadReport, ct));
            }
        }

        private async UniTask<UniTask> TeleportToParcelAsync(Vector2Int parcel, AsyncLoadProcessReport processReport, CancellationToken ct)
        {
            var waitForSceneReadiness = await teleportController.TeleportToSceneSpawnPointAsync(parcel, processReport, ct);
            return waitForSceneReadiness.ToUniTask(ct);
        }


        private async UniTask<UniTask> TeleportToWorldSpawnPoint(AsyncLoadProcessReport processReport, CancellationToken ct)
        {
            await UniTask.WaitUntil(() => realmController.GlobalWorld.EcsWorld.Get<FixedScenePointers>(realmController.RealmEntity).AllPromisesResolved, cancellationToken: ct);
            AssetPromise<SceneEntityDefinition, GetSceneDefinition>[] promises = realmController.GlobalWorld.EcsWorld.Get<FixedScenePointers>(realmController.RealmEntity).Promises;
            var waitForSceneReadiness = await teleportController.TeleportToSceneSpawnPointAsync(promises[0].Result!.Value.Asset!.metadata.scene.DecodedBase, processReport, ct);
            return waitForSceneReadiness.ToUniTask(ct);
        }

        private async UniTask ChangeRealm(URLDomain realm, AsyncLoadProcessReport loadReport, CancellationToken ct)
        {
            await realmController.SetRealmAsync(realm, ct);
            SwitchMiscVisibilityAsync();
        }
        
        private async UniTask GenerateWorldTerrainAsync(uint worldSeed, AsyncLoadProcessReport processReport, CancellationToken ct)
        {
            if (!worldsTerrain.IsInitialized) return;

            await UniTask.WaitUntil(() => realmController.GlobalWorld.EcsWorld.Has<FixedScenePointers>(realmController.RealmEntity), cancellationToken: ct);
            await UniTask.WaitUntil(() => realmController.GlobalWorld.EcsWorld.Get<FixedScenePointers>(realmController.RealmEntity).AllPromisesResolved, cancellationToken: ct);

            AssetPromise<SceneEntityDefinition, GetSceneDefinition>[] promises = realmController.GlobalWorld.EcsWorld.Get<FixedScenePointers>(realmController.RealmEntity).Promises;

            var decodedParcelsAmount = 0;

            foreach (AssetPromise<SceneEntityDefinition, GetSceneDefinition> promise in promises)
                decodedParcelsAmount += promise.Result!.Value.Asset!.metadata.scene.DecodedParcels.Count;

            using (var ownedParcels = new NativeParallelHashSet<int2>(decodedParcelsAmount, AllocatorManager.Persistent))
            {
                foreach (AssetPromise<SceneEntityDefinition, GetSceneDefinition> promise in promises)
                foreach (Vector2Int parcel in promise.Result!.Value.Asset!.metadata.scene.DecodedParcels)
                    ownedParcels.Add(parcel.ToInt2());

                await worldsTerrain.GenerateTerrainAsync(ownedParcels, worldSeed, processReport, cancellationToken: ct);
            }
        }

        public void SwitchMiscVisibilityAsync()
        {
            bool isGenesis = !realmController.GetRealm().ScenesAreFixed;

            //TODO(Juani): This two methods looks quite similar....
            //if (!isGenesis) genesisTerrain.Hide();
            // is NOT visible
            worldsTerrain.SwitchVisibility(!isGenesis);

            // isVisible
            mapRenderer.SetSharedLayer(MapLayer.PlayerMarker, isGenesis);
            satelliteFloor.SwitchVisibility(isGenesis);
            roadsPlugin.RoadAssetPool?.SwitchVisibility(isGenesis);
        }
    }
}
