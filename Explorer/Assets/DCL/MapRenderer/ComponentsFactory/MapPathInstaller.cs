using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.Pins;
using DCL.Notification.NotificationsBus;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.MapRenderer.ComponentsFactory
{
    internal struct MapPathInstaller
    {
        private IAssetsProvisioner assetsProvisioner;
        private MapRendererSettings mapSettings;

        public async UniTask InstallAsync(
            Dictionary<MapLayer, IMapLayerController> writer,
            List<IZoomScalingLayer> zoomScalingWriter,
            MapRendererConfiguration configuration,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            MapRendererSettings settings,
            IAssetsProvisioner assetProv,
            IMapPathEventBus mapPathEventBus,
            INotificationsBusController notificationsBusController,
            CancellationToken cancellationToken)
        {
            mapSettings = settings;
            assetsProvisioner = assetProv;
            PinMarkerObject prefab = await GetPrefabAsync(cancellationToken);

            var objectsPool = new ObjectPool<PinMarkerObject>(
                () => CreatePoolMethod(configuration, prefab, coordsUtils),
                defaultCapacity: 1,
                actionOnGet: obj => obj.gameObject.SetActive(true),
                actionOnRelease: obj => obj.gameObject.SetActive(false));

            ProvidedInstance<MapPathRenderer> pathInstance = await assetsProvisioner.ProvideInstanceAsync(mapSettings.DestinationPathLine, configuration.MapPathRoot, ct: cancellationToken);

            var pathRendererController = new MapPathController(
                objectsPool,
                CreateMarker,
                configuration.MapPathRoot,
                mapPathEventBus,
                pathInstance.Value,
                coordsUtils,
                cullingController,
                notificationsBusController);

            pathRendererController.Initialize();

            writer.Add(MapLayer.Path, pathRendererController);
            zoomScalingWriter.Add(pathRendererController);
        }

        private static PinMarkerObject CreatePoolMethod(MapRendererConfiguration configuration, PinMarkerObject prefab, ICoordsUtils coordsUtils)
        {
            PinMarkerObject pinMarkerObject = Object.Instantiate(prefab, configuration.PinMarkerRoot);

            for (var i = 0; i < pinMarkerObject.renderers.Length; i++)
                pinMarkerObject.renderers[i].sortingOrder = MapRendererDrawOrder.PIN_MARKER;

            pinMarkerObject.mapPinIcon.sortingOrder = MapRendererDrawOrder.PIN_MARKER_THUMBNAIL;
            pinMarkerObject.mapPinIconOutline.sortingOrder = MapRendererDrawOrder.PIN_MARKER_OUTLINE;

            coordsUtils.SetObjectScale(pinMarkerObject);
            return pinMarkerObject;
        }

        private static IPinMarker CreateMarker(IObjectPool<PinMarkerObject> objectsPool, IMapCullingController cullingController) =>
            new PinMarker(objectsPool, cullingController);

        internal async UniTask<PinMarkerObject> GetPrefabAsync(CancellationToken cancellationToken) =>
            (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.PathDestinationPin, ct: cancellationToken)).Value;
    }
}
