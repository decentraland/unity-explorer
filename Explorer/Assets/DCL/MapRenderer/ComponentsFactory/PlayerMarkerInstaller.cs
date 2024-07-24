using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.Pins;
using DCL.MapRenderer.MapLayers.PlayerMarker;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.MapRenderer.ComponentsFactory
{
    internal struct PlayerMarkerInstaller
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
            CancellationToken cancellationToken)
        {
            mapSettings = settings;
            assetsProvisioner = assetProv;
            PlayerMarkerObject prefab = await GetPrefabAsync(cancellationToken);

            var playerMarkerController = new PlayerMarkerController(
                CreateMarker,
                configuration.PlayerMarkerRoot,
                coordsUtils,
                cullingController
            );

            playerMarkerController.Initialize();

            ProvidedInstance<PathRenderer> pathInstance = await assetsProvisioner.ProvideInstanceAsync(mapSettings.DestinationPathLine, configuration.PlayerMarkerRoot, ct: CancellationToken.None);
            ProvidedInstance<PinMarkerObject> pinMarkerInstance = await assetsProvisioner.ProvideInstanceAsync(mapSettings.PathDestinationPin, configuration.PlayerMarkerRoot, ct: CancellationToken.None);

            var pathRendererController = new PathRendererController(
                CreatePinMarkerObject,
                configuration.PlayerMarkerRoot,
                mapPathEventBus,
                pathInstance.Value,
                coordsUtils,
                cullingController);

            pathRendererController.Initialize(playerMarkerController.PlayerMarkerTransform);

            writer.Add(MapLayer.PlayerMarker, playerMarkerController);
            writer.Add(MapLayer.Path, pathRendererController);

            zoomScalingWriter.Add(playerMarkerController);
            zoomScalingWriter.Add(pathRendererController);
            return;

            IPlayerMarker CreateMarker(Transform parent)
            {
                PlayerMarkerObject pmObject = Object.Instantiate(prefab, parent);
                coordsUtils.SetObjectScale(pmObject);
                pmObject.SetSortingOrder(MapRendererDrawOrder.PLAYER_MARKER);
                pmObject.SetAnimatedCircleVisibility(true);

                return new PlayerMarker(pmObject);
            }

            PinMarkerObject CreatePinMarkerObject(Transform parent)
            {
                PinMarkerObject pmObject = pinMarkerInstance.Value;
                pmObject.mapPinIconOutline.sortingOrder = MapRendererDrawOrder.PIN_MARKER_OUTLINE;

                for (var i = 0; i < pmObject.renderers.Length; i++) { pmObject.renderers[i].sortingOrder = MapRendererDrawOrder.PIN_MARKER; }

                pmObject.mapPinIcon.sortingOrder = MapRendererDrawOrder.PIN_MARKER_THUMBNAIL;

                coordsUtils.SetObjectScale(pmObject);
                return pmObject;
            }
        }

        internal async UniTask<PlayerMarkerObject> GetPrefabAsync(CancellationToken cancellationToken) =>
            (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.PlayerMarker, ct: CancellationToken.None)).Value.GetComponent<PlayerMarkerObject>();
    }
}
