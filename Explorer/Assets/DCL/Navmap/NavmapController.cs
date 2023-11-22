using DCL.AssetsProvision;
using DCL.UI;
using DCLServices.MapRenderer;
using DCLServices.MapRenderer.CommonBehavior;
using DCLServices.MapRenderer.ConsumerUtils;
using DCLServices.MapRenderer.MapCameraController;
using DCLServices.MapRenderer.MapLayers;
using DCLServices.MapRenderer.MapLayers.PlayerMarker;
using DCLServices.PlacesAPIService;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Navmap
{
    public class NavmapController : IMapActivityOwner, ISection
    {
        public IReadOnlyDictionary<MapLayer, IMapLayerParameter> LayersParameters  { get; } = new Dictionary<MapLayer, IMapLayerParameter>
            { { MapLayer.PlayerMarker, new PlayerMarkerParameter {BackgroundIsActive = true} } };
        private const MapLayer ACTIVE_MAP_LAYERS =
            MapLayer.SatelliteAtlas | MapLayer.ParcelsAtlas | MapLayer.HomePoint | MapLayer.ScenesOfInterest | MapLayer.PlayerMarker | MapLayer.HotUsersMarkers | MapLayer.ColdUsersMarkers | MapLayer.ParcelHoverHighlight;

        private readonly NavmapView navmapView;
        private readonly IMapRenderer mapRenderer;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IAssetsProvisioner assetsProvisioner;
        private CancellationTokenSource animationCts;
        private IMapCameraController cameraController;
        private readonly NavmapZoomController zoomController;
        private readonly FloatingPanelController floatingPanelController;
        private readonly NavmapFilterController filterController;
        private readonly NavmapSearchBarController searchBarController;
        private readonly RectTransform rectTransform;
        private readonly SatelliteController satelliteController;
        private readonly StreetViewController streetViewController;
        private readonly Dictionary<ExploreSections, ISection> mapSections;

        public NavmapController(
            NavmapView navmapView,
            IMapRenderer mapRenderer,
            IPlacesAPIService placesAPIService,
            IAssetsProvisioner assetsProvisioner)
        {
            this.navmapView = navmapView;
            this.mapRenderer = mapRenderer;
            this.placesAPIService = placesAPIService;
            this.assetsProvisioner = assetsProvisioner;
            rectTransform = this.navmapView.transform.parent.GetComponent<RectTransform>();

            zoomController = new NavmapZoomController(navmapView.zoomView);
            floatingPanelController = new FloatingPanelController(navmapView.floatingPanelView, placesAPIService);
            filterController = new NavmapFilterController(this.navmapView.filterView);
            searchBarController = new NavmapSearchBarController(navmapView.SearchBarView, navmapView.SearchBarResultPanel, placesAPIService, assetsProvisioner);

            satelliteController = new SatelliteController(navmapView.GetComponentInChildren<SatelliteView>(), this.navmapView.MapCameraDragBehaviorData, mapRenderer);
            streetViewController = new StreetViewController(navmapView.GetComponentInChildren<StreetViewView>(), this.navmapView.MapCameraDragBehaviorData, mapRenderer);


            mapSections = new ()
            {
                { ExploreSections.Satellite, satelliteController },
                { ExploreSections.StreetView, streetViewController },
            };

            var sectionSelectorController = new SectionSelectorController(mapSections, ExploreSections.Satellite);
            foreach (var tabSelector in navmapView.TabSelectorViews)
            {
                tabSelector.TabSelectorToggle.onValueChanged.RemoveAllListeners();
                tabSelector.TabSelectorToggle.onValueChanged.AddListener(
                    (isOn) =>
                    {
                        animationCts?.Cancel();
                        animationCts?.Dispose();
                        animationCts = new CancellationTokenSource();
                        sectionSelectorController.OnTabSelectorToggleValueChangedAsync(isOn, tabSelector, animationCts.Token, false).Forget();
                    });
            }

            this.navmapView.SatelliteRenderImage.ParcelClicked += OnParcelClicked;
            this.navmapView.StreetViewRenderImage.ParcelClicked += OnParcelClicked;
            this.navmapView.SatelliteRenderImage.EmbedMapCameraDragBehavior(this.navmapView.MapCameraDragBehaviorData);
            this.navmapView.StreetViewRenderImage.EmbedMapCameraDragBehavior(this.navmapView.MapCameraDragBehaviorData);
        }

        private void OnParcelClicked(MapRenderImage.ParcelClickData clickedParcel)
        {
            floatingPanelController.HandlePanelVisibility(clickedParcel.Parcel);
        }

        public void Activate()
        {
            navmapView.TabSelectorViews[0].TabSelectorToggle.isOn = true;
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
            mapSections[ExploreSections.Satellite].Activate();
            zoomController.Activate(cameraController);
        }

        public void Deactivate()
        {
            foreach (ISection mapSectionsValue in mapSections.Values)
                mapSectionsValue.Deactivate();

            zoomController.Deactivate();
            cameraController?.Release(this);
        }

        public RectTransform GetRectTransform() =>
            rectTransform;
    }
}
