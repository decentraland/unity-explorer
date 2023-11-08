using DCL.UI;
using DCLServices.MapRenderer;
using DCLServices.MapRenderer.CommonBehavior;
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

        private NavmapView navmapView;
        private SectionSelectorController sectionSelectorController;
        private CancellationTokenSource animationCts;
        private IMapCameraController cameraController;
        private IMapRenderer mapRenderer;

        public NavmapController(NavmapView navmapView, IMapRenderer mapRenderer)
        {
            this.navmapView = navmapView;
            this.mapRenderer = mapRenderer;
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
                    ParcelMathHelper.WorldToGridPosition(new Vector3(0,0,0)),//DataStore.i.player.playerWorldPosition.Get()),
                    5f,//navmapZoomViewController.ResetZoomToMidValue(),
                    this.navmapView.SatellitePixelPerfectMapRendererTextureProvider.GetPixelPerfectTextureResolution(),
                    new Vector2Int(100,100)//zoomView.zoomVerticalRange
                ));

            this.navmapView.SatelliteRenderImage.texture = cameraController.GetRenderTexture();
            this.navmapView.SatellitePixelPerfectMapRendererTextureProvider.Activate(cameraController);
            this.navmapView.StreetViewRenderImage.texture = cameraController.GetRenderTexture();
            this.navmapView.StreetViewPixelPerfectMapRendererTextureProvider.Activate(cameraController);
        }

    }
}
