using Arch.Core;
using DCL.Audio;
using DCL.Browser;
using DCL.Chat.MessageBus;
using DCL.Input;
using DCL.MapRenderer;
using DCL.MapRenderer.CommonBehavior;
using DCL.MapRenderer.ConsumerUtils;
using DCL.MapRenderer.MapCameraController;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.Pins;
using DCL.MapRenderer.MapLayers.PlayerMarker;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.WebRequests;
using ECS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using Utility;

namespace DCL.Navmap
{
    public class NavmapController : IMapActivityOwner, ISection, IDisposable
    {
        private const string EMPTY_PARCEL_NAME = "Empty parcel";
        private const string WORLDS_WARNING_MESSAGE = "This is the Genesis City map. If you jump into any of this places you will leave the world you are currently visiting.";
        private const MapLayer ACTIVE_MAP_LAYERS =
            MapLayer.SatelliteAtlas | MapLayer.PlayerMarker | MapLayer.ParcelHoverHighlight | MapLayer.ScenesOfInterest | MapLayer.Favorites | MapLayer.HotUsersMarkers | MapLayer.Pins;

        private readonly NavmapView navmapView;
        private readonly IMapRenderer mapRenderer;
        private readonly NavmapZoomController zoomController;
        private readonly NavmapFilterController filterController;
        private readonly NavmapSearchBarController searchBarController;
        private readonly RectTransform rectTransform;
        private readonly SatelliteController satelliteController;
        private readonly IRealmData realmData;
        private readonly IMapPathEventBus mapPathEventBus;
        private readonly UIAudioEventsBus audioEventsBus;
        private readonly SectionSelectorController<NavmapSections> sectionSelectorController;
        private readonly Dictionary<NavmapSections, TabSelectorView> tabsBySections;
        private readonly Dictionary<NavmapSections, ISection> mapSections;
        private readonly Mouse mouse;
        private readonly StringBuilder parcelTitleStringBuilder = new ();
        private readonly NavmapLocationController navmapLocationController;
        private readonly EventInfoCardController eventInfoCardController;

        private CancellationTokenSource? animationCts;
        private IMapCameraController? cameraController;

        private Vector2 lastParcelHovered;
        private NavmapSections lastShownSection;
        private MapRenderImage.ParcelClickData lastParcelClicked;

        public IReadOnlyDictionary<MapLayer, IMapLayerParameter> LayersParameters { get; } = new Dictionary<MapLayer, IMapLayerParameter>
            { { MapLayer.PlayerMarker, new PlayerMarkerParameter { BackgroundIsActive = true } } };

        public INavmapBus NavmapBus { get; }

        public NavmapController(
            NavmapView navmapView,
            IMapRenderer mapRenderer,
            IPlacesAPIService placesAPIService,
            IWebRequestController webRequestController,
            IWebBrowser webBrowser,
            DCLInput dclInput,
            IRealmData realmData,
            IMapPathEventBus mapPathEventBus,
            World world,
            Entity playerEntity,
            IInputBlock inputBlock,
            IChatMessagesBus chatMessagesBus,
            ISearchHistory searchHistory,
            INavmapBus navmapBus,
            UIAudioEventsBus audioEventsBus)
        {
            this.navmapView = navmapView;
            this.mapRenderer = mapRenderer;
            this.realmData = realmData;
            this.mapPathEventBus = mapPathEventBus;
            this.audioEventsBus = audioEventsBus;
            this.NavmapBus = navmapBus;

            rectTransform = this.navmapView.transform.parent.GetComponent<RectTransform>();

            zoomController = new NavmapZoomController(navmapView.zoomView, dclInput);
            filterController = new NavmapFilterController(this.navmapView.filterView, mapRenderer, webBrowser);
            searchBarController = new NavmapSearchBarController(navmapView.SearchBarView,
                navmapView.HistoryRecordPanelView, inputBlock, searchHistory, navmapBus);
            eventInfoCardController = new EventInfoCardController(navmapView.eventInfoCardView, placesAPIService,
                webRequestController, mapPathEventBus, chatMessagesBus, zoomController, navmapBus);
            navmapBus.OnDestinationSelected += SetDestination;
            this.navmapView.DestinationInfoElement.QuitButton.onClick.AddListener(OnRemoveDestinationButtonClicked);
            navmapBus.OnPlaceSelected += OnPlaceSelected;
            satelliteController = new SatelliteController(navmapView.GetComponentInChildren<SatelliteView>(), this.navmapView.MapCameraDragBehaviorData, mapRenderer, webBrowser);
            mapPathEventBus.OnRemovedDestination += RemoveDestination;

            mapSections = new Dictionary<NavmapSections, ISection>
            {
                { NavmapSections.Satellite, satelliteController },
            };

            sectionSelectorController = new SectionSelectorController<NavmapSections>(mapSections, NavmapSections.Satellite);
            tabsBySections = navmapView.TabSelectorMappedViews.ToDictionary(map => map.Section, map => map.TabSelectorViews);

            foreach ((NavmapSections section, TabSelectorView? tabSelector) in tabsBySections)
            {
                tabSelector.TabSelectorToggle.onValueChanged.RemoveAllListeners();

                tabSelector.TabSelectorToggle.onValueChanged.AddListener(
                    isOn => { ToggleSection(isOn, tabSelector, section, true); }
                );
            }

            this.navmapView.SatelliteRenderImage.ParcelClicked += OnParcelClicked;
            this.navmapView.SatelliteRenderImage.HoveredMapPin += OnMapPinHovered;
            this.navmapView.SatelliteRenderImage.HoveredParcel += OnParcelHovered;

            this.navmapView.SatelliteRenderImage.EmbedMapCameraDragBehavior(this.navmapView.MapCameraDragBehaviorData);
            lastParcelHovered = Vector2.zero;

            navmapView.DestinationInfoElement.gameObject.SetActive(false);

            navmapView.WorldsWarningNotificationView.SetText(WORLDS_WARNING_MESSAGE);
            navmapView.WorldsWarningNotificationView.Hide();
            mouse = InputSystem.GetDevice<Mouse>();

            navmapLocationController = new NavmapLocationController(navmapView.LocationView, world, playerEntity);
        }

