using Cysharp.Threading.Tasks;
using DCL.MapRenderer.Culling;
using DCL.PlacesAPIService;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using ICoordsUtils = DCL.MapRenderer.CoordsUtils.ICoordsUtils;
using IPlacesAPIService = DCL.PlacesAPIService.IPlacesAPIService;

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
        private readonly IPlacesAPIService placesAPIService;
        private readonly ClusterController clusterController;

        private readonly Dictionary<Vector2Int, IClusterableMarker> markers = new();

        private Vector2Int decodePointer;
        private bool isEnabled;
        private int zoomLevel = 1;
        private float baseZoom = 1;
        private float zoom = 1;
        private bool arePlacesLoaded;

        public CategoryMarkersController(
            IPlacesAPIService placesAPIService,
            IObjectPool<CategoryMarkerObject> objectsPool,
            CategoryMarkerBuilder builder,
            Transform instantiationParent,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            CategoryIconMappingsSO categoryIconMappings,
            MapLayer mapLayer,
            ClusterController clusterController)
            : base(instantiationParent, coordsUtils, cullingController)
        {
            this.placesAPIService = placesAPIService;
            this.objectsPool = objectsPool;
            this.builder = builder;
            this.categoryIconMappings = categoryIconMappings;
            this.mapLayer = mapLayer;
            this.clusterController = clusterController;
        }

        public async UniTask InitializeAsync(CancellationToken cancellationToken)
        {

        }

        private async UniTask LoadPlaces(CancellationToken cancellationToken)
        {
            IReadOnlyList<OptimizedPlaceInMapResponse> placesOfCategory = await placesAPIService.GetOptimizedPlacesFromTheMap(MapLayerUtils.MapLayerToCategory[mapLayer], cancellationToken);

            foreach (OptimizedPlaceInMapResponse placeInfo in placesOfCategory)
            {
                if (markers.ContainsKey(placeInfo.base_position))
                    continue;

                if (IsEmptyParcel(placeInfo))
                    continue;

                var marker = builder(objectsPool, mapCullingController, coordsUtils);
                var position = coordsUtils.CoordsToPosition(placeInfo.base_position);

                marker.SetData(placeInfo.name, position);
                marker.SetCategorySprite(categoryIconMappings.GetCategoryImage(mapLayer));
                markers.Add(placeInfo.base_position, marker);

                if (isEnabled)
                    mapCullingController.StartTracking(marker, this);
            }

            arePlacesLoaded = true;
        }

        private static bool IsEmptyParcel(OptimizedPlaceInMapResponse sceneInfo) =>
            sceneInfo.name == EMPTY_PARCEL_NAME;

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
            if(!arePlacesLoaded)
                await LoadPlaces(cancellationToken);

            foreach (ICategoryMarker marker in markers.Values)
                mapCullingController.StartTracking(marker, this);

            isEnabled = true;
            clusterController.UpdateClusters(zoomLevel, baseZoom, zoom, markers);
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
    }
}
