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
        private readonly IPlacesAPIService placesAPIService;
        private readonly IAssetsProvisioner assetsProvisioner;
        private CancellationTokenSource animationCts;
        private readonly IMapCameraController cameraController;
        private readonly NavmapZoomController zoomController;
        private readonly FloatingPanelController floatingPanelController;
        private readonly NavmapFilterController filterController;
        private readonly NavmapSearchBarController searchBarController;
        private RectTransform rectTransform;

        public NavmapController(
            NavmapView navmapView,
            IMapRenderer mapRenderer,
            IPlacesAPIService placesAPIService,
            IAssetsProvisioner assetsProvisioner)
        {
            this.navmapView = navmapView;
            this.placesAPIService = placesAPIService;
            this.assetsProvisioner = assetsProvisioner;
            rectTransform = this.navmapView.transform.parent.GetComponent<RectTransform>();

            zoomController = new NavmapZoomController(navmapView.zoomView);
            floatingPanelController = new FloatingPanelController(navmapView.floatingPanelView, placesAPIService);
            filterController = new NavmapFilterController(this.navmapView.filterView);
            searchBarController = new NavmapSearchBarController(navmapView.SearchBarView, navmapView.SearchBarResultPanel, placesAPIService, assetsProvisioner);

            cameraController = mapRenderer.RentCamera(
                new MapCameraInput(
                    this,
                    ACTIVE_MAP_LAYERS,
                    ParcelMathHelper.WorldToGridPosition(new Vector3(0,0,0)),
                    zoomController.ResetZoomToMidValue(),
                    this.navmapView.SatellitePixelPerfectMapRendererTextureProvider.GetPixelPerfectTextureResolution(),
                    navmapView.zoomView.zoomVerticalRange
                ));

            SatelliteController satelliteController = new SatelliteController(navmapView.GetComponentInChildren<SatelliteView>(), this.navmapView.MapCameraDragBehaviorData, cameraController, mapRenderer);
            StreetViewController streetViewController = new StreetViewController(navmapView.GetComponentInChildren<StreetViewView>(), this.navmapView.MapCameraDragBehaviorData, cameraController, mapRenderer);
            Dictionary<ExploreSections, ISection> mapSections = new ()
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
            mapSections[ExploreSections.Satellite].Activate();

            this.navmapView.SatelliteRenderImage.ParcelClicked += OnParcelClicked;
            this.navmapView.StreetViewRenderImage.ParcelClicked += OnParcelClicked;
            this.navmapView.SatelliteRenderImage.EmbedMapCameraDragBehavior(this.navmapView.MapCameraDragBehaviorData);
            this.navmapView.StreetViewRenderImage.EmbedMapCameraDragBehavior(this.navmapView.MapCameraDragBehaviorData);
            this.navmapView.SatelliteRenderImage.texture = cameraController.GetRenderTexture();
            this.navmapView.StreetViewRenderImage.texture = cameraController.GetRenderTexture();
        }

        private void OnParcelClicked(MapRenderImage.ParcelClickData clickedParcel)
        {
            floatingPanelController.ShowPanel(clickedParcel.Parcel);
        }

        public void Activate()
        {
            navmapView.StreetViewRenderImage.Activate(null, cameraController.GetRenderTexture(), cameraController);
            navmapView.SatelliteRenderImage.Activate(null, cameraController.GetRenderTexture(), cameraController);
            navmapView.gameObject.SetActive(true);
            zoomController.Activate(cameraController);
        }

        public void Deactivate()
        {
            navmapView.StreetViewRenderImage.Deactivate();
            navmapView.SatelliteRenderImage.Deactivate();
            zoomController.Deactivate();
            navmapView.gameObject.SetActive(false);
        }

        public RectTransform GetRectTransform() =>
            rectTransform;
    }
}
