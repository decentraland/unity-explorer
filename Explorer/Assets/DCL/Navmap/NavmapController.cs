using DCL.UI;
using DCLServices.MapRenderer;
using DCLServices.MapRenderer.CommonBehavior;
using DCLServices.MapRenderer.ConsumerUtils;
using DCLServices.MapRenderer.MapCameraController;
using DCLServices.MapRenderer.MapLayers;
using DCLServices.MapRenderer.MapLayers.PlayerMarker;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Navmap
{
    public class NavmapController : IMapActivityOwner
    {
        public IReadOnlyDictionary<MapLayer, IMapLayerParameter> LayersParameters  { get; } = new Dictionary<MapLayer, IMapLayerParameter>
            { { MapLayer.PlayerMarker, new PlayerMarkerParameter {BackgroundIsActive = true} } };
        private const MapLayer ACTIVE_MAP_LAYERS =
            MapLayer.SatelliteAtlas | MapLayer.ParcelsAtlas | MapLayer.HomePoint | MapLayer.ScenesOfInterest | MapLayer.PlayerMarker | MapLayer.HotUsersMarkers | MapLayer.ColdUsersMarkers | MapLayer.ParcelHoverHighlight;

        private readonly NavmapView navmapView;
        private readonly SectionSelectorController sectionSelectorController;
        private CancellationTokenSource animationCts;
        private readonly IMapCameraController cameraController;
        private readonly NavmapZoomController zoomController;
        private readonly FloatingPanelController floatingPanelController;
        private readonly NavmapFilterController filterController;

        public NavmapController(NavmapView navmapView, IMapRenderer mapRenderer)
        {
            this.navmapView = navmapView;
            zoomController = new NavmapZoomController(navmapView.zoomView);
            floatingPanelController = new FloatingPanelController(navmapView.floatingPanelView);
            filterController = new NavmapFilterController(this.navmapView.filterView);
            Dictionary<ExploreSections, GameObject> mapSections = new ()
            {
                { ExploreSections.Satellite, navmapView.satellite },
                { ExploreSections.StreetView, navmapView.streetView },
            };

            sectionSelectorController = new SectionSelectorController(mapSections, ExploreSections.Satellite);
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

                        if (isOn)
                        {
                            mapRenderer.SetSharedLayer(MapLayer.ParcelsAtlas, tabSelector.section == ExploreSections.StreetView);
                            mapRenderer.SetSharedLayer(MapLayer.SatelliteAtlas, tabSelector.section == ExploreSections.Satellite);
                        }

                    });
            }
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

            this.navmapView.SatelliteRenderImage.EmbedMapCameraDragBehavior(this.navmapView.MapCameraDragBehaviorData);
            this.navmapView.SatelliteRenderImage.ParcelClicked += OnParcelClicked;
            this.navmapView.StreetViewRenderImage.EmbedMapCameraDragBehavior(this.navmapView.MapCameraDragBehaviorData);
            this.navmapView.StreetViewRenderImage.ParcelClicked += OnParcelClicked;
            this.navmapView.SatelliteRenderImage.texture = cameraController.GetRenderTexture();
            this.navmapView.StreetViewRenderImage.texture = cameraController.GetRenderTexture();
            Activate();
        }

        private void OnParcelClicked(MapRenderImage.ParcelClickData obj)
        {
            floatingPanelController.ShowPanel();
        }

        private void Activate()
        {
            navmapView.SatellitePixelPerfectMapRendererTextureProvider.Activate(cameraController);
            navmapView.StreetViewPixelPerfectMapRendererTextureProvider.Activate(cameraController);
            navmapView.StreetViewRenderImage.Activate(null, cameraController.GetRenderTexture(), cameraController);
            navmapView.SatelliteRenderImage.Activate(null, cameraController.GetRenderTexture(), cameraController);
            zoomController.Activate(cameraController);
        }

        private void Deactivate()
        {
            navmapView.SatellitePixelPerfectMapRendererTextureProvider.Deactivate();
            navmapView.StreetViewPixelPerfectMapRendererTextureProvider.Deactivate();
            zoomController.Deactivate();
        }
    }
}
