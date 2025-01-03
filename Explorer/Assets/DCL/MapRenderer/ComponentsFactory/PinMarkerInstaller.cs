using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.MapPins.Bus;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.Pins;
using DCL.Navmap;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.MapRenderer.ComponentsFactory
{
    internal struct PinMarkerInstaller
    {
        private IAssetsProvisioner assetsProvisioner;
        private IMapRendererSettings mapSettings;

        public async UniTask<PinMarkerController> InstallAsync(
            Dictionary<MapLayer, IMapLayerController> writer,
            List<IZoomScalingLayer> zoomScalingWriter,
            MapRendererConfiguration configuration,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            IMapRendererSettings settings,
            IAssetsProvisioner assetProv,
            IMapPathEventBus mapPathEventBus,
            IMapPinsEventBus mapPinsEventBus,
            INavmapBus navmapBus,
            CancellationToken cancellationToken)
        {
            mapSettings = settings;
            assetsProvisioner = assetProv;
            PinMarkerObject prefab = await GetPrefabAsync(cancellationToken);

            var objectsPool = new ObjectPool<PinMarkerObject>(
                () => CreatePoolMethod(configuration, prefab, coordsUtils),
                actionOnGet: obj => obj.gameObject.SetActive(true),
                actionOnRelease: obj => obj.gameObject.SetActive(false));

            var controller = new PinMarkerController(
                objectsPool,
                CreateMarker,
                configuration.PinMarkerRoot,
                coordsUtils,
                cullingController,
                mapPathEventBus,
                mapPinsEventBus,
                navmapBus
            );

            writer.Add(MapLayer.Pins, controller);
            zoomScalingWriter.Add(controller);
            return controller;
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

        private async UniTask<PinMarkerObject> GetPrefabAsync(CancellationToken cancellationToken) =>
            (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.PinMarker, ct: cancellationToken)).Value;
    }
}
