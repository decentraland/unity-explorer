using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers.Categories;
using DCL.Navmap;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.MapRenderer.MapLayers.Cluster
{
    internal class ClusterController
    {
        private readonly IMapCullingController mapCullingController;
        private readonly List<IClusterMarker> clusteredMarkers = new();
        private readonly List<IClusterableMarker> visibleMarkers = new();
        private readonly Dictionary<GameObject, IClusterMarker> clusterVisibleMarkers = new ();
        private readonly Dictionary<Vector2Int, List<IClusterableMarker>> spatialHashGrid = new();
        private readonly IObjectPool<ClusterMarkerObject> clusterObjectsPool;
        private readonly CategoryMarkersController.ClusterMarkerBuilder clusterBuilder;
        private readonly ICoordsUtils coordsUtils;
        private readonly INavmapBus navmapBus;

        private CancellationTokenSource highlightCt = new ();
        private CancellationTokenSource deHighlightCt = new ();
        private IClusterMarker? previousMarker;
        private int previousZoomLevel = -1;
        private Sprite clusterIcon;

        public ClusterController(
            IMapCullingController mapCullingController,
            IObjectPool<ClusterMarkerObject> clusterObjectsPool,
            CategoryMarkersController.ClusterMarkerBuilder clusterBuilder,
            ICoordsUtils coordsUtils,
            INavmapBus navmapBus)
        {
            this.mapCullingController = mapCullingController;
            this.clusterObjectsPool = clusterObjectsPool;
            this.clusterBuilder = clusterBuilder;
            this.coordsUtils = coordsUtils;
            this.navmapBus = navmapBus;
        }

        public void SetClusterIcon(Sprite currentIcon)
        {
            clusterIcon = currentIcon;
        }

        public List<IClusterableMarker> UpdateClusters(int zoomLevel, float baseZoom, float zoom, Dictionary<Vector2Int, IClusterableMarker> markers)
        {
            if (previousZoomLevel == zoomLevel)
                return visibleMarkers;

            visibleMarkers.Clear();
            previousZoomLevel = zoomLevel;
            float clusterCellSize = ClusterUtilities.CalculateCellSize(zoomLevel);
            foreach (IClusterMarker clusteredMarker in clusteredMarkers)
            {
                mapCullingController.StopTracking(clusteredMarker);
                clusteredMarker.OnBecameInvisible();
            }
            clusteredMarkers.Clear();
            spatialHashGrid.Clear();
            clusterVisibleMarkers.Clear();

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
                    clusterMarker.SetCategorySprite(clusterIcon);
                    clusterMarker.SetZoom(coordsUtils.ParcelSize, baseZoom, zoom);
                    clusterMarker.OnBecameVisible();
                    clusterVisibleMarkers.Add(clusterMarker.GetGameObject(), clusterMarker);
                    clusteredMarkers.Add(clusterMarker);
                }
                else
                {
                    visibleMarkers.Add(cell.Value[0]);
                }
            }

            return visibleMarkers;
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

        public bool HighlightObject(GameObject gameObject)
        {
            if (clusterVisibleMarkers.TryGetValue(gameObject, out IClusterMarker marker))
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
            previousMarker = null;

            if (clusterVisibleMarkers.TryGetValue(gameObject, out IClusterMarker marker))
            {
                deHighlightCt = deHighlightCt.SafeRestart();
                marker.AnimateDeSelectionAsync(deHighlightCt.Token);
                return true;
            }

            return false;
        }

        public bool ClickObject(GameObject gameObject)
        {
            if (clusterVisibleMarkers.TryGetValue(gameObject, out IClusterMarker marker))
            {
                navmapBus.ZoomCamera(true);
                return true;
            }

            return false;
        }

        public void ResetToBaseScale()
        {
            foreach (var marker in clusteredMarkers)
                marker.ResetScale(coordsUtils.ParcelSize);
        }
    }
}
