using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.MapRenderer;
using DCL.MapRenderer.CommonBehavior;
using DCL.MapRenderer.ConsumerUtils;
using DCL.MapRenderer.MapCameraController;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.Pins;
using DCL.MapRenderer.MapLayers.PlayerMarker;
using DCL.Navmap.FilterPanel;
using DCL.PlacesAPIService;
using DCL.UI;
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
        private const string WORLDS_WARNING_MESSAGE = "This is the Genesis City map. If you jump into any of this places you will leave the world you are currently visiting.";
        private const MapLayer ACTIVE_MAP_LAYERS =
            MapLayer.SatelliteAtlas | MapLayer.ParcelsAtlas | MapLayer.PlayerMarker | MapLayer.ParcelHoverHighlight | MapLayer.ScenesOfInterest | MapLayer.Favorites | MapLayer.HotUsersMarkers | MapLayer.Pins | MapLayer.SearchResults | MapLayer.LiveEvents |
            MapLayer.Category;

        private readonly NavmapView navmapView;
        private readonly IMapRenderer mapRenderer;
        private readonly NavmapZoomController zoomController;
        private readonly NavmapSearchBarController searchBarController;
        private readonly RectTransform rectTransform;
        private readonly SatelliteController satelliteController;
        private readonly PlaceInfoToastController placeToastController;
        private readonly IPlacesAPIService placesAPIService;
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
            PlaceInfoToastController placeToastController,
            IPlacesAPIService placesAPIService)
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
            this.placeToastController = placeToastController;
            this.placesAPIService = placesAPIService;
            mapPathEventBus.OnRemovedDestination += RemoveDestination;

            this.navmapView.SatelliteRenderImage.ParcelClicked += OnParcelClicked;
            this.navmapView.SatelliteRenderImage.HoveredParcel += OnParcelHovered;

            this.navmapView.SatelliteRenderImage.EmbedMapCameraDragBehavior(this.navmapView.MapCameraDragBehaviorData);
            lastParcelHovered = Vector2.zero;

            navmapView.DestinationInfoElement.gameObject.SetActive(false);

            navmapView.WorldsWarningNotificationView.Text.text = WORLDS_WARNING_MESSAGE;
            navmapView.WorldsWarningNotificationView.Hide();
            navmapFilterPanelController = new (mapRenderer, navmapView.LocationView.FiltersPanel);
            navmapLocationController = new NavmapLocationController(navmapView.LocationView, world, playerEntity, navmapFilterPanelController, navmapBus);
        }

        public void Dispose()
        {
            navmapView.SatelliteRenderImage.ParcelClicked -= OnParcelClicked;
            navmapView.SatelliteRenderImage.HoveredParcel -= OnParcelHovered;

            animationCts?.Dispose();
            zoomController.Dispose();
            searchBarController.Dispose();
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

            async UniTaskVoid FetchPlaceAndShowAsync(CancellationToken ct)
            {
                PlacesData.PlaceInfo? place = await placesAPIService.GetPlaceAsync(clickedParcel.Parcel, ct, true);

                if (place == null) place = new PlacesData.PlaceInfo(clickedParcel.Parcel);

                navmapBus.SelectPlaceAsync(place, fetchPlaceAndShowCancellationToken.Token, true).Forget();
            }

            fetchPlaceAndShowCancellationToken = fetchPlaceAndShowCancellationToken.SafeRestart();
            FetchPlaceAndShowAsync(fetchPlaceAndShowCancellationToken.Token).Forget();
        }

        public void Activate()
        {
            cameraController?.Release(this);

            cameraController = mapRenderer.RentCamera(
                new MapCameraInput(
                    this,
                    ACTIVE_MAP_LAYERS,
                    Vector3.zero.ToParcel(),
                    zoomController.ResetZoomToMidValue(),
                    navmapView.SatellitePixelPerfectMapRendererTextureProvider.GetPixelPerfectTextureResolution(),
                    navmapView.zoomView.zoomVerticalRange
                ));

            mapRenderer.SetSharedLayer(MapLayer.LiveEvents, navmapFilterPanelController.IsFilterActivated(MapLayer.LiveEvents));
            mapRenderer.SetSharedLayer(MapLayer.ScenesOfInterest, navmapFilterPanelController.IsFilterActivated(MapLayer.ScenesOfInterest));
            mapRenderer.SetSharedLayer(MapLayer.Pins, navmapFilterPanelController.IsFilterActivated(MapLayer.Pins));
            mapRenderer.SetSharedLayer(MapLayer.HotUsersMarkers, navmapFilterPanelController.IsFilterActivated(MapLayer.HotUsersMarkers));
            mapRenderer.SetSharedLayer(MapLayer.SatelliteAtlas, navmapFilterPanelController.IsFilterActivated(MapLayer.SatelliteAtlas));
            mapRenderer.SetSharedLayer(MapLayer.ParcelsAtlas, navmapFilterPanelController.IsFilterActivated(MapLayer.ParcelsAtlas));

            satelliteController.InjectCameraController(cameraController);
            navmapLocationController.InjectCameraController(cameraController);
            satelliteController.Activate();
            zoomController.Activate(cameraController);
            lastParcelHovered = Vector2.zero;

            if (!navmapView.WorldsWarningNotificationView.WasEverClosed)
            {
                if (realmData is {Configured: true, ScenesAreFixed: true })
                    navmapView.WorldsWarningNotificationView.Show();
                else
                    navmapView.WorldsWarningNotificationView.Hide();
            }

            placesAndEventsPanelController.Show();
        }

        public void Deactivate()
        {
            navmapView.WorldsWarningNotificationView.Hide();
            satelliteController.Deactivate();

            mapRenderer.SetSharedLayer(MapLayer.ScenesOfInterest, false);
            zoomController.Deactivate();
            cameraController?.Release(this);
            navmapBus.ClearHistory();
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
