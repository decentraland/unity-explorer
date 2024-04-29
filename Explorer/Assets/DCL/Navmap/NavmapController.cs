using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.Browser;
using DCL.MapRenderer;
using DCL.MapRenderer.CommonBehavior;
using DCL.MapRenderer.ConsumerUtils;
using DCL.MapRenderer.MapCameraController;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.PlayerMarker;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.WebRequests;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Navmap
{
    public class NavmapController : IMapActivityOwner, ISection, IDisposable
    {
        public IReadOnlyDictionary<MapLayer, IMapLayerParameter> LayersParameters  { get; } = new Dictionary<MapLayer, IMapLayerParameter>
            { { MapLayer.PlayerMarker, new PlayerMarkerParameter {BackgroundIsActive = true} } };
        private const MapLayer ACTIVE_MAP_LAYERS =
            MapLayer.SatelliteAtlas | MapLayer.ParcelsAtlas | MapLayer.PlayerMarker | MapLayer.ParcelHoverHighlight | MapLayer.ScenesOfInterest | MapLayer.Favorites | MapLayer.HotUsersMarkers;

        private readonly NavmapView navmapView;
        private readonly IMapRenderer mapRenderer;
        private CancellationTokenSource animationCts;
        private IMapCameraController cameraController;
        private readonly NavmapZoomController zoomController;
        private readonly FloatingPanelController floatingPanelController;
        private readonly NavmapFilterController filterController;
        private readonly NavmapSearchBarController searchBarController;
        private readonly RectTransform rectTransform;
        private readonly SatelliteController satelliteController;
        private readonly StreetViewController streetViewController;
        private readonly NavmapLocationController navmapLocationController;

        private Vector2 lastParcelHovered;
        private readonly SectionSelectorController<NavmapSections> sectionSelectorController;
        private readonly Dictionary<NavmapSections, TabSelectorView> tabsBySections;
        private readonly Dictionary<NavmapSections, ISection> mapSections;
        private NavmapSections lastShownSection;

        public NavmapController(
            NavmapView navmapView,
            IMapRenderer mapRenderer,
            IPlacesAPIService placesAPIService,
            IWebRequestController webRequestController,
            IMVCManager mvcManager,
            IWebBrowser webBrowser,
            DCLInput dclInput,
            World world,
            Entity playerEntity,
            IRealmNavigator realmNavigator)
        {
            this.navmapView = navmapView;
            this.mapRenderer = mapRenderer;

            rectTransform = this.navmapView.transform.parent.GetComponent<RectTransform>();

            zoomController = new NavmapZoomController(navmapView.zoomView, dclInput);
            filterController = new NavmapFilterController(this.navmapView.filterView, mapRenderer, webBrowser);
            searchBarController = new NavmapSearchBarController(navmapView.SearchBarView, navmapView.SearchBarResultPanel, navmapView.HistoryRecordPanelView, placesAPIService, navmapView.floatingPanelView, webRequestController, dclInput);
            floatingPanelController = new FloatingPanelController(navmapView.floatingPanelView, placesAPIService, webRequestController, realmNavigator);

            searchBarController.OnResultClicked += OnResultClicked;
            searchBarController.OnSearchTextChanged += floatingPanelController.HidePanel;
            satelliteController = new SatelliteController(navmapView.GetComponentInChildren<SatelliteView>(), this.navmapView.MapCameraDragBehaviorData, mapRenderer, webBrowser);
            streetViewController = new StreetViewController(navmapView.GetComponentInChildren<StreetViewView>(), this.navmapView.MapCameraDragBehaviorData, mapRenderer);
            navmapLocationController = new NavmapLocationController(navmapView.LocationView, world, playerEntity);

            mapSections = new ()
            {
                { NavmapSections.Satellite, satelliteController },
                { NavmapSections.StreetView, streetViewController },
            };

            sectionSelectorController = new SectionSelectorController<NavmapSections>(mapSections, NavmapSections.Satellite);
            tabsBySections = navmapView.TabSelectorMappedViews.ToDictionary(map => map.Section, map => map.TabSelectorViews);

            foreach ((NavmapSections section, TabSelectorView? tabSelector) in tabsBySections)
            {
                tabSelector.TabSelectorToggle.onValueChanged.RemoveAllListeners();
                tabSelector.TabSelectorToggle.onValueChanged.AddListener(
                    isOn =>
                    {
                        ToggleSection(isOn, tabSelector, section, true);
                    }
                );
            }

            this.navmapView.SatelliteRenderImage.ParcelClicked += OnParcelClicked;
            this.navmapView.StreetViewRenderImage.ParcelClicked += OnParcelClicked;
            this.navmapView.StreetViewRenderImage.HoveredParcel += OnParcelHovered;
            this.navmapView.SatelliteRenderImage.HoveredParcel += OnParcelHovered;

            this.navmapView.SatelliteRenderImage.EmbedMapCameraDragBehavior(this.navmapView.MapCameraDragBehaviorData);
            this.navmapView.StreetViewRenderImage.EmbedMapCameraDragBehavior(this.navmapView.MapCameraDragBehaviorData);
            lastParcelHovered = Vector2.zero;
        }

        private void ToggleSection(bool isOn, TabSelectorView tabSelectorView, NavmapSections shownSection, bool animate)
        {
            if(isOn && animate && shownSection != lastShownSection)
                sectionSelectorController.SetAnimationState(false, tabsBySections[lastShownSection]);

            animationCts.SafeCancelAndDispose();
            animationCts = new CancellationTokenSource();
            sectionSelectorController.OnTabSelectorToggleValueChangedAsync(isOn, tabSelectorView, shownSection, animationCts.Token, animate).Forget();

            if (isOn)
                lastShownSection = shownSection;
        }

        private void OnParcelHovered(Vector2 parcel)
        {
            if (!parcel.Equals(lastParcelHovered))
            {
                lastParcelHovered = parcel;
                UIAudioEventsBus.Instance.SendPlayAudioEvent(navmapView.HoverAudio);
            }
        }

        public async UniTask InitialiseAssetsAsync(IAssetsProvisioner assetsProvisioner, CancellationToken ct) =>
            await searchBarController.InitialiseAssetsAsync(assetsProvisioner, ct);

        private void OnResultClicked(string coordinates)
        {
            VectorUtilities.TryParseVector2Int(coordinates, out Vector2Int result);
            floatingPanelController.HandlePanelVisibility(result, true);
        }

        private void OnParcelClicked(MapRenderImage.ParcelClickData clickedParcel)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(navmapView.ClickAudio);
            floatingPanelController.HandlePanelVisibility(clickedParcel.Parcel, false);
        }

        public void Activate()
        {
            cameraController = mapRenderer.RentCamera(
                new MapCameraInput(
                    this,
                    ACTIVE_MAP_LAYERS,
                    ParcelMathHelper.WorldToGridPosition(new Vector3(0,0,0)),
                    zoomController.ResetZoomToMidValue(),
                    this.navmapView.SatellitePixelPerfectMapRendererTextureProvider.GetPixelPerfectTextureResolution(),
                    navmapView.zoomView.zoomVerticalRange
                ));
            satelliteController.InjectCameraController(cameraController);
            streetViewController.InjectCameraController(cameraController);
            navmapLocationController.InjectCameraController(cameraController);
            mapSections[NavmapSections.Satellite].Activate();
            zoomController.Activate(cameraController);
            lastParcelHovered = Vector2.zero;
            foreach ((NavmapSections section, TabSelectorView? tab) in tabsBySections)
            {
                ToggleSection(section == NavmapSections.Satellite, tab, section, true);
            }
        }

        public void Deactivate()
        {
            foreach (ISection mapSectionsValue in mapSections.Values)
                mapSectionsValue.Deactivate();

            zoomController.Deactivate();
            cameraController?.Release(this);
        }

        public void Animate(int triggerId)
        {
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

        public void Dispose()
        {
            this.navmapView.SatelliteRenderImage.ParcelClicked -= OnParcelClicked;
            this.navmapView.StreetViewRenderImage.ParcelClicked -= OnParcelClicked;
            this.navmapView.StreetViewRenderImage.HoveredParcel -= OnParcelHovered;
            this.navmapView.SatelliteRenderImage.HoveredParcel -= OnParcelHovered;
            animationCts?.Dispose();
            zoomController?.Dispose();
            floatingPanelController?.Dispose();
            searchBarController?.Dispose();
        }
    }
}
