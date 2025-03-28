﻿using Cysharp.Threading.Tasks;
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
        public bool ZoomBlocked { get; set; }

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
            this.navmapBus.OnSelectPlaceFromResultsPanel += OnSelectPlaceFromResultsPanel;
        }

        private void OnSelectPlaceFromResultsPanel(Vector2Int coordinates, bool isHovered, bool isClicked)
        {
            if (markers.TryGetValue(coordinates, out IClusterableMarker marker))
            {
                marker.SetIsSelected(isClicked);
                if(isHovered)
                    marker.AnimateSelectionAsync(highlightCt.Token);
                else
                    marker.AnimateDeSelectionAsync(deHighlightCt.Token);
            }
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
                if (markers.ContainsKey(placeInfo.base_position_processed))
                    continue;

                if (IsEmptyParcel(placeInfo))
                    continue;

                var marker = builder(objectsPool, mapCullingController, coordsUtils);
                var centerParcel = MapLayerUtils.GetParcelsCenter(placeInfo);
                var position = coordsUtils.CoordsToPosition(centerParcel);

                marker.SetData(placeInfo.title, position, placeInfo);
                markers.Add(placeInfo.base_position_processed, marker);
                marker.SetZoom(coordsUtils.ParcelSize, baseZoom, zoom);

                if(isEnabled)
                    mapCullingController.StartTracking(marker, this);
            }
            if (isEnabled && !ZoomBlocked)
                foreach (ISearchResultMarker clusterableMarker in clusterController.UpdateClusters(zoomLevel, baseZoom, zoom, markers))
                    mapCullingController.StartTracking(clusterableMarker, this);
        }

        private static bool IsEmptyParcel(PlacesData.PlaceInfo sceneInfo) =>
            sceneInfo.title == EMPTY_PARCEL_NAME;

        public void ApplyCameraZoom(float baseZoom, float zoom, int zoomLevel)
        {
            if (ZoomBlocked)
                return;

            this.baseZoom = baseZoom;
            this.zoom = zoom;
            this.zoomLevel = zoomLevel;

            foreach (ISearchResultMarker marker in markers.Values)
                marker.SetZoom(coordsUtils.ParcelSize, baseZoom, zoom);

            if (isEnabled && !ZoomBlocked)
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

        public async UniTask EnableAsync(CancellationToken cancellationToken)
        {
            foreach (ISearchResultMarker marker in markers.Values)
                mapCullingController.StartTracking(marker, this);

            if (!ZoomBlocked)
            {
                foreach (ISearchResultMarker clusterableMarker in clusterController.UpdateClusters(zoomLevel, baseZoom, zoom, markers))
                    mapCullingController.StartTracking(clusterableMarker, this);
            }

            isEnabled = true;
        }

        public void ResetToBaseScale()
        {
            foreach (var marker in markers.Values)
                marker.ResetScale(coordsUtils.ParcelSize);

            clusterController.ResetToBaseScale();
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

        public bool TryHighlightObject(GameObject gameObject, out IMapRendererMarker? mapMarker)
        {
            mapMarker = null;
            if (clusterController.HighlightObject(gameObject))
                return true;

            if (visibleMarkers.TryGetValue(gameObject, out ISearchResultMarker marker))
            {
                mapMarker = marker;
                highlightCt = highlightCt.SafeRestart();
                previousMarker?.AnimateDeSelectionAsync(deHighlightCt.Token);
                marker.AnimateSelectionAsync(highlightCt.Token);
                previousMarker = marker;
                return true;
            }

            return false;
        }

        public bool TryDeHighlightObject(GameObject gameObject)
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

        public bool TryClickObject(GameObject gameObject, CancellationTokenSource cts, out IMapRendererMarker? mapRendererMarker)
        {
            mapRendererMarker = null;
            if (clusterController.ClickObject(gameObject))
                return true;

            if (visibleMarkers.TryGetValue(gameObject, out ISearchResultMarker marker))
            {
                marker.ToggleSelection(true);
                navmapBus.SelectPlaceAsync(marker.PlaceInfo, cts.Token, true).Forget();
                mapRendererMarker = marker;
                return true;
            }

            return false;
        }
    }
}
