using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.HomeMarker;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.MapRenderer.ComponentsFactory
{
	internal struct HomeMarkerInstaller
	{
		public async UniTask InstallAsync(
			Dictionary<MapLayer, IMapLayerController> writer,
			List<IZoomScalingLayer> zoomScalingWriter,
			MapRendererConfiguration configuration,
			ICoordsUtils coordsUtils,
			IMapCullingController cullingController,
			IMapRendererSettings mapSettings,
			IAssetsProvisioner assetsProvisioner,
			HomePlaceEventBus homePlaceEventBus,
			CancellationToken cancellationToken)
		{
			HomeMarkerObject prefab = (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.HomeMarker, ct: cancellationToken)).Value;

			var homeMarkerController = new HomeMarkerController(
				CreateMarker,
				configuration.HomeMarkerRoot,
				coordsUtils,
				cullingController,
				homePlaceEventBus
			);

			homeMarkerController.Initialize();

			writer.Add(MapLayer.HomeMarker, homeMarkerController);
			zoomScalingWriter.Add(homeMarkerController);
			return;

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