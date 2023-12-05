using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers;
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
            CancellationToken cancellationToken)
        {
            this.mapSettings = settings;
            this.assetsProvisioner = assetProv;
            PlayerMarkerObject prefab = await GetPrefabAsync(cancellationToken);

            var controller = new PlayerMarkerController(
                CreateMarker,
                configuration.PlayerMarkerRoot,
                coordsUtils,
                cullingController
            );

            controller.Initialize();

            writer.Add(MapLayer.PlayerMarker, controller);
            zoomScalingWriter.Add(controller);
            return;

            IPlayerMarker CreateMarker(Transform parent)
            {
                PlayerMarkerObject pmObject = Object.Instantiate(prefab, parent);
                coordsUtils.SetObjectScale(pmObject);
                pmObject.SetSortingOrder(MapRendererDrawOrder.PLAYER_MARKER);
                pmObject.SetAnimatedCircleVisibility(true);

                return new PlayerMarker(pmObject);
            }
        }

        internal async UniTask<PlayerMarkerObject> GetPrefabAsync(CancellationToken cancellationToken) =>
            (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.PlayerMarker, ct: CancellationToken.None)).Value.GetComponent<PlayerMarkerObject>();

    }
}
