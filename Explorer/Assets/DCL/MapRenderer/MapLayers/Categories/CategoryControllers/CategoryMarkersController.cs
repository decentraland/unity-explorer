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
        private readonly IObjectPool<ClusterMarkerObject> clusterObjectsPool;
        private readonly ClusterMarkerBuilder clusterBuilder;
        private readonly CategoryIconMappingsSO categoryIconMappings;
        private readonly IPlacesAPIService placesAPIService;

        private readonly Dictionary<Vector2Int, ICategoryMarker> markers = new();
        private readonly List<IClusterMarker> clusteredMarkers = new();
        private readonly Dictionary<Vector2Int, List<ICategoryMarker>> spatialHashGrid = new();

        private Vector2Int decodePointer;
        private bool isEnabled;
        private float clusterCellSize;

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
            this.clusterObjectsPool = clusterObjectsPool;
            this.clusterBuilder = clusterBuilder;
            this.categoryIconMappings = categoryIconMappings;
            this.mapLayer = mapLayer;
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
            clusterCellSize = ClusterUtilities.CalculateCellSize(zoom);

            UpdateClusters();

            foreach (ICategoryMarker marker in markers.Values)
                marker.SetZoom(coordsUtils.ParcelSize, baseZoom, zoom);

            foreach (IClusterMarker clusteredMarker in clusteredMarkers)
                clusteredMarker.SetZoom(coordsUtils.ParcelSize, baseZoom, zoom);
        }

        private void UpdateClusters()
        {
            if (!isEnabled)
                return;

            foreach (IClusterMarker clusteredMarker in clusteredMarkers)
            {
                mapCullingController.StopTracking(clusteredMarker);
                clusteredMarker.OnBecameInvisible();
            }
            clusteredMarkers.Clear();
            spatialHashGrid.Clear();

            foreach (var markerEntry in markers)
            {
                Vector2Int hashPosition = ClusterUtilities.GetHashPosition(markerEntry.Key, clusterCellSize);

                if (!spatialHashGrid.ContainsKey(hashPosition))
                    spatialHashGrid[hashPosition] = new List<ICategoryMarker>();

                spatialHashGrid[hashPosition].Add(markerEntry.Value);
            }

            foreach (var cell in spatialHashGrid)
            {
                if (cell.Value.Count > 1)
                {
                    Vector3 averagePosition = Vector3.zero;
                    foreach (ICategoryMarker marker in cell.Value)
                    {
                        averagePosition += marker.CurrentPosition;
                        mapCullingController.StopTracking(marker);
                        marker.OnBecameInvisible();
                    }
                    averagePosition /= cell.Value.Count;

                    var clusterMarker = clusterBuilder(clusterObjectsPool, mapCullingController, coordsUtils);
                    clusterMarker.SetData(string.Format("{0}", cell.Value.Count), averagePosition);
                    clusterMarker.SetCategorySprite(categoryIconMappings.GetCategoryImage(mapLayer));
                    clusterMarker.OnBecameVisible();
                    clusteredMarkers.Add(clusterMarker);
                }
                else
                {
                    cell.Value[0].OnBecameVisible();
                }
            }
        }

        public UniTask Disable(CancellationToken cancellationToken)
        {
            foreach (ICategoryMarker marker in markers.Values)
            {
                mapCullingController.StopTracking(marker);
                marker.OnBecameInvisible();
            }

            foreach (IClusterMarker clusteredMarker in clusteredMarkers)
            {
                mapCullingController.StopTracking(clusteredMarker);
                clusteredMarker.OnBecameInvisible();
            }

            isEnabled = false;
            return UniTask.CompletedTask;
        }

        public UniTask Enable(CancellationToken cancellationToken)
        {
            foreach (ICategoryMarker marker in markers.Values)
                mapCullingController.StartTracking(marker, this);

            isEnabled = true;
            UpdateClusters();
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
