﻿using Cysharp.Threading.Tasks;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers.Cluster;
using DCL.Navmap;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using ICoordsUtils = DCL.MapRenderer.CoordsUtils.ICoordsUtils;
using PlacesData = DCL.PlacesAPIService.PlacesData;

namespace DCL.MapRenderer.MapLayers.Categories
{
    internal class CategoryMarkersController : MapLayerControllerBase, IMapCullingListener<ICategoryMarker>, IMapLayerController, IZoomScalingLayer
    {
        private const string EMPTY_PARCEL_NAME = "Empty parcel";
        private readonly MapLayer mapLayer;

        internal delegate ICategoryMarker CategoryMarkerBuilder(
            IObjectPool<CategoryMarkerObject> objectsPool,
            IMapCullingController cullingController,
            ICoordsUtils coordsUtils);

        internal delegate IClusterMarker ClusterMarkerBuilder(
            IObjectPool<ClusterMarkerObject> objectsPool,
            IMapCullingController cullingController,
            ICoordsUtils coordsUtils);

        private readonly IObjectPool<CategoryMarkerObject> objectsPool;
        private readonly CategoryMarkerBuilder builder;
        private readonly CategoryIconMappingsSO categoryIconMappings;
        private readonly ClusterController clusterController;
        private readonly INavmapBus navmapBus;

        private readonly Dictionary<Vector2Int, IClusterableMarker> markers = new();

        private Vector2Int decodePointer;
        private bool isEnabled;
        private int zoomLevel = 1;
        private float baseZoom = 1;
        private float zoom = 1;

        public CategoryMarkersController(
            IObjectPool<CategoryMarkerObject> objectsPool,
            CategoryMarkerBuilder builder,
            Transform instantiationParent,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            CategoryIconMappingsSO categoryIconMappings,
            MapLayer mapLayer,
            ClusterController clusterController,
            INavmapBus navmapBus)
            : base(instantiationParent, coordsUtils, cullingController)
        {
            this.objectsPool = objectsPool;
            this.builder = builder;
            this.categoryIconMappings = categoryIconMappings;
            this.mapLayer = mapLayer;
            this.clusterController = clusterController;
            this.navmapBus = navmapBus;
            this.navmapBus.OnPlaceSearched += OnPlaceSearched;
        }

        private void OnPlaceSearched(INavmapBus.SearchPlaceParams searchparams, IReadOnlyList<PlacesData.PlaceInfo> places, int totalresultcount)
        {
            ReleaseMarkers();

            if (string.IsNullOrEmpty(searchparams.category) || !string.IsNullOrEmpty(searchparams.text))
                return;

            if (!string.IsNullOrEmpty(searchparams.category) && searchparams.category == mapLayer.ToString())
                ShowPlaces(places);
        }

        public async UniTask InitializeAsync(CancellationToken cancellationToken)
        {

        }

        private void ShowPlaces(IReadOnlyList<PlacesData.PlaceInfo> places)
        {
            ReleaseMarkers();
            foreach (PlacesData.PlaceInfo placeInfo in places)
            {
                if (markers.ContainsKey(MapLayerUtils.GetParcelsCenter(placeInfo)))
                    continue;

                if (IsEmptyParcel(placeInfo))
                    continue;

                var marker = builder(objectsPool, mapCullingController, coordsUtils);
                var position = coordsUtils.CoordsToPosition(MapLayerUtils.GetParcelsCenter(placeInfo));

                marker.SetData(placeInfo.title, position);
                marker.SetCategorySprite(categoryIconMappings.GetCategoryImage(mapLayer));
                markers.Add(MapLayerUtils.GetParcelsCenter(placeInfo), marker);
                marker.SetZoom(coordsUtils.ParcelSize, baseZoom, zoom);

                if (isEnabled)
                    mapCullingController.StartTracking(marker, this);
            }
        }

        private static bool IsEmptyParcel(PlacesData.PlaceInfo sceneInfo) =>
            sceneInfo.title == EMPTY_PARCEL_NAME;

        public void ApplyCameraZoom(float baseZoom, float zoom, int zoomLevel)
        {
            this.baseZoom = baseZoom;
            this.zoom = zoom;
            this.zoomLevel = zoomLevel;

            if (isEnabled)
                clusterController.UpdateClusters(zoomLevel, baseZoom, zoom, markers);

            foreach (ICategoryMarker marker in markers.Values)
                marker.SetZoom(coordsUtils.ParcelSize, baseZoom, zoom);

            clusterController.ApplyCameraZoom(baseZoom, zoom);
        }

        public UniTask Disable(CancellationToken cancellationToken)
        {
            foreach (ICategoryMarker marker in markers.Values)
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
            foreach (ICategoryMarker marker in markers.Values)
                mapCullingController.StartTracking(marker, this);

            clusterController.UpdateClusters(zoomLevel, baseZoom, zoom, markers);
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

            foreach (ICategoryMarker marker in markers.Values)
                marker.Dispose();

            markers.Clear();
        }

        public void OnMapObjectBecameVisible(ICategoryMarker marker)
        {
            marker.OnBecameVisible();
        }

        public void OnMapObjectCulled(ICategoryMarker marker)
        {
            marker.OnBecameInvisible();
        }

        private void ReleaseMarkers()
        {
            foreach (ICategoryMarker marker in markers.Values)
            {
                mapCullingController.StopTracking(marker);
                marker.OnBecameInvisible();
            }

            markers.Clear();
            clusterController.Disable();
        }
    }
}
