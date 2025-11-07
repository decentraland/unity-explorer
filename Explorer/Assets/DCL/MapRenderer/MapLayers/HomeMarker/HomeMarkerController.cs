using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
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

		public bool HomeIsSet => currentMarker != null;
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
			}

			if (serialize)
				HomeMarkerSerializer.Serialize(homeMarkerData);
		}

		private void SetAsHomeRequested(Vector2Int coordinates)
		{
			currentMarker = new HomeMarkerData(coordinates);
			SetMarker(currentMarker);
		}

		private void UnsetAsHomeRequested(Vector2Int coordinates)
		{
			if (currentMarker == null || currentMarker.Value.Position != coordinates)
				return;
			
			currentMarker = null;
			SetMarker(null);
		}

		public UniTask InitializeAsync(CancellationToken cancellationToken) =>
			UniTask.CompletedTask;

		public UniTask EnableAsync(CancellationToken cancellationToken)
		{
			if(HomeIsSet)
				homeMarker.SetActive(true);
			
			return UniTask.CompletedTask;
		}

		public UniTask Disable(CancellationToken cancellationToken)
		{
			homeMarker.SetActive(false);
			
			mapCullingController.StopTracking(homeMarker);
			
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