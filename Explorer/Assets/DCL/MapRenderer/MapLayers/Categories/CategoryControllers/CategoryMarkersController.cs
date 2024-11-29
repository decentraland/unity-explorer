using Cysharp.Threading.Tasks;
using DCL.EventsApi;
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

namespace DCL.MapRenderer.MapLayers.Categories
{
    internal class CategoryMarkersController : MapLayerControllerBase, IMapCullingListener<ICategoryMarker>, IMapLayerController, IZoomScalingLayer
    {
        private const string EMPTY_PARCEL_NAME = "Empty parcel";

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
        private readonly CategoryLayerIconMappingsSO categoryIconMappings;
        private readonly ClusterController clusterController;
        private readonly INavmapBus navmapBus;

        private readonly Dictionary<Vector2Int, IClusterableMarker> markers = new();
        private readonly Dictionary<GameObject, ICategoryMarker> visibleMarkers = new ();

        private CancellationTokenSource cts = new();
        private Vector2Int decodePointer;
        private CancellationTokenSource highlightCt = new ();
        private CancellationTokenSource deHighlightCt = new ();
        private ICategoryMarker? previousMarker;
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
            CategoryLayerIconMappingsSO categoryIconMappings,
            ClusterController clusterController,
            INavmapBus navmapBus)
            : base(instantiationParent, coordsUtils, cullingController)
        {
            this.objectsPool = objectsPool;
            this.builder = builder;
            this.categoryIconMappings = categoryIconMappings;
            this.clusterController = clusterController;
            this.navmapBus = navmapBus;
            this.navmapBus.OnPlaceSearched += OnPlaceSearched;
        }

        private void OnPlaceSearched(INavmapBus.SearchPlaceParams searchparams, IReadOnlyList<PlacesData.PlaceInfo> places, int totalresultcount)
        {
            ReleaseMarkers();

            if (string.IsNullOrEmpty(searchparams.category) || !string.IsNullOrEmpty(searchparams.text))
                return;

            if (!string.IsNullOrEmpty(searchparams.category))
            {
                Enum.TryParse(searchparams.category, out CategoriesEnum mapLayer);
                ShowPlaces(places, mapLayer);
            }
        }

        public async UniTask InitializeAsync(CancellationToken cancellationToken)
        {

        }

        private void ShowPlaces(IReadOnlyList<PlacesData.PlaceInfo> places, CategoriesEnum mapLayer)
        {
            ReleaseMarkers();
            Sprite categoryImage = categoryIconMappings.GetCategoryImage(mapLayer);
            clusterController.SetClusterIcon(categoryImage);
            foreach (PlacesData.PlaceInfo placeInfo in places)
            {
                if (markers.ContainsKey(MapLayerUtils.GetParcelsCenter(placeInfo)))
                    continue;

                if (IsEmptyParcel(placeInfo))
                    continue;

                var marker = builder(objectsPool, mapCullingController, coordsUtils);
                var position = coordsUtils.CoordsToPosition(MapLayerUtils.GetParcelsCenter(placeInfo));

                marker.SetData(placeInfo.title, position, placeInfo, new EventDTO());
                marker.SetCategorySprite(categoryImage);
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
                foreach (ICategoryMarker clusterableMarker in clusterController.UpdateClusters(zoomLevel, baseZoom, zoom, markers))
                    mapCullingController.StartTracking(clusterableMarker, this);

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

            foreach (ICategoryMarker clusterableMarker in clusterController.UpdateClusters(zoomLevel, baseZoom, zoom, markers))
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

            foreach (ICategoryMarker marker in markers.Values)
                marker.Dispose();

            markers.Clear();
        }

        public void OnMapObjectBecameVisible(ICategoryMarker marker)
        {
            marker.OnBecameVisible();
            GameObject? gameObject = marker.GetGameObject();
            if(gameObject != null)
                visibleMarkers.AddOrReplace(gameObject, marker);
        }

        public void OnMapObjectCulled(ICategoryMarker marker)
        {
            GameObject? gameObject = marker.GetGameObject();
            if(gameObject != null)
                visibleMarkers.Remove(gameObject);
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

        public bool HighlightObject(GameObject gameObject)
        {
            if (clusterController.HighlightObject(gameObject))
                return true;

            if (visibleMarkers.TryGetValue(gameObject, out ICategoryMarker marker))
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

            if (visibleMarkers.TryGetValue(gameObject, out ICategoryMarker marker))
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

            if (visibleMarkers.TryGetValue(gameObject, out ICategoryMarker marker))
            {
                cts = cts.SafeRestart();
                navmapBus.SelectPlaceAsync(marker.PlaceInfo, cts.Token, true).Forget();
                return true;
            }

            return false;
        }
    }
}
