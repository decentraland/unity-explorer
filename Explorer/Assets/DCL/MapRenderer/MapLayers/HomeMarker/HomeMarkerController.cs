using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.PlacesAPIService;
using Utility;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.HomeMarker
{
	public class HomeMarkerController : MapLayerControllerBase, IMapLayerController, IZoomScalingLayer
	{
		internal delegate IHomeMarker HomeMarkerBuilder(Transform parent);
		
		private readonly HomeMarkerBuilder builder;
		private readonly HomePlaceEventBus homePlaceEventBus;

		private IHomeMarker homeMarker;
		private HomeMarkerData? currentMarker = null;
		
		public bool ZoomBlocked { get; set; }
		
		internal HomeMarkerController(
			HomeMarkerBuilder builder,
			Transform instantiationParent, 
			ICoordsUtils coordsUtils, 
			IMapCullingController cullingController,
			HomePlaceEventBus homePlaceEventBus) 
			: base(instantiationParent, coordsUtils, cullingController)
		{
			this.builder = builder;
			this.homePlaceEventBus = homePlaceEventBus;

			this.homePlaceEventBus.SetAsHomeRequested += SetAsHomeRequested;
			this.homePlaceEventBus.UnsetAsHomeRequested += UnsetAsHomeRequested;
			this.homePlaceEventBus.IsHomeQuery = (coordinates) => 
				currentMarker != null && currentMarker.Value.Position == coordinates;
			this.homePlaceEventBus.GetHomeCoordinatesQuery = () => currentMarker?.Position;
		}

		internal void Initialize()
		{
			homeMarker = builder(instantiationParent);
			currentMarker = HomeMarkerSerializer.Deserialize();
			SetMarker(currentMarker, false);
		}

		private void SetMarker(HomeMarkerData? homeMarkerData, bool serialize = true)
		{
			homeMarker.SetActive(homeMarkerData != null);

			if (homeMarkerData != null)
			{
				homeMarker.SetPosition(coordsUtils.CoordsToPositionWithOffset(homeMarkerData.Value.Position));
				homeMarker.SetTitle(homeMarkerData.Value.Title);
			}

			if (serialize)
				HomeMarkerSerializer.Serialize(homeMarkerData);
		}

		private void SetAsHomeRequested(PlacesData.PlaceInfo placeInfo)
		{
			currentMarker = new HomeMarkerData(placeInfo.base_position, placeInfo.title);
			SetMarker(currentMarker);
		}

		private void UnsetAsHomeRequested(PlacesData.PlaceInfo placeInfo)
		{
			if (currentMarker == null 
			    || !VectorUtilities.TryParseVector2Int(placeInfo.base_position, out var position)
			    || currentMarker.Value.Position != position)
				return;
			
			SetMarker(null);
		}

		public UniTask InitializeAsync(CancellationToken cancellationToken) =>
			UniTask.CompletedTask;

		public UniTask EnableAsync(CancellationToken cancellationToken)
		{
			homeMarker.SetActive(true);
			return UniTask.CompletedTask;
		}

		public UniTask Disable(CancellationToken cancellationToken)
		{
			homeMarker.SetActive(false);
			return UniTask.CompletedTask;
		}

		public void ApplyCameraZoom(float baseZoom, float zoom, int zoomLevel)
		{
			if (ZoomBlocked)
				return;

			homeMarker.SetZoom(coordsUtils.ParcelSize, baseZoom, zoom);
		}

		public void ResetToBaseScale()
		{
			homeMarker.ResetToBaseScale();
		}

		protected override void DisposeImpl()
		{
			homeMarker.Dispose();
		}
	}
}