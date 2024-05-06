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
using ECS.Prioritization.Components;
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

        private readonly ObjectProxy<Entity> cameraEntity;
        private readonly CameraSamplingData cameraSamplingData;


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
                await loadingScreen.ShowWhileExecuteTaskAsync(async parentLoadReport =>
                    {
                        ct.ThrowIfCancellationRequested();

                        remoteEntities.ForceRemoveAll(world);
                        await roomHub.StopIfNotAsync();
                        // Re-add on exception? If there is timeout
                        world.Remove<CameraSamplingData>(cameraEntity.Object);

                        await ChangeRealmAsync(realm, ct);
                        parentLoadReport.SetProgress(RealFlowLoadingStatus.PROGRESS[ProfileLoaded]);

                        var landscapeLoadReport
                            = parentLoadReport.CreateChildReport(RealFlowLoadingStatus.PROGRESS[LandscapeLoaded]);
                        await LoadTerrainAsync(landscapeLoadReport, ct);
                        parentLoadReport.SetProgress(RealFlowLoadingStatus.PROGRESS[LandscapeLoaded]);

                        var teleportLoadReport
                            = parentLoadReport.CreateChildReport(RealFlowLoadingStatus.PROGRESS[PlayerTeleported]);
                        await InitializeTeleportToSpawnPointAsync(teleportLoadReport, ct, parcelToTeleport);
                        parentLoadReport.SetProgress(RealFlowLoadingStatus.PROGRESS[PlayerTeleported]);

                        await roomHub.StartAsync();
                        parentLoadReport.SetProgress(RealFlowLoadingStatus.PROGRESS[Completed]);

                    },
                    ct
                );

            }
            catch (TimeoutException)
            {
                if (!world.Has<CameraSamplingData>(cameraEntity.Object))
                {
                    world.Add(cameraEntity.Object, cameraSamplingData);
                }

                ;
            }

            return true;
        }

        public async UniTask InitializeTeleportToSpawnPointAsync(AsyncLoadProcessReport teleportLoadReport, CancellationToken ct, Vector2Int parcelToTeleport)
        {
            var world = globalWorldProxy.Object.EnsureNotNull();
            bool isGenesis = !realmController.GetRealm().ScenesAreFixed;
            UniTask? waitForSceneReadiness = null;
            if (isGenesis)
                waitForSceneReadiness = await TeleportToParcelAsync(parcelToTeleport, teleportLoadReport, ct);
            else
                waitForSceneReadiness = await TeleportToWorldSpawnPointAsync(teleportLoadReport, ct);

            // add camera sampling data to the camera entity to start partitioning
            Assert.IsTrue(cameraEntity.Configured);
            world.Add(cameraEntity.Object, cameraSamplingData);
            await waitForSceneReadiness.Value;
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
                    await loadingScreen.ShowWhileExecuteTaskAsync(async parentLoadReport =>
                    {
                        ct.ThrowIfCancellationRequested();
                        parentLoadReport.SetProgress(RealFlowLoadingStatus.PROGRESS[LandscapeLoaded]);

                        var teleportLoadReport
                            = parentLoadReport.CreateChildReport(RealFlowLoadingStatus.PROGRESS[PlayerTeleported]);
                        var waitForSceneReadiness = await TeleportToParcelAsync(parcel, teleportLoadReport, ct);
                        await waitForSceneReadiness;

                        parentLoadReport.SetProgress(RealFlowLoadingStatus.PROGRESS[Completed]);
                    }, ct);
                }
            }
            catch (TimeoutException) { }
        }

        public async UniTask LoadTerrainAsync(AsyncLoadProcessReport landscapeLoadReport, CancellationToken ct)
        {
            if (landscapeEnabled)
            {
                bool isGenesis = !realmController.GetRealm().ScenesAreFixed;
                if (isGenesis)
                {
                    //TODO (Juani): The world terrain would be hidden. We need to implement the re-usage when going back
                    worldsTerrain.SwitchVisibility(false);
                    if (!genesisTerrain.IsTerrainGenerated)
                        await genesisTerrain.GenerateTerrainAsync(cancellationToken: ct);
                    else
                        await genesisTerrain.ShowAsync(landscapeLoadReport);
                }
                else
                {
                    genesisTerrain.Hide();
                    await GenerateWorldTerrainAsync((uint)realmController.GetRealm().GetHashCode(), landscapeLoadReport, ct);
                }
            }
        }

        private async UniTask<UniTask> TeleportToParcelAsync(Vector2Int parcel, AsyncLoadProcessReport processReport, CancellationToken ct)
        {
            var waitForSceneReadiness = await teleportController.TeleportToSceneSpawnPointAsync(parcel, processReport, ct);
            return waitForSceneReadiness.ToUniTask(ct);
        }


        private async UniTask<UniTask> TeleportToWorldSpawnPointAsync(AsyncLoadProcessReport processReport, CancellationToken ct)
        {
            await UniTask.WaitUntil(() => realmController.GlobalWorld.EcsWorld.Has<FixedScenePointers>(realmController.RealmEntity), cancellationToken: ct);
            await UniTask.WaitUntil(() => realmController.GlobalWorld.EcsWorld.Get<FixedScenePointers>(realmController.RealmEntity).AllPromisesResolved, cancellationToken: ct);
            AssetPromise<SceneEntityDefinition, GetSceneDefinition>[] promises = realmController.GlobalWorld.EcsWorld.Get<FixedScenePointers>(realmController.RealmEntity).Promises;
            var waitForSceneReadiness = await teleportController.TeleportToSceneSpawnPointAsync(promises[0].Result!.Value.Asset!.metadata.scene.DecodedBase, processReport, ct);
            return waitForSceneReadiness.ToUniTask(ct);
        }

        private async UniTask ChangeRealmAsync(URLDomain realm, CancellationToken ct)
        {
            await realmController.SetRealmAsync(realm, ct);
            await SwitchMiscVisibilityAsync();
        }
        
        private async UniTask GenerateWorldTerrainAsync(uint worldSeed, AsyncLoadProcessReport processReport, CancellationToken ct)
        {
            if (!worldsTerrain.IsInitialized)
                return;

            await UniTask.WaitUntil(() => realmController.GlobalWorld.EcsWorld.Has<FixedScenePointers>(realmController.RealmEntity), cancellationToken: ct);
            await UniTask.WaitUntil(() => realmController.GlobalWorld.EcsWorld.Get<FixedScenePointers>(realmController.RealmEntity).AllPromisesResolved, cancellationToken: ct);

            AssetPromise<SceneEntityDefinition, GetSceneDefinition>[] promises = realmController.GlobalWorld.EcsWorld.Get<FixedScenePointers>(realmController.RealmEntity).Promises;

            var decodedParcelsAmount = 0;

            foreach (AssetPromise<SceneEntityDefinition, GetSceneDefinition> promise in promises)
            {
                //TODO (JUANI) : Shouldnt be possible, but some promises result are null
                if (promise.Result == null) continue;
                decodedParcelsAmount += promise.Result!.Value.Asset!.metadata.scene.DecodedParcels.Count;
            }

            using (var ownedParcels = new NativeParallelHashSet<int2>(decodedParcelsAmount, AllocatorManager.Persistent))
            {
                foreach (AssetPromise<SceneEntityDefinition, GetSceneDefinition> promise in promises)
                {
                    //TODO (JUANI) : Shouldnt be possible, but some promises result are null
                    if (promise.Result == null) continue;
                    foreach (var parcel in promise.Result!.Value.Asset!.metadata.scene.DecodedParcels)
                        ownedParcels.Add(parcel.ToInt2());
                }

                await worldsTerrain.GenerateTerrainAsync(ownedParcels, worldSeed, processReport, cancellationToken: ct);
            }
        }

        public async UniTask SwitchMiscVisibilityAsync()
        {
            bool isGenesis = !realmController.GetRealm().ScenesAreFixed;
            // is NOT visible

            // isVisible
            mapRenderer.SetSharedLayer(MapLayer.PlayerMarker, isGenesis);
            await satelliteFloor.SwitchVisibility(isGenesis);
            roadsPlugin.RoadAssetPool?.SwitchVisibility(isGenesis);
        }
    }
}
