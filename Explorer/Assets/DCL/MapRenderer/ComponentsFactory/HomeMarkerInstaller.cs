using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.Navmap;
using DCL.PlacesAPIService;
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
				analyticsEventBus
			);
			homePlaceEventBus.Controller = homeMarkerController;

			homeMarkerController.Initialize();

			writer.Add(MapLayer.HomeMarker, homeMarkerController);
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