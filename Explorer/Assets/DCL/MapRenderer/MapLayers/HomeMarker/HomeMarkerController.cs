using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.Navmap;
using DCL.PlacesAPIService;
using DCL.Prefs;
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
		private readonly IEventBus analyticsEventBus;

		public Vector2Int? CurrentCoordinates { get; private set; }
		private IHomeMarker homeMarker;
		private CancellationTokenSource highlightCt = new();
		private CancellationTokenSource deHighlightCt = new();
		private CancellationTokenSource placesCts = new();

		public bool HomeIsSet => CurrentCoordinates.HasValue;
		public bool ZoomBlocked { get; set; }
		
		internal HomeMarkerController(
			HomeMarkerBuilder builder,
			Transform instantiationParent, 
			ICoordsUtils coordsUtils, 
			IMapCullingController cullingController,
			INavmapBus navmapBus,
			IPlacesAPIService placesAPIService,
			IEventBus analyticsEventBus) 
			: base(instantiationParent, coordsUtils, cullingController)
		{
			this.builder = builder;
			this.navmapBus = navmapBus;
			this.placesAPIService = placesAPIService;
			this.analyticsEventBus = analyticsEventBus;
		}

		internal void Initialize()
		{
			homeMarker = builder(instantiationParent);
			SetMarker(Deserialize());
		}

		protected override void DisposeImpl()
		{
			highlightCt.SafeCancelAndDispose();
			deHighlightCt.SafeCancelAndDispose();
			placesCts.SafeCancelAndDispose();
			homeMarker.Dispose();
		}
		
		public static Vector2Int? Deserialize()
		{
			if (!HasSerializedPosition())
				return null;
			
			return DCLPlayerPrefs.GetVector2Int(DCLPrefKeys.MAP_HOME_MARKER_DATA, Vector2Int.zero);
		}

		internal static void Serialize(Vector2Int? coordinates)
		{
			if (!coordinates.HasValue)
			{
				DCLPlayerPrefs.DeleteVector2Key(DCLPrefKeys.MAP_HOME_MARKER_DATA);
				return;
			}

			DCLPlayerPrefs.SetVector2Int(DCLPrefKeys.MAP_HOME_MARKER_DATA, coordinates.Value);
		}

		public static bool HasSerializedPosition() => DCLPlayerPrefs.HasVectorKey(DCLPrefKeys.MAP_HOME_MARKER_DATA);

		public void SetMarker(Vector2Int? coordinates)
		{
			homeMarker.SetActive(coordinates.HasValue);
			CurrentCoordinates = coordinates;

			if (CurrentCoordinates.HasValue)
				homeMarker.SetPosition(coordsUtils.CoordsToPositionWithOffset(CurrentCoordinates.Value));
			
			Serialize(CurrentCoordinates);
			analyticsEventBus.Publish(new HomeMarkerEvents.MessageHomePositionChanged(CurrentCoordinates));
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
			if (!CurrentCoordinates.HasValue)
				return;

			try
			{
				placesCts = placesCts.SafeRestart();
				PlacesData.PlaceInfo? placeInfo = await placesAPIService.GetPlaceAsync(CurrentCoordinates.Value, placesCts.Token) 
				                                  ?? new PlacesData.PlaceInfo(CurrentCoordinates.Value);
				if (placesCts.IsCancellationRequested)
					return;
				navmapBus.SelectPlaceAsync(placeInfo, placesCts.Token, true, CurrentCoordinates.Value).Forget();
			}
			catch (OperationCanceledException _) { }
			catch (Exception e)
			{
				ReportHub.LogError(ReportCategory.UNSPECIFIED, "HomeMarkerController: Error while fetching place info" + e);
			}
			
		}
	}
}