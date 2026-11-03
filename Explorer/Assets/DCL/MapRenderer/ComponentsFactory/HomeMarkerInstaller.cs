using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Diagnostics;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.Navmap;
using DCL.PlacesAPIService;
using DCL.Web3.Identities;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.MapRenderer.ComponentsFactory
{
    internal struct HomeMarkerInstaller
    {
        public async UniTask<IMapLayerController> InstallAsync(
            Dictionary<MapLayer, IMapLayerController> writer,
            List<IZoomScalingLayer> zoomScalingWriter,
            MapRendererConfiguration configuration,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            INavmapBus navmapBus,
            IPlacesAPIService placesAPIService,
            IMapRendererSettings mapSettings,
            IAssetsProvisioner assetsProvisioner,
            HomePlaceEventBus homePlaceEventBus,
            IEventBus analyticsEventBus,
            IWeb3IdentityCache identityCache,
            CancellationToken cancellationToken)
        {
            HomeMarkerObject prefab = (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.HomeMarker, ct: cancellationToken)).Value;

            var homeMarkerController = new HomeMarkerController(
                CreateMarker,
                configuration.HomeMarkerRoot,
                coordsUtils,
                cullingController,
                navmapBus,
                placesAPIService,
                analyticsEventBus,
                identityCache
            );

            homePlaceEventBus.SetController(homeMarkerController);

            homeMarkerController.Initialize();

            if (!writer.TryAdd(MapLayer.HomeMarker, homeMarkerController))
                ReportHub.LogError(ReportCategory.UI, $"Map layer {MapLayer.HomeMarker} is already installed");
            zoomScalingWriter.Add(homeMarkerController);

            return homeMarkerController;

            IHomeMarker CreateMarker(Transform parent)
            {
                HomeMarkerObject markerObject = Object.Instantiate(prefab, parent);
                coordsUtils.SetObjectScale(markerObject);
                markerObject.SetSortingOrder(MapRendererDrawOrder.HOME_MARKER);

                return new HomeMarker(markerObject);
            }
        }
    }
}
