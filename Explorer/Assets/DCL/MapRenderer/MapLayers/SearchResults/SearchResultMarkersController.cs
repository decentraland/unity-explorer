using Cysharp.Threading.Tasks;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers.Cluster;
using DCL.Navmap;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using ICoordsUtils = DCL.MapRenderer.CoordsUtils.ICoordsUtils;
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
        private readonly ClusterController clusterController;

        private readonly Dictionary<Vector2Int, IClusterableMarker> markers = new();
        private readonly Dictionary<GameObject, ISearchResultMarker> visibleMarkers = new ();

        private CancellationTokenSource cts = new();
        private Vector2Int decodePointer;
        private CancellationTokenSource highlightCt = new ();
        private CancellationTokenSource deHighlightCt = new ();
        private ISearchResultMarker? previousMarker;
        private bool isEnabled;
        private int zoomLevel = 1;
        private float baseZoom = 1;
        private float zoom = 1;

        public SearchResultMarkersController(
            IObjectPool<SearchResultMarkerObject> objectsPool,
            SearchResultsMarkerBuilder builder,
            Transform instantiationParent,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            INavmapBus navmapBus,
            ClusterController clusterController)
            : base(instantiationParent, coordsUtils, cullingController)
        {
            this.objectsPool = objectsPool;
            this.builder = builder;
            this.navmapBus = navmapBus;
            this.clusterController = clusterController;

            navmapBus.OnPlaceSearched += OnPlaceSearched;
            navmapBus.OnClearPlacesFromMap += OnClearPlacesFromMap;
        }

        private void OnClearPlacesFromMap()
        {
            ReleaseMarkers();
        }

        public async UniTask InitializeAsync(CancellationToken cancellationToken) { }

        private void OnPlaceSearched(INavmapBus.SearchPlaceParams searchParams,
            IReadOnlyList<PlacesData.PlaceInfo> places, int totalResultCount)
        {
            ReleaseMarkers();

            if (!string.IsNullOrEmpty(searchParams.category))
                return;

            foreach (PlacesData.PlaceInfo placeInfo in places)
            {
                if (markers.ContainsKey(MapLayerUtils.GetParcelsCenter(placeInfo)))
                    continue;

                if (IsEmptyParcel(placeInfo))
                    continue;

                var marker = builder(objectsPool, mapCullingController, coordsUtils);
                var centerParcel = MapLayerUtils.GetParcelsCenter(placeInfo);
                var position = coordsUtils.CoordsToPosition(centerParcel);

                marker.SetData(placeInfo.title, position, placeInfo);
                markers.Add(MapLayerUtils.GetParcelsCenter(placeInfo), marker);
                marker.SetZoom(coordsUtils.ParcelSize, baseZoom, zoom);

                if(isEnabled)
                    mapCullingController.StartTracking(marker, this);
            }
            if (isEnabled)
                foreach (ISearchResultMarker clusterableMarker in clusterController.UpdateClusters(zoomLevel, baseZoom, zoom, markers))
                    mapCullingController.StartTracking(clusterableMarker, this);
        }

        private static bool IsEmptyParcel(PlacesData.PlaceInfo sceneInfo) =>
            sceneInfo.title == EMPTY_PARCEL_NAME;

        public void ApplyCameraZoom(float baseZoom, float zoom, int zoomLevel)
        {
            this.baseZoom = baseZoom;
            this.zoom = zoom;
            this.zoomLevel = zoomLevel;

            foreach (ISearchResultMarker marker in markers.Values)
                marker.SetZoom(coordsUtils.ParcelSize, baseZoom, zoom);

            if (isEnabled)
                foreach (ISearchResultMarker clusterableMarker in clusterController.UpdateClusters(zoomLevel, baseZoom, zoom, markers))
                    mapCullingController.StartTracking(clusterableMarker, this);

            clusterController.ApplyCameraZoom(baseZoom, zoom);
        }

        public UniTask Disable(CancellationToken cancellationToken)
        {
            foreach (ISearchResultMarker marker in markers.Values)
            {
                mapCullingController.StopTracking(marker);
                marker.OnBecameInvisible();
            }
            clusterController.Disable();

            isEnabled = false;
            return UniTask.CompletedTask;
        }

        public async UniTask Enable(CancellationToken cancellationToken)
        {
            foreach (ISearchResultMarker marker in markers.Values)
                mapCullingController.StartTracking(marker, this);

            foreach (ISearchResultMarker clusterableMarker in clusterController.UpdateClusters(zoomLevel, baseZoom, zoom, markers))
                mapCullingController.StartTracking(clusterableMarker, this);

            isEnabled = true;
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
            marker.SetZoom(coordsUtils.ParcelSize, baseZoom, zoom);
            GameObject? gameObject = marker.GetGameObject();
            if(gameObject != null)
                visibleMarkers.AddOrReplace(gameObject, marker);
        }

        public void OnMapObjectCulled(ISearchResultMarker marker)
        {
            GameObject? gameObject = marker.GetGameObject();
            if(gameObject != null)
                visibleMarkers.Remove(gameObject);
            marker.OnBecameInvisible();
        }

        private void ReleaseMarkers()
        {
            foreach (ISearchResultMarker marker in markers.Values)
            {
                mapCullingController.StopTracking(marker);
                marker.OnBecameInvisible();
            }

            markers.Clear();
            clusterController.Disable();
        }

        public bool HighlightObject(GameObject gameObject)
        {
            if (clusterController.HighlightObject(gameObject))
                return true;

            if (visibleMarkers.TryGetValue(gameObject, out ISearchResultMarker marker))
            {
                highlightCt = highlightCt.SafeRestart();
                previousMarker?.AnimateDeSelectionAsync(deHighlightCt.Token);
                marker.AnimateSelectionAsync(highlightCt.Token);
                previousMarker = marker;
                return true;
            }

            return false;
        }

        public bool DeHighlightObject(GameObject gameObject)
        {
            if (clusterController.DeHighlightObject(gameObject))
                return true;

            previousMarker = null;

            if (visibleMarkers.TryGetValue(gameObject, out ISearchResultMarker marker))
            {
                deHighlightCt = deHighlightCt.SafeRestart();
                marker.AnimateDeSelectionAsync(deHighlightCt.Token);
                return true;
            }

            return false;
        }

        public bool ClickObject(GameObject gameObject)
        {
            if (clusterController.ClickObject(gameObject))
                return true;

            if (visibleMarkers.TryGetValue(gameObject, out ISearchResultMarker marker))
            {
                cts = cts.SafeRestart();
                navmapBus.SelectPlaceAsync(marker.PlaceInfo, cts.Token, true).Forget();
                return true;
            }

            return false;
        }
    }
}
