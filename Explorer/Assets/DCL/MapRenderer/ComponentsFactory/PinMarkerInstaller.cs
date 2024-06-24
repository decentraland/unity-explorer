using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.Pins;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.MapRenderer.ComponentsFactory
{
    internal struct PinMarkerInstaller
    {
        private const int PREWARM_COUNT = 60;

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
            CancellationToken cancellationToken)
        {
            this.mapSettings = settings;
            this.assetsProvisioner = assetProv;
            PinMarkerObject prefab = await GetPrefabAsync(cancellationToken);

            var objectsPool = new ObjectPool<PinMarkerObject>(
                () => CreatePoolMethod(configuration, prefab, coordsUtils),
                defaultCapacity: PREWARM_COUNT,
                actionOnGet: (obj) => obj.gameObject.SetActive(true),
                actionOnRelease: (obj) => obj.gameObject.SetActive(false));

            var controller = new PinMarkerController(
                objectsPool,
                CreateMarker,
                configuration.PinMarkerRoot,
                coordsUtils,
                cullingController
            );

            await controller.InitializeAsync(cancellationToken);
            writer.Add(MapLayer.Pins, controller);
            zoomScalingWriter.Add(controller);
        }

        private static PinMarkerObject CreatePoolMethod(MapRendererConfiguration configuration, PinMarkerObject prefab, ICoordsUtils coordsUtils)
        {
            PinMarkerObject pinMarkerObject = Object.Instantiate(prefab, configuration.PinMarkerRoot);
            for (var i = 0; i < pinMarkerObject.renderers.Length; i++)
                pinMarkerObject.renderers[i].sortingOrder = MapRendererDrawOrder.PIN_MARKER;

            pinMarkerObject.title.sortingOrder = MapRendererDrawOrder.PIN_MARKER;
            coordsUtils.SetObjectScale(pinMarkerObject);
            return pinMarkerObject;
        }

        private static IPinMarker CreateMarker(IObjectPool<PinMarkerObject> objectsPool, IMapCullingController cullingController) =>
            new PinMarker(objectsPool, cullingController);

        internal async UniTask<PinMarkerObject> GetPrefabAsync(CancellationToken cancellationToken) =>
            (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.PinMarker, ct: cancellationToken)).Value.GetComponent<PinMarkerObject>();

    }
}
