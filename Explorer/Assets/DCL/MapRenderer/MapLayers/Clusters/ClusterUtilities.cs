using System.Collections.Generic;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.Categories
{
    public static class ClusterUtilities
    {
        //For each zoom level, the size of the cell in the grid used for the spatial hashing
        private static readonly Dictionary<int, float> ZOOM_TO_CELL_SIZE = new()
        {
            { 0, 35 },
            { 1, 25 },
            { 2, 27 },
            { 3, 12 },
            { 4, 7 },
            { 5, 4 },
            { 6, 1 },
        };

        public static float CalculateCellSize(int zoomLevel) =>
            ZOOM_TO_CELL_SIZE.GetValueOrDefault(zoomLevel, 1);

        public static Vector2Int GetHashPosition(Vector2Int basePosition, float clusterCellSize) =>
            new (
                Mathf.FloorToInt(basePosition.x / clusterCellSize),
                Mathf.FloorToInt(basePosition.y / clusterCellSize)
            );
    }

}
