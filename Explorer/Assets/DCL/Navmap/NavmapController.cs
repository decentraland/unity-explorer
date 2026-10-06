using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.MapRenderer;
using DCL.MapRenderer.CommonBehavior;
using DCL.MapRenderer.ConsumerUtils;
using DCL.MapRenderer.MapCameraController;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.MapRenderer.MapLayers.Pins;
using DCL.MapRenderer.MapLayers.PlayerMarker;
using DCL.Navmap.FilterPanel;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.Utilities;
using ECS;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Navmap
{
    public class NavmapController : IMapActivityOwner, ISection, IDisposable
    {
        private const string EMPTY_PARCEL_NAME = "Empty parcel";
        private const MapLayer ACTIVE_MAP_LAYERS =
            MapLayer.SatelliteAtlas | MapLayer.ParcelsAtlas | MapLayer.PlayerMarker | MapLayer.ParcelHoverHighlight | MapLayer.ScenesOfInterest | MapLayer.Favorites | MapLayer.HotUsersMarkers | MapLayer.Pins | MapLayer.SearchResults | MapLayer.LiveEvents | MapLayer.Category | MapLayer.HomeMarker;

        private readonly NavmapView navmapView;
        private readonly IMapRenderer mapRenderer;
        private readonly NavmapZoomController zoomController;
        private readonly NavmapSearchBarController searchBarController;
        private readonly RectTransform rectTransform;
        private readonly SatelliteController satelliteController;
        private readonly IPlacesAPIService placesAPIService;
        private readonly UpscalingController upscalingController;
        private readonly IRealmData realmData;
        private readonly IMapPathEventBus mapPathEventBus;
        private readonly UIAudioEventsBus audioEventsBus;
        private readonly PlacesAndEventsPanelController placesAndEventsPanelController;
        private readonly StringBuilder parcelTitleStringBuilder = new ();
        private readonly NavmapLocationController navmapLocationController;
        private readonly INavmapBus navmapBus;
        private CancellationTokenSource? fetchPlaceAndShowCancellationToken = new ();

        private CancellationTokenSource? animationCts;
        private IMapCameraController? cameraController;

        private Vector2 lastParcelHovered;
        private NavmapSections lastShownSection;
        private MapRenderImage.ParcelClickData lastParcelClicked;
        private NavmapFilterPanelController navmapFilterPanelController;

        public IReadOnlyDictionary<MapLayer, IMapLayerParameter> LayersParameters { get; } = new Dictionary<MapLayer, IMapLayerParameter>
            { { MapLayer.PlayerMarker, new PlayerMarkerParameter { BackgroundIsActive = true } } };

        public NavmapController(
            NavmapView navmapView,
            IMapRenderer mapRenderer,
            IRealmData realmData,
            IMapPathEventBus mapPathEventBus,
            World world,
            Entity playerEntity,
            INavmapBus navmapBus,
            UIAudioEventsBus audioEventsBus,
            PlacesAndEventsPanelController placesAndEventsPanelController,
            NavmapSearchBarController navmapSearchBarController,
            NavmapZoomController navmapZoomController,
            SatelliteController satelliteController,
            IPlacesAPIService placesAPIService,
            HomePlaceEventBus homePlaceEventBus,
            UpscalingController upscalingController)
        {
            this.navmapView = navmapView;
            this.mapRenderer = mapRenderer;
            this.realmData = realmData;
            this.mapPathEventBus = mapPathEventBus;
            this.audioEventsBus = audioEventsBus;
            this.placesAndEventsPanelController = placesAndEventsPanelController;
            this.navmapBus = navmapBus;

            rectTransform = this.navmapView.transform.parent.GetComponent<RectTransform>();

            zoomController = navmapZoomController;
            searchBarController = navmapSearchBarController;
            navmapBus.OnDestinationSelected += SetDestination;
            this.navmapView.DestinationInfoElement.QuitButton.onClick.AddListener(OnRemoveDestinationButtonClicked);
            this.satelliteController = satelliteController;
            this.placesAPIService = placesAPIService;
            this.upscalingController = upscalingController;
            mapPathEventBus.OnRemovedDestination += RemoveDestination;

            this.navmapView.SatelliteRenderImage.ParcelClicked += OnParcelClicked;
            this.navmapView.SatelliteRenderImage.HoveredParcel += OnParcelHovered;

            this.navmapView.SatelliteRenderImage.EmbedMapCameraDragBehavior(this.navmapView.MapCameraDragBehaviorData);
            lastParcelHovered = Vector2.zero;

            navmapView.DestinationInfoElement.gameObject.SetActive(false);

            navmapView.WorldsWarningNotificationView.Hide();
            navmapFilterPanelController = new (mapRenderer, navmapView.LocationView.FiltersPanel);
            navmapLocationController = new NavmapLocationController(navmapView.LocationView, world, playerEntity, navmapFilterPanelController, navmapBus, homePlaceEventBus);
        }

        public void Dispose()
        {
            navmapView.SatelliteRenderImage.ParcelClicked -= OnParcelClicked;
            navmapView.SatelliteRenderImage.HoveredParcel -= OnParcelHovered;

            animationCts?.Dispose();
            zoomController.Dispose();
            searchBarController.Dispose();
            upscalingController.ReleaseFullRenderScale(this);
        }

        private void OnRemoveDestinationButtonClicked()
        {
            mapPathEventBus.RemoveDestination();
        }

        private void RemoveDestination()
        {
            navmapView.DestinationInfoElement.gameObject.SetActive(false);
        }

        private void SetDestination(PlacesData.PlaceInfo? placeInfo)
        {
            Vector2Int destinationParcel = placeInfo switch
                                           {
                                               { Positions: { Length: 1 } } => placeInfo.Positions[0],
                                               { Positions: { Length: > 1 } } => placeInfo.base_position_processed,
                                               _ => Vector2Int.zero,
                                           };

            IPinMarker? destinationPinMarker = destinationParcel == lastParcelClicked.Parcel ? lastParcelClicked.PinMarker : null;

            mapPathEventBus.SetDestination(destinationParcel, destinationPinMarker);
            navmapView.DestinationInfoElement.gameObject.SetActive(true);

            if (destinationPinMarker != null)
                navmapView.DestinationInfoElement.Setup(destinationPinMarker.Title, true, destinationPinMarker.CurrentSprite);
            else
            {
                parcelTitleStringBuilder.Clear();
                var parcelDescription = parcelTitleStringBuilder.Append(placeInfo != null ? placeInfo.title : EMPTY_PARCEL_NAME).Append(" ").Append(destinationParcel.ToString()).ToString();
                navmapView.DestinationInfoElement.Setup(parcelDescription, false, null);
            }
        }

        private void OnParcelHovered(Vector2 parcel)
        {
            if (parcel.Equals(lastParcelHovered)) return;
            navmapView.MapPinTooltip.Hide();
            lastParcelHovered = parcel;
        }

        private void OnParcelClicked(MapRenderImage.ParcelClickData clickedParcel)
        {
            lastParcelClicked = clickedParcel;
            audioEventsBus.SendPlayAudioEvent(navmapView.ClickAudio);

            fetchPlaceAndShowCancellationToken = fetchPlaceAndShowCancellationToken.SafeRestart();

            if (realmData.IsWorld())
            {
                // Only the world's own parcels can be jumped into; without a manifest the map only reacts over its scenes.
                if (realmData.IsParcelOfWorld(clickedParcel.Parcel.x, clickedParcel.Parcel.y))
                    navmapBus.SelectPlaceAsync(clickedParcel.Parcel, fetchPlaceAndShowCancellationToken.Token, true).Forget();

                return;
            }

            FetchPlaceAndShowAsync(fetchPlaceAndShowCancellationToken.Token).Forget();
            return;

            async UniTaskVoid FetchPlaceAndShowAsync(CancellationToken ct)
            {
                PlacesData.PlaceInfo? place = await placesAPIService.GetPlaceAsync(clickedParcel.Parcel, ct, true);

                if (place == null) place = new PlacesData.PlaceInfo(clickedParcel.Parcel);

                navmapBus.SelectPlaceAsync(place, fetchPlaceAndShowCancellationToken.Token, true, clickedParcel.Parcel).Forget();
            }
        }

        public void Activate()
        {
            cameraController?.Release(this);

            // A world's map opens zoomed out to the whole world, which its farthest zoom fits.
            bool isWorld = realmData.IsWorld();

            cameraController = mapRenderer.RentCamera(
                new MapCameraInput(
                    this,
                    ACTIVE_MAP_LAYERS,
                    Vector3.zero.ToParcel(),
                    isWorld ? zoomController.ResetZoomToFarthestValue() : zoomController.ResetZoomToMidValue(),
                    navmapView.SatellitePixelPerfectMapRendererTextureProvider.GetPixelPerfectTextureResolution(),
                    navmapView.zoomView.zoomVerticalRange
                ));

            mapRenderer.SetSharedLayer(MapLayer.LiveEvents, navmapFilterPanelController.IsFilterActivated(MapLayer.LiveEvents));
            mapRenderer.SetSharedLayer(MapLayer.ScenesOfInterest, navmapFilterPanelController.IsFilterActivated(MapLayer.ScenesOfInterest));
            mapRenderer.SetSharedLayer(MapLayer.Pins, navmapFilterPanelController.IsFilterActivated(MapLayer.Pins));
            mapRenderer.SetSharedLayer(MapLayer.HotUsersMarkers, navmapFilterPanelController.IsFilterActivated(MapLayer.HotUsersMarkers));
            // A world only has a satellite map.
            mapRenderer.SetSharedLayer(MapLayer.SatelliteAtlas, isWorld || navmapFilterPanelController.IsFilterActivated(MapLayer.SatelliteAtlas));
            mapRenderer.SetSharedLayer(MapLayer.ParcelsAtlas, navmapFilterPanelController.IsFilterActivated(MapLayer.ParcelsAtlas));
            navmapFilterPanelController.SetMapTypeSelectable(!isWorld);
            satelliteController.SetGenesisCityCreditsVisible(!isWorld);

            satelliteController.InjectCameraController(cameraController);
            navmapLocationController.InjectCameraController(cameraController);

            if (isWorld)
                cameraController.CenterOnMap();

            satelliteController.Activate();
            zoomController.Activate(cameraController);
            lastParcelHovered = Vector2.zero;
            navmapView.gameObject.SetActive(true);

            // A world's map only lists its own parcels: Genesis City's categories and search don't apply there.
            navmapView.SetPlacesSearchVisible(!isWorld);
            placesAndEventsPanelController.Show();

            // A parcel clicked in a world opens its card; until then the panel of Genesis City's places stays closed.
            if (isWorld)
                placesAndEventsPanelController.Close();

            // The map renders into a RenderTexture, so the user's render scale would pixelate it.
            upscalingController.RequireFullRenderScale(this);
        }

        public void Deactivate()
        {
            navmapView.WorldsWarningNotificationView.Hide();
            satelliteController.Deactivate();

            mapRenderer.SetSharedLayer(MapLayer.ScenesOfInterest, false);
            zoomController.Deactivate();
            cameraController?.Release(this);
            navmapBus.ClearHistory();
            searchBarController.ClearInput();
            navmapView.gameObject.SetActive(false);
            upscalingController.ReleaseFullRenderScale(this);
        }

        public void Animate(int triggerId)
        {
            navmapView.PanelAnimator.SetTrigger(triggerId);
        }

        public void ResetAnimator()
        {
            navmapView.PanelAnimator.Rebind();
            navmapView.PanelAnimator.Update(0);
        }

        public RectTransform GetRectTransform() =>
            rectTransform;
    }
}
