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
using DCL.PluginSystem.Global;
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
using System.Collections.Generic;
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
            var world = globalWorldProxy.Object.EnsureNotNull();

            ct.ThrowIfCancellationRequested();

            if (!await realmController.IsReachableAsync(realm, ct))
                return false;

            ct.ThrowIfCancellationRequested();

            await SwitchMiscVisibility(realm == genesisDomain);

            await loadingScreen.ShowWhileExecuteTaskAsync(async loadReport =>
                {
                    remoteEntities.ForceRemoveAll(world);
                    await roomHub.StopAsync();
                    loadReport.ProgressCounter.Value = 0.1f;
                    var sceneLoadReport = new AsyncLoadProcessReport(new UniTaskCompletionSource(), new AsyncReactiveProperty<float>(0));

                    await realmController.SetRealmAsync(realm, Vector2Int.zero, sceneLoadReport, ct);

                    if (realm != genesisDomain)
                    {
                        var terrainLoadReport = new AsyncLoadProcessReport(new UniTaskCompletionSource(), new AsyncReactiveProperty<float>(0));

                        await UniTask.WhenAll(terrainLoadReport.PropagateAsync(loadReport, ct, loadReport.ProgressCounter.Value, timeout: TimeSpan.FromSeconds(30)),
                            GenerateWorldTerrainAsync((uint)realm.GetHashCode(), terrainLoadReport, ct));
                    }

                    await roomHub.StartAsync();
                },
                ct
            );

            return true;
        }

        public async UniTask TeleportToParcelAsync(Vector2Int parcel, CancellationToken ct, bool isLocal = false)
        {
            ct.ThrowIfCancellationRequested();

            await loadingScreen.ShowWhileExecuteTaskAsync(async loadReport =>
            {
                if (!isLocal && realmController.GetRealm().Ipfs.CatalystBaseUrl != genesisDomain)
                {
                    await realmController.SetRealmAsync(genesisDomain, Vector2Int.zero, loadReport, ct);
                    await SwitchMiscVisibility(true);

                    ct.ThrowIfCancellationRequested();
                }

                WaitForSceneReadiness? waitForSceneReadiness = await teleportController.TeleportToSceneSpawnPointAsync(parcel, loadReport, ct);
                await waitForSceneReadiness.ToUniTask(ct);
            }, ct);
        }

        private async UniTask GenerateWorldTerrainAsync(uint worldSeed, AsyncLoadProcessReport processReport, CancellationToken ct)
        {
            if (!worldsTerrain.IsInitialized) return;

            await UniTask.WaitUntil(() => realmController.GlobalWorld.EcsWorld.Get<FixedScenePointers>(realmController.RealmEntity).AllPromisesResolved, cancellationToken: ct);

            AssetPromise<SceneEntityDefinition,GetSceneDefinition>[] promises = realmController.GlobalWorld.EcsWorld.Get<FixedScenePointers>(realmController.RealmEntity).Promises;

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

        private async UniTask SwitchMiscVisibility(bool isVisible)
        {
            // is NOT visible
            worldsTerrain.SwitchVisibility(!isVisible);

            // isVisible
            mapRenderer.SetSharedLayer(MapLayer.PlayerMarker, isVisible);
            satelliteFloor.SwitchVisibility(isVisible);
            roadsPlugin.RoadAssetPool?.SwitchVisibility(isVisible);
            await genesisTerrain.SwitchVisibility(isVisible);
        }
    }
}
