using UnityEngine;

namespace DCL.MapRenderer.MapLayers.Categories
{
    public static class ClusterUtilities
    {
        private const float MAX_ZOOM = 300;
        private const float MIN_ZOOM = 3400;
        private const float MAX_GRID_SIZE = 25;
        private const float MIN_GRID_SIZE = 1;

        public static float CalculateCellSize(float value) =>
            ((value - MAX_ZOOM) / (MIN_ZOOM - MAX_ZOOM) * (MAX_GRID_SIZE - MIN_GRID_SIZE)) + MIN_GRID_SIZE;

        public static Vector2Int GetHashPosition(Vector2Int basePosition, float clusterCellSize) =>
            new (
                Mathf.FloorToInt(basePosition.x / clusterCellSize),
                Mathf.FloorToInt(basePosition.y / clusterCellSize)
            );
    }

}
