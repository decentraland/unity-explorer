using UnityEngine;

namespace DCL.Landscape
{
    public interface ITerrain
    {
        bool Contains(Vector2Int parcel);

        float GetHeight(float x, float z);

        public bool IsTerrainShown { get; }
        public int OccupancyFloor { get; }
        public Texture2D? OccupancyMap { get; }
        public TreeData? Trees { get; }
    }
}
