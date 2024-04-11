using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Ipfs;
using DCL.Landscape;
using DCL.MapRenderer;
using DCL.MapRenderer.MapLayers;
using DCL.ParcelsService;
using DCL.PluginSystem.Global;
using DCL.Roads.Systems;
using DCL.SceneLoadingScreens;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.Realm;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using MVC;
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

        private readonly MVCManager mvcManager;
        private readonly IMapRenderer mapRenderer;
        private readonly IRealmController realmController;
        private readonly ITeleportController teleportController;
        private readonly LandscapePlugin landscapePlugin;
        private readonly RoadPlugin roadsPlugin;
        private readonly WorldTerrainGenerator worldsTerrainGenerator;

        public RealmNavigator(MVCManager mvcManager, IMapRenderer mapRenderer, IRealmController realmController, ITeleportController teleportController,
            LandscapePlugin landscapePlugin, RoadPlugin roadsPlugin)
        {
            this.mvcManager = mvcManager;
            this.mapRenderer = mapRenderer;
            this.realmController = realmController;
            this.teleportController = teleportController;
            this.landscapePlugin = landscapePlugin;
            this.roadsPlugin = roadsPlugin;
        }

        public async UniTask<bool> TryChangeRealmAsync(URLDomain realm, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (!await realmController.IsReachableAsync(realm, ct))
                return false;

            ct.ThrowIfCancellationRequested();

            SwitchMiscVisibility(realm == genesisDomain);

            await ShowLoadingScreenAndExecuteTaskAsync(ct,
                async loadReport =>
                {
                    await realmController.SetRealmAsync(realm, Vector2Int.zero, loadReport, ct);

                    if (realm != genesisDomain)
                        await GenerateWorldTerrain(ct);
                });

            return true;
        }

        private async UniTask GenerateWorldTerrain(CancellationToken ct)
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

            await landscapePlugin.WorldTerrainGenerator.GenerateTerrainAsync(ownedParcels, cancellationToken: ct);
        }

        public async UniTask TeleportToParcelAsync(Vector2Int parcel, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            await ShowLoadingScreenAndExecuteTaskAsync(ct, async loadReport =>
            {
                if (realmController.GetRealm().Ipfs.CatalystBaseUrl != genesisDomain)
                {
                    await realmController.SetRealmAsync(genesisDomain, Vector2Int.zero, loadReport, ct);
                    SwitchMiscVisibility(true);

                    ct.ThrowIfCancellationRequested();
                }

                WaitForSceneReadiness? waitForSceneReadiness = await teleportController.TeleportToSceneSpawnPointAsync(parcel, loadReport, ct);
                await waitForSceneReadiness.ToUniTask(ct);
            });
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

        private async UniTask ShowLoadingScreenAndExecuteTaskAsync(CancellationToken ct, params Func<AsyncLoadProcessReport, UniTask>[] operations)
        {
            ct.ThrowIfCancellationRequested();

            var timeout = TimeSpan.FromSeconds(30);
            var loadReport = AsyncLoadProcessReport.Create();

            UniTask showLoadingScreenTask = mvcManager.ShowAsync(
                                                           SceneLoadingScreenController.IssueCommand(
                                                               new SceneLoadingScreenController.Params(loadReport, timeout)), ct)
                                                      .AttachExternalCancellation(ct);

            var operationTasks = new UniTask[operations.Length + 1];

            for (var index = 0; index < operations.Length; index++)
                operationTasks[index] = operations[index](loadReport);

            operationTasks[operations.Length] = showLoadingScreenTask;

            await UniTask.WhenAll(operationTasks);
        }
    }
}
