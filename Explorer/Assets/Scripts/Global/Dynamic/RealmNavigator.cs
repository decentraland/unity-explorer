using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.MapRenderer;
using DCL.MapRenderer.MapLayers;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.ParcelsService;
using DCL.SceneLoadingScreens.LoadingScreen;
using ECS.SceneLifeCycle.Realm;
using ECS.SceneLifeCycle.Reporting;
using System.Threading;
using UnityEngine;

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

        public RealmNavigator(ILoadingScreen loadingScreen, IMapRenderer mapRenderer, IRealmController realmController, ITeleportController teleportController, IRoomHub roomHub)
        {
            this.loadingScreen = loadingScreen;
            this.mapRenderer = mapRenderer;
            this.realmController = realmController;
            this.teleportController = teleportController;
            this.roomHub = roomHub;
        }

        public async UniTask<bool> TryChangeRealmAsync(URLDomain realm, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (!await realmController.IsReachableAsync(realm, ct))
                return false;

            ct.ThrowIfCancellationRequested();
            mapRenderer.SetSharedLayer(MapLayer.PlayerMarker, realm == genesisDomain);

            await loadingScreen.ShowWhileExecuteTaskAsync(async loadReport =>
                {
                    roomHub.Reconnect();
                    await realmController.SetRealmAsync(realm, Vector2Int.zero, loadReport, ct);
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
                    mapRenderer.SetSharedLayer(MapLayer.PlayerMarker, true);

                    ct.ThrowIfCancellationRequested();
                }

                WaitForSceneReadiness? waitForSceneReadiness = await teleportController.TeleportToSceneSpawnPointAsync(parcel, loadReport, ct);
                await waitForSceneReadiness.ToUniTask(ct);
            }, ct);
        }
    }
}
