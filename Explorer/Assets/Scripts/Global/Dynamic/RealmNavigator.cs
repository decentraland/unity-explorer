using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
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
        private readonly LandscapePlugin landscapePlugin;
        private readonly RoadPlugin roadsPlugin;
        private readonly WorldTerrainGenerator worldsTerrainGenerator;

        public RealmNavigator(
            ILoadingScreen loadingScreen,
            IMapRenderer mapRenderer,
            IRealmController realmController,
            ITeleportController teleportController,
            IRoomHub roomHub,
            IRemoteEntities remoteEntities,
            ObjectProxy<World> globalWorldProxy,
            LandscapePlugin landscapePlugin, RoadPlugin roadsPlugin
        )
        {
            this.loadingScreen = loadingScreen;
            this.mapRenderer = mapRenderer;
            this.realmController = realmController;
            this.teleportController = teleportController;
            this.landscapePlugin = landscapePlugin;
            this.roadsPlugin = roadsPlugin;
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

            SwitchMiscVisibility(realm == genesisDomain);

            await loadingScreen.ShowWhileExecuteTaskAsync(async loadReport =>
                {
                    remoteEntities.ForceRemoveAll(world);
                    await roomHub.StopAsync();
                    await realmController.SetRealmAsync(realm, Vector2Int.zero, loadReport, ct);

                    if (realm != genesisDomain)
                        await GenerateWorldTerrainAsync((uint)realm.GetHashCode(),ct);

                    await roomHub.StartAsync();
                },
                ct
            );

            return true;
        }

        public async UniTask TeleportToParcelAsync(Vector2Int parcel, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            await loadingScreen.ShowWhileExecuteTaskAsync(async loadReport =>
            {
                if (realmController.GetRealm().Ipfs.CatalystBaseUrl != genesisDomain)
                {
                    await realmController.SetRealmAsync(genesisDomain, Vector2Int.zero, loadReport, ct);
                    SwitchMiscVisibility(true);

                    ct.ThrowIfCancellationRequested();
                }

                WaitForSceneReadiness? waitForSceneReadiness = await teleportController.TeleportToSceneSpawnPointAsync(parcel, loadReport, ct);
                await waitForSceneReadiness.ToUniTask(ct);
            }, ct);
        }

        private async UniTask GenerateWorldTerrainAsync(uint worldSeed, CancellationToken ct)
        {
            FixedScenePointers scenePointers = realmController.GlobalWorld.EcsWorld.Get<FixedScenePointers>(realmController.RealmEntity);
            await UniTask.WaitUntil(() => scenePointers.AllPromisesResolved, cancellationToken: ct);

            var decodedParcels = new List<int2>();

            foreach (AssetPromise<SceneEntityDefinition, GetSceneDefinition> promise in scenePointers.Promises)
            foreach (Vector2Int parcel in promise.Result.Value.Asset.metadata.scene.DecodedParcels)
                decodedParcels.Add(parcel.ToInt2());

            var ownedParcels = new NativeParallelHashSet<int2>(decodedParcels.Count, AllocatorManager.Persistent);

            foreach (int2 parcel in decodedParcels)
                ownedParcels.Add(parcel);

            await landscapePlugin.WorldTerrainGenerator.GenerateTerrainAsync(ownedParcels, worldSeed, cancellationToken: ct);
            ownedParcels.Dispose();
        }

        private void SwitchMiscVisibility(bool isVisible)
        {
            // isVisible
            mapRenderer.SetSharedLayer(MapLayer.PlayerMarker, isVisible);
            landscapePlugin.TerrainGenerator.SwitchVisibility(isVisible);
            landscapePlugin.landscapeData.Value.showSatelliteView = isVisible;
            roadsPlugin.RoadAssetPool.SwitchVisibility(isVisible);

            // is NOT visible
            landscapePlugin.WorldTerrainGenerator.SwitchVisibility(!isVisible);
        }

        // private async UniTask ShowLoadingScreenAndExecuteTaskAsync(CancellationToken ct, params Func<AsyncLoadProcessReport, UniTask>[] operations)
        // {
        //     ct.ThrowIfCancellationRequested();
        //
        //     var timeout = TimeSpan.FromSeconds(30);
        //     var loadReport = AsyncLoadProcessReport.Create();
        //
        //     UniTask showLoadingScreenTask = mvcManager.ShowAsync(
        //                                                    SceneLoadingScreenController.IssueCommand(
        //                                                        new SceneLoadingScreenController.Params(loadReport, timeout)), ct)
        //                                               .AttachExternalCancellation(ct);
        //
        //     var operationTasks = new UniTask[operations.Length + 1];
        //
        //     for (var index = 0; index < operations.Length; index++)
        //         operationTasks[index] = operations[index](loadReport);
        //
        //     operationTasks[operations.Length] = showLoadingScreenTask;
        //
        //     await UniTask.WhenAll(operationTasks);
        // }
    }
}
