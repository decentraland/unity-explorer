using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.MapRenderer.MapLayers.Categories
{
    internal class ClusterController
    {
        private readonly IMapCullingController mapCullingController;
        private readonly List<IClusterMarker> clusteredMarkers = new();
        private readonly Dictionary<Vector2Int, List<IClusterableMarker>> spatialHashGrid = new();
        private readonly IObjectPool<ClusterMarkerObject> clusterObjectsPool;
        private readonly CategoryMarkersController.ClusterMarkerBuilder clusterBuilder;
        private readonly ICoordsUtils coordsUtils;
        private readonly MapLayer mapLayer;
        private readonly CategoryIconMappingsSO categoryIconMappings;
        private int previousZoomLevel = -1;

        public ClusterController(
            IMapCullingController mapCullingController,
            IObjectPool<ClusterMarkerObject> clusterObjectsPool,
            CategoryMarkersController.ClusterMarkerBuilder clusterBuilder,
            ICoordsUtils coordsUtils,
            MapLayer mapLayer,
            CategoryIconMappingsSO categoryIconMappings)
        {
            this.mapCullingController = mapCullingController;
            this.clusterObjectsPool = clusterObjectsPool;
            this.clusterBuilder = clusterBuilder;
            this.coordsUtils = coordsUtils;
            this.mapLayer = mapLayer;
            this.categoryIconMappings = categoryIconMappings;
        }

        public void UpdateClusters(int zoomLevel, float baseZoom, float zoom, Dictionary<Vector2Int, IClusterableMarker> markers)
        {
            if (previousZoomLevel == zoomLevel)
                return;

            previousZoomLevel = zoomLevel;
            float clusterCellSize = ClusterUtilities.CalculateCellSize(zoomLevel);
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
                    spatialHashGrid[hashPosition] = new List<IClusterableMarker>();

                spatialHashGrid[hashPosition].Add(markerEntry.Value);
            }

            foreach (var cell in spatialHashGrid)
            {
                if (cell.Value.Count > 1)
                {
                    Vector3 averagePosition = Vector3.zero;
                    foreach (IClusterableMarker marker in cell.Value)
                    {
                        averagePosition += marker.CurrentPosition;
                        mapCullingController.StopTracking(marker);
                        marker.OnBecameInvisible();
                    }
                    averagePosition /= cell.Value.Count;

                    var clusterMarker = clusterBuilder(clusterObjectsPool, mapCullingController, coordsUtils);
                    clusterMarker.SetData(string.Format("{0}", cell.Value.Count), averagePosition);
                    clusterMarker.SetCategorySprite(categoryIconMappings.GetCategoryImage(mapLayer));
                    clusterMarker.SetZoom(coordsUtils.ParcelSize, baseZoom, zoom);
                    clusterMarker.OnBecameVisible();
                    clusteredMarkers.Add(clusterMarker);
                }
                else
                {
                    cell.Value[0].OnBecameVisible();
                }
            }
        }

        public void ApplyCameraZoom(float baseZoom, float zoom)
        {
            foreach (IClusterMarker clusteredMarker in clusteredMarkers)
                clusteredMarker.SetZoom(coordsUtils.ParcelSize, baseZoom, zoom);
        }

        public void Disable()
        {
            previousZoomLevel = -1;
            foreach (IClusterMarker clusteredMarker in clusteredMarkers)
            {
                mapCullingController.StopTracking(clusteredMarker);
                clusteredMarker.OnBecameInvisible();
            }
        }

    }
}
