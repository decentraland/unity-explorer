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
        public async UniTask InstallAsync(
            Dictionary<MapLayer, IMapLayerController> writer,
            List<IZoomScalingLayer> zoomScalingWriter,
            MapRendererConfiguration configuration,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            MapRendererSettings mapSettings,
            IAssetsProvisioner assetsProvisioner,
            IMapPathEventBus mapPathEventBus,
            CancellationToken cancellationToken)
        {
            ProvidedAsset<GameObject> providedAsset = await assetsProvisioner.ProvideMainAssetAsync(mapSettings.PlayerMarker, ct: cancellationToken);
            PlayerMarkerObject prefab = providedAsset.Value.GetComponent<PlayerMarkerObject>();

            var playerMarkerController = new PlayerMarkerController(
                CreateMarker,
                configuration.PlayerMarkerRoot,
                coordsUtils,
                cullingController,
                mapPathEventBus
            );

            playerMarkerController.Initialize();

            writer.Add(MapLayer.PlayerMarker, playerMarkerController);
            zoomScalingWriter.Add(playerMarkerController);
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
    }
}
