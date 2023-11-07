using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCLServices.MapRenderer.CoordsUtils;
using DCLServices.MapRenderer.Culling;
using DCLServices.MapRenderer.MapLayers;
using DCLServices.MapRenderer.MapLayers.PlayerMarker;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCLServices.MapRenderer.ComponentsFactory
{
    internal struct PlayerMarkerInstaller
    {
        private IAssetsProvisioner assetsProvisioner;
        private MapRendererSettings mapSettings;

        public async UniTask Install(
            Dictionary<MapLayer, IMapLayerController> writer,
            List<IZoomScalingLayer> zoomScalingWriter,
            MapRendererConfiguration configuration,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            MapRendererSettings mapSettings,
            CancellationToken cancellationToken)
        {
            this.mapSettings = mapSettings;
            PlayerMarkerObject prefab = await GetPrefab(cancellationToken);

            var controller = new PlayerMarkerController(
                CreateMarker,
                //DataStore.i.player.playerWorldPosition,
                //CommonScriptableObjects.cameraForward,
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

        internal async UniTask<PlayerMarkerObject> GetPrefab(CancellationToken cancellationToken) =>
            (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.MapCameraObject, ct: CancellationToken.None)).Value.GetComponent<PlayerMarkerObject>();

    }
}
