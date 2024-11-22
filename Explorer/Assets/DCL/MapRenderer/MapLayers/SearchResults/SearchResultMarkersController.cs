using Cysharp.Threading.Tasks;
using DCL.MapRenderer.Culling;
using DCL.Navmap;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using ICoordsUtils = DCL.MapRenderer.CoordsUtils.ICoordsUtils;
using IPlacesAPIService = DCL.PlacesAPIService.IPlacesAPIService;
using PlacesData = DCL.PlacesAPIService.PlacesData;

namespace DCL.MapRenderer.MapLayers.SearchResults
{
    internal class SearchResultMarkersController : MapLayerControllerBase, IMapCullingListener<ISearchResultMarker>, IMapLayerController, IZoomScalingLayer
    {
        private const string EMPTY_PARCEL_NAME = "Empty parcel";

        internal delegate ISearchResultMarker SearchResultsMarkerBuilder(
            IObjectPool<SearchResultMarkerObject> objectsPool,
            IMapCullingController cullingController,
            ICoordsUtils coordsUtils);

        private readonly IObjectPool<SearchResultMarkerObject> objectsPool;
        private readonly SearchResultsMarkerBuilder builder;
        private readonly INavmapBus navmapBus;

        private readonly Dictionary<Vector2Int, ISearchResultMarker> markers = new();

        private Vector2Int decodePointer;
        private bool isEnabled;

        public SearchResultMarkersController(
            IObjectPool<SearchResultMarkerObject> objectsPool,
            SearchResultsMarkerBuilder builder,
            Transform instantiationParent,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            INavmapBus navmapBus)
            : base(instantiationParent, coordsUtils, cullingController)
        {
            this.objectsPool = objectsPool;
            this.builder = builder;
            this.navmapBus = navmapBus;
            navmapBus.OnPlaceSearched += OnPlaceSearched;
        }

        public async UniTask InitializeAsync(CancellationToken cancellationToken) { }

        private void OnPlaceSearched(IReadOnlyList<PlacesData.PlaceInfo> searchedPlaces)
        {
            foreach ((Vector2Int key, ISearchResultMarker? marker) in markers)
            {
                mapCullingController.StopTracking(marker);
                marker.OnBecameInvisible();
                markers.Remove(key);
            }

            foreach (PlacesData.PlaceInfo placeInfo in searchedPlaces)
            {
                if (markers.ContainsKey(MapLayerUtils.GetParcelsCenter(placeInfo)))
                    continue;

                if (IsEmptyParcel(placeInfo))
                    continue;

                var marker = builder(objectsPool, mapCullingController, coordsUtils);
                var centerParcel = MapLayerUtils.GetParcelsCenter(placeInfo);
                var position = coordsUtils.CoordsToPosition(centerParcel);

                marker.SetData(placeInfo.title, position);
                markers.Add(MapLayerUtils.GetParcelsCenter(placeInfo), marker);

                if (isEnabled)
                    mapCullingController.StartTracking(marker, this);
            }
        }

        private static bool IsEmptyParcel(PlacesData.PlaceInfo sceneInfo) =>
            sceneInfo.title == EMPTY_PARCEL_NAME;

        public void ApplyCameraZoom(float baseZoom, float zoom, int zoomLevel)
        {
            foreach (ISearchResultMarker marker in markers.Values)
                marker.SetZoom(coordsUtils.ParcelSize, baseZoom, zoom);
        }

        public UniTask Disable(CancellationToken cancellationToken)
        {
            foreach (ISearchResultMarker marker in markers.Values)
            {
                mapCullingController.StopTracking(marker);
                marker.OnBecameInvisible();
            }

            isEnabled = false;
            return UniTask.CompletedTask;
        }

        public async UniTask Enable(CancellationToken cancellationToken)
        {
            foreach (ISearchResultMarker marker in markers.Values)
                mapCullingController.StartTracking(marker, this);
        }

        public void ResetToBaseScale()
        {
            foreach (var marker in markers.Values)
                marker.ResetScale(coordsUtils.ParcelSize);
        }

        protected override void DisposeImpl()
        {
            objectsPool.Clear();

            foreach (ISearchResultMarker marker in markers.Values)
                marker.Dispose();

            markers.Clear();
        }

        public void OnMapObjectBecameVisible(ISearchResultMarker marker)
        {
            marker.OnBecameVisible();
        }

        public void OnMapObjectCulled(ISearchResultMarker marker)
        {
            marker.OnBecameInvisible();
        }
    }
}
