using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Browser.DecentralandUrls;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.Users;
using DCL.MapRenderer.MapLayers.UsersMarker;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility.TeleportBus;

namespace DCL.MapRenderer.ComponentsFactory
{
    internal struct HotUsersMarkersInstaller
    {
        private const int HOT_USER_MARKERS_PREWARM_COUNT = 30;

        private IAssetsProvisioner assetsProvisioner;
        private IMapRendererSettings mapSettings;

        public async UniTask InstallAsync(
            Dictionary<MapLayer, IMapLayerController> writer,
            MapRendererConfiguration configuration,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            IAssetsProvisioner assetsProv,
            IMapRendererSettings settings,
            IWebRequestController webRequestController,
            IDecentralandUrlsSource decentralandUrls,
            ITeleportBusController teleportBusController,
            CancellationToken cancellationToken)
        {
            assetsProvisioner = assetsProv;
            mapSettings = settings;
            HotUserMarkerObject? prefab = await GetPrefabAsync(cancellationToken);

            var objectsPool = new ObjectPool<HotUserMarkerObject>(
                () => CreatePoolMethod(configuration, prefab, coordsUtils),
                actionOnGet: obj => obj.gameObject.SetActive(true),
                actionOnRelease: obj => obj.gameObject.SetActive(false),
                defaultCapacity: HOT_USER_MARKERS_PREWARM_COUNT);

            IHotUserMarker CreateWrap() =>
                new HotUserMarker(objectsPool, coordsUtils);

            var wrapsPool = new ObjectPool<IHotUserMarker>(CreateWrap, actionOnRelease: m => m.Dispose(), defaultCapacity: HOT_USER_MARKERS_PREWARM_COUNT);

            var controller = new UsersMarkersHotAreaController(objectsPool, wrapsPool, configuration.HotUserMarkersRoot, coordsUtils, cullingController, webRequestController, decentralandUrls, teleportBusController);
            await controller.InitializeAsync(cancellationToken);
            writer.Add(MapLayer.HotUsersMarkers, controller);
        }

        private static HotUserMarkerObject CreatePoolMethod(MapRendererConfiguration configuration, HotUserMarkerObject prefab, ICoordsUtils coordsUtils)
        {
            HotUserMarkerObject markerObject = Object.Instantiate(prefab, configuration.HotUserMarkersRoot);
            markerObject.UpdateSortOrder(MapRendererDrawOrder.HOT_USER_MARKERS);
            coordsUtils.SetObjectScale(markerObject);
            return markerObject;
        }

        private async UniTask<HotUserMarkerObject> GetPrefabAsync(CancellationToken cancellationToken) =>
            (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.UserMarker, cancellationToken)).Value;
    }
}
