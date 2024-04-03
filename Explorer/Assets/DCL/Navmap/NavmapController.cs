using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.MapRenderer;
using DCL.MapRenderer.CommonBehavior;
using DCL.MapRenderer.ConsumerUtils;
using DCL.MapRenderer.MapCameraController;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.PlayerMarker;
using DCL.ParcelsService;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.WebRequests;
using MVC;
using System;
using System.Collections.Generic;
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
            MapLayer.SatelliteAtlas | MapLayer.ParcelsAtlas | MapLayer.PlayerMarker | MapLayer.ParcelHoverHighlight | MapLayer.ScenesOfInterest | MapLayer.Favorites;

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
        private readonly Dictionary<NavmapSections, ISection> mapSections;
        private readonly NavmapLocationController navmapLocationController;

        public NavmapController(
            NavmapView navmapView,
            IMapRenderer mapRenderer,
            IPlacesAPIService placesAPIService,
            ITeleportController teleportController,
            IWebRequestController webRequestController,
            IMVCManager mvcManager,
            DCLInput dclInput,
            World world,
            Entity playerEntity)
        {
            this.navmapView = navmapView;
            this.mapRenderer = mapRenderer;

            rectTransform = this.navmapView.transform.parent.GetComponent<RectTransform>();

            zoomController = new NavmapZoomController(navmapView.zoomView, dclInput);
            floatingPanelController = new FloatingPanelController(navmapView.floatingPanelView, placesAPIService, teleportController, webRequestController, mvcManager);
            filterController = new NavmapFilterController(this.navmapView.filterView, mapRenderer);
            searchBarController = new NavmapSearchBarController(navmapView.SearchBarView, navmapView.SearchBarResultPanel, placesAPIService, navmapView.floatingPanelView, webRequestController);
            searchBarController.OnResultClicked += OnResultClicked;
            satelliteController = new SatelliteController(navmapView.GetComponentInChildren<SatelliteView>(), this.navmapView.MapCameraDragBehaviorData, mapRenderer);
            streetViewController = new StreetViewController(navmapView.GetComponentInChildren<StreetViewView>(), this.navmapView.MapCameraDragBehaviorData, mapRenderer);
            navmapLocationController = new NavmapLocationController(navmapView.LocationView, world, playerEntity);

            mapSections = new ()
            {
                { NavmapSections.Satellite, satelliteController },
                { NavmapSections.StreetView, streetViewController },
            };

            var sectionSelectorController = new SectionSelectorController<NavmapSections>(mapSections, NavmapSections.Satellite);
            foreach (var tabSelector in navmapView.TabSelectorMappedViews)
            {
                tabSelector.TabSelectorViews.TabSelectorToggle.onValueChanged.RemoveAllListeners();
                tabSelector.TabSelectorViews.TabSelectorToggle.onValueChanged.AddListener(
                    (isOn) =>
                    {
                        animationCts.SafeCancelAndDispose();
                        animationCts = new CancellationTokenSource();
                        sectionSelectorController.OnTabSelectorToggleValueChangedAsync(isOn, tabSelector.TabSelectorViews, tabSelector.Section, animationCts.Token, false).Forget();
                    });
            }

            this.navmapView.SatelliteRenderImage.ParcelClicked += OnParcelClicked;
            this.navmapView.StreetViewRenderImage.ParcelClicked += OnParcelClicked;
            this.navmapView.SatelliteRenderImage.EmbedMapCameraDragBehavior(this.navmapView.MapCameraDragBehaviorData);
            this.navmapView.StreetViewRenderImage.EmbedMapCameraDragBehavior(this.navmapView.MapCameraDragBehaviorData);
        }

        public async UniTask InitialiseAssetsAsync(IAssetsProvisioner assetsProvisioner, CancellationToken ct) =>
            await searchBarController.InitialiseAssetsAsync(assetsProvisioner, ct);

        private void OnResultClicked(string coordinates)
        {
            VectorUtilities.TryParseVector2Int(coordinates, out Vector2Int result);
            floatingPanelController.HandlePanelVisibility(result, false);
        }

        private void OnParcelClicked(MapRenderImage.ParcelClickData clickedParcel)
        {
            floatingPanelController.HandlePanelVisibility(clickedParcel.Parcel);
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

        public void Dispose()
        {
            animationCts?.Dispose();
            zoomController?.Dispose();
            floatingPanelController?.Dispose();
            searchBarController?.Dispose();
        }
    }
}
