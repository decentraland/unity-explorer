using Cysharp.Threading.Tasks;
using DCL.MapRenderer.Culling;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using ICoordsUtils = DCL.MapRenderer.CoordsUtils.ICoordsUtils;
using IPlacesAPIService = DCL.PlacesAPIService.IPlacesAPIService;
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
        private readonly IPlacesAPIService placesAPIService;

        private readonly Dictionary<Vector2Int, IClusterableMarker> markers = new();

        private Vector2Int decodePointer;
        private bool isEnabled;
        private float clusterCellSize;
        private float baseZoom;
        private float zoom;
        private ClusterController clusterController;
        public CategoryMarkersController(
            IPlacesAPIService placesAPIService,
            IObjectPool<CategoryMarkerObject> objectsPool,
            CategoryMarkerBuilder builder,
            IObjectPool<ClusterMarkerObject> clusterObjectsPool,
            ClusterMarkerBuilder clusterBuilder,
            Transform instantiationParent,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            CategoryIconMappingsSO categoryIconMappings,
            MapLayer mapLayer)
            : base(instantiationParent, coordsUtils, cullingController)
        {
            this.placesAPIService = placesAPIService;
            this.objectsPool = objectsPool;
            this.builder = builder;
            this.categoryIconMappings = categoryIconMappings;
            this.mapLayer = mapLayer;
            clusterController = new ClusterController(mapCullingController, clusterObjectsPool, clusterBuilder, coordsUtils, mapLayer, categoryIconMappings);
        }

        public async UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            List<PlacesData.CategoryPlaceData> placesOfCategory = await placesAPIService.GetPlacesByCategoryListAsync(MapLayerUtils.MapLayerToCategory[mapLayer], cancellationToken);

            foreach (PlacesData.CategoryPlaceData placeInfo in placesOfCategory)
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
        }

        private static bool IsEmptyParcel(PlacesData.CategoryPlaceData sceneInfo) =>
            sceneInfo.name == EMPTY_PARCEL_NAME;

        public void ApplyCameraZoom(float baseZoom, float zoom)
        {
            this.baseZoom = baseZoom;
            this.zoom = zoom;
            clusterCellSize = ClusterUtilities.CalculateCellSize(zoom);

            if(isEnabled)
                clusterController.UpdateClusters(clusterCellSize, baseZoom, zoom, markers);

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

        public UniTask Enable(CancellationToken cancellationToken)
        {
            foreach (ICategoryMarker marker in markers.Values)
                mapCullingController.StartTracking(marker, this);

            isEnabled = true;
            clusterController.UpdateClusters(clusterCellSize, baseZoom, zoom, markers);
            return UniTask.CompletedTask;
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