        public void Dispose()
        {
            navmapView.SatelliteRenderImage.ParcelClicked -= OnParcelClicked;
            navmapView.SatelliteRenderImage.HoveredParcel -= OnParcelHovered;
            navmapView.SatelliteRenderImage.HoveredMapPin -= OnMapPinHovered;
            animationCts?.Dispose();
            zoomController.Dispose();
            eventInfoCardController.Dispose();
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
            mapPathEventBus.SetDestination(lastParcelClicked.Parcel, lastParcelClicked.PinMarker);
            navmapView.DestinationInfoElement.gameObject.SetActive(true);

            if (lastParcelClicked.PinMarker != null) { navmapView.DestinationInfoElement.Setup(lastParcelClicked.PinMarker.Title, true, lastParcelClicked.PinMarker.CurrentSprite); }
            else
            {
                parcelTitleStringBuilder.Clear();
                var parcelDescription = parcelTitleStringBuilder.Append(placeInfo != null ? placeInfo.title : EMPTY_PARCEL_NAME).Append(" ").Append(lastParcelClicked.Parcel.ToString()).ToString();
                navmapView.DestinationInfoElement.Setup(parcelDescription, false, null);
            }
        }

        private void OnMapPinHovered(Vector2Int parcel, IPinMarker pinMarker)
        {
            navmapView.MapPinTooltip.RectTransform.position = mouse.position.value;
            navmapView.MapPinTooltip.Title.text = pinMarker.Title;
            navmapView.MapPinTooltip.Description.text = pinMarker.Description;
            navmapView.MapPinTooltip.Show();
        }

        private void ToggleSection(bool isOn, TabSelectorView tabSelectorView, NavmapSections shownSection, bool animate)
        {
            if (isOn && animate && shownSection != lastShownSection)
                sectionSelectorController.SetAnimationState(false, tabsBySections[lastShownSection]);

            animationCts.SafeCancelAndDispose();
            animationCts = new CancellationTokenSource();
            sectionSelectorController.OnTabSelectorToggleValueChangedAsync(isOn, tabSelectorView, shownSection, animationCts.Token, animate).Forget();

            if (isOn)
                lastShownSection = shownSection;
        }

        private void OnParcelHovered(Vector2 parcel)
        {
            if (parcel.Equals(lastParcelHovered)) return;
            navmapView.MapPinTooltip.Hide();
            lastParcelHovered = parcel;
            audioEventsBus.SendPlayAudioEvent(navmapView.HoverAudio);
        }

        private void OnPlaceSelected(PlacesData.PlaceInfo place)
        {
            if (VectorUtilities.TryParseVector2Int(place.base_position, out Vector2Int result))
                //This will trigger a "parcel clicked" event with the data from the parcel
                this.navmapView.SatelliteRenderImage.OnSearchResultParcelSelected(result);
        }

        private void OnParcelClicked(MapRenderImage.ParcelClickData clickedParcel)
        {
            lastParcelClicked = clickedParcel;
            audioEventsBus.SendPlayAudioEvent(navmapView.ClickAudio);
            eventInfoCardController.Show(clickedParcel.Parcel, clickedParcel.PinMarker);
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

            satelliteController.InjectCameraController(cameraController);
            navmapLocationController.InjectCameraController(cameraController);
            mapSections[NavmapSections.Satellite].Activate();
            zoomController.Activate(cameraController);
            lastParcelHovered = Vector2.zero;

            foreach ((NavmapSections section, TabSelectorView? tab) in tabsBySections) { ToggleSection(section == NavmapSections.Satellite, tab, section, true); }

            sectionSelectorController.SetAnimationState(true, tabsBySections[NavmapSections.Satellite]);

            if (!navmapView.WorldsWarningNotificationView.WasEverClosed)
            {
                if (realmData is {Configured: true, ScenesAreFixed: true })
                    navmapView.WorldsWarningNotificationView.Show();
                else
                    navmapView.WorldsWarningNotificationView.Hide();
            }
        }

        public void Deactivate()
        {
            filterController.CloseFilterContent();
            navmapView.WorldsWarningNotificationView.Hide();

            foreach (ISection mapSectionsValue in mapSections.Values)
                mapSectionsValue.Deactivate();

            zoomController.Deactivate();
            cameraController?.Release(this);
            NavmapBus.ClearHistory();
        }

        public void Animate(int triggerId)
        {
            filterController.CloseFilterContent();
            navmapView.PanelAnimator.SetTrigger(triggerId);
            navmapView.HeaderAnimator.SetTrigger(triggerId);
        }

        public void ResetAnimator()
        {
            navmapView.PanelAnimator.Rebind();
            navmapView.HeaderAnimator.Rebind();
            navmapView.PanelAnimator.Update(0);
            navmapView.HeaderAnimator.Update(0);
        }

        public RectTransform GetRectTransform() =>
            rectTransform;
    }
}
