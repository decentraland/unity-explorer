using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.Navmap;
using DCL.PlacesAPIService;
using UnityEngine;
using Utility;

namespace DCL.MapRenderer.MapLayers.HomeMarker
{
	/// <summary>
	/// Controls the home marker on the map, handling placement, highlighting, and interaction with the home location.
	/// </summary>
	public class HomeMarkerController : MapLayerControllerBase, IMapLayerController, IZoomScalingLayer
	{
		internal delegate IHomeMarker HomeMarkerBuilder(Transform parent);
		
		private readonly HomeMarkerBuilder builder;
		private readonly INavmapBus navmapBus;
		private readonly IPlacesAPIService placesAPIService;
		private readonly HomePlaceEventBus homePlaceEventBus;

		private IHomeMarker homeMarker;
		private HomeMarkerData? currentMarker = null;
		private CancellationTokenSource highlightCt = new();
		private CancellationTokenSource deHighlightCt = new();
		private CancellationTokenSource placesCts = new();

		public bool HomeIsSet => currentMarker != null;
		public bool ZoomBlocked { get; set; }
		
		internal HomeMarkerController(
			HomeMarkerBuilder builder,
			Transform instantiationParent, 
			ICoordsUtils coordsUtils, 
			IMapCullingController cullingController,
			INavmapBus navmapBus,
			IPlacesAPIService placesAPIService,
			HomePlaceEventBus homePlaceEventBus) 
			: base(instantiationParent, coordsUtils, cullingController)
		{
			this.builder = builder;
			this.navmapBus = navmapBus;
			this.placesAPIService = placesAPIService;
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

		protected override void DisposeImpl()
		{
			highlightCt.SafeCancelAndDispose();
			deHighlightCt.SafeCancelAndDispose();
			homeMarker.Dispose();
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

		private void UnsetAsHomeRequested()
		{
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
		
		public bool TryHighlightObject(GameObject gameObject, out IMapRendererMarker? mapMarker)
		{
			mapMarker = null;
			
			if (gameObject.GetInstanceID() != homeMarker.MarkerObject.gameObject.GetInstanceID())
				return false;
    
			mapMarker = homeMarker;
			highlightCt = highlightCt.SafeRestart();
			homeMarker.AnimateSelectionAsync(highlightCt.Token);
			return true;
		}

		public bool TryDeHighlightObject(GameObject gameObject)
		{
			if (gameObject.GetInstanceID() != homeMarker.MarkerObject.gameObject.GetInstanceID())
				return false;
			
			deHighlightCt = deHighlightCt.SafeRestart();
			homeMarker.AnimateDeSelectionAsync(deHighlightCt.Token);
			return true;
		}

		public bool TryClickObject(GameObject gameObject, CancellationTokenSource cts, out IMapRendererMarker? mapRenderMarker)
		{
			mapRenderMarker = null;
			
			if (gameObject.GetInstanceID() != homeMarker.MarkerObject.gameObject.GetInstanceID())
				return false;

			DisplayPlacesInfoPanelAsync().Forget();
			return true;
		}

		private async UniTask DisplayPlacesInfoPanelAsync()
		{
			if (currentMarker == null)
				return;
			placesCts = placesCts.SafeRestart();
			PlacesData.PlaceInfo? placeInfo = await placesAPIService.GetPlaceAsync(currentMarker.Value.Position, placesCts.Token) 
			                                  ?? new PlacesData.PlaceInfo(currentMarker.Value.Position);
			if (placesCts.IsCancellationRequested)
				return;
			navmapBus.SelectPlaceAsync(placeInfo, placesCts.Token, true).Forget();
		}
	}
}