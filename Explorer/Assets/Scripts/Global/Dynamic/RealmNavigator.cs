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
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Utility;

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
            SatelliteFloor satelliteFloor
        )
        {
            this.loadingScreen = loadingScreen;
            this.mapRenderer = mapRenderer;
            this.realmController = realmController;
            this.teleportController = teleportController;
            this.roadsPlugin = roadsPlugin;
            this.genesisTerrain = genesisTerrain;
            this.worldsTerrain = worldsTerrain;
            this.satelliteFloor = satelliteFloor;
            this.roomHub = roomHub;
            this.remoteEntities = remoteEntities;
            this.globalWorldProxy = globalWorldProxy;
        }

        public async UniTask<bool> TryChangeRealmAsync(URLDomain realm, CancellationToken ct)
        {
            World world = globalWorldProxy.Object.EnsureNotNull();

            ct.ThrowIfCancellationRequested();

            if (!await realmController.IsReachableAsync(realm, ct))
                return false;

            ct.ThrowIfCancellationRequested();

            bool isGenesis = realm == genesisDomain;
            if (!isGenesis) genesisTerrain.Hide();

            try
            {
                await loadingScreen.ShowWhileExecuteTaskAsync(async loadReport =>
                    {
                        remoteEntities.ForceRemoveAll(world);
                        await roomHub.StopAsync();
                        loadReport.ProgressCounter.Value = 0.3f;
                        await realmController.SetRealmAsync(realm, Vector2Int.zero, loadReport, ct);
                        await LoadTerrainAsync(loadReport, ct);
                        await teleportController.TeleportToSceneSpawnPointAsync(Vector2Int.zero, loadReport, ct);
                        loadReport.ProgressCounter.Value = 0.7f;

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

        public async UniTask InitializeTeleportToParcelAsync(Vector2Int parcel, CancellationToken ct, bool isLocal = false)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await loadingScreen.ShowWhileExecuteTaskAsync(async loadReport =>
                {
                    if (isLocal)
                        await teleportController.TeleportToSceneSpawnPointAsync(parcel, loadReport, ct);
                    else
                    {
                        await realmController.SetRealmAsync(genesisDomain, Vector2Int.zero, loadReport, ct);
                        await LoadTerrainAsync(loadReport, ct);
                        await teleportController.TeleportToSceneSpawnPointAsync(parcel, loadReport, ct);
                    }

                    ct.ThrowIfCancellationRequested();
                }, ct);
            }
            catch (TimeoutException) { }
        }

        public async UniTask LoadTerrainAsync(AsyncLoadProcessReport loadReport, CancellationToken ct)
        {
            bool isGenesis = realmController.GetRealm().Ipfs.CatalystBaseUrl == genesisDomain;

            SwitchMiscVisibilityAsync(isGenesis);
            var postRealmLoadReport = AsyncLoadProcessReport.Create();
            await UniTask.WhenAll(postRealmLoadReport.PropagateAsync(loadReport, ct, loadReport.ProgressCounter.Value, timeout: TimeSpan.FromSeconds(30)),
                isGenesis
                    ? genesisTerrain.IsTerrainGenerated ? genesisTerrain.ShowAsync(postRealmLoadReport) : genesisTerrain.GenerateTerrainAsync(cancellationToken: ct)
                    : GenerateWorldTerrainAsync((uint)realmController.GetRealm().GetHashCode(), postRealmLoadReport, ct));
        }

        public async UniTask TeleportToParcelAsync(Vector2Int initialParcel, AsyncLoadProcessReport processReport,  CancellationToken ct)
        {
            bool isGenesis = realmController.GetRealm().Ipfs.CatalystBaseUrl == genesisDomain;

            WaitForSceneReadiness? waitForSceneReadiness;

            if (isGenesis)
                waitForSceneReadiness = await teleportController.TeleportToSceneSpawnPointAsync(initialParcel, processReport, ct);
            else
            {
                await UniTask.WaitUntil(() => realmController.GlobalWorld.EcsWorld.Get<FixedScenePointers>(realmController.RealmEntity).AllPromisesResolved, cancellationToken: ct);
                AssetPromise<SceneEntityDefinition, GetSceneDefinition>[] promises = realmController.GlobalWorld.EcsWorld.Get<FixedScenePointers>(realmController.RealmEntity).Promises;
                waitForSceneReadiness = await teleportController.TeleportToSceneSpawnPointAsync(promises[0].Result!.Value.Asset!.metadata.scene.DecodedBase, processReport, ct);
            }

            await waitForSceneReadiness.ToUniTask(ct);
        }

        
        private async UniTask GenerateWorldTerrainAsync(uint worldSeed, AsyncLoadProcessReport processReport, CancellationToken ct)
        {
            if (!worldsTerrain.IsInitialized) return;


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

        private void SwitchMiscVisibilityAsync(bool isVisible)
        {
            // is NOT visible
            worldsTerrain.SwitchVisibility(!isVisible);

            // isVisible
            mapRenderer.SetSharedLayer(MapLayer.PlayerMarker, isVisible);
            satelliteFloor.SwitchVisibility(isVisible);
            roadsPlugin.RoadAssetPool?.SwitchVisibility(isVisible);
        }
    }
}
