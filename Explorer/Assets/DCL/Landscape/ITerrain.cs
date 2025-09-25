using Unity.Collections;
using UnityEngine;

namespace DCL.Landscape
{
    public interface ITerrain
    {
        bool Contains(Vector2Int parcel);

        float GetHeight(float x, float z);

        public bool IsTerrainShown { get; }
        public TerrainModel? TerrainModel { get; }
        public float MaxHeight { get; }
        public int OccupancyFloor { get; }
        public Texture2D? OccupancyMap { get; }
        public NativeArray<byte> OccupancyMapData { get; }
        public int OccupancyMapSize { get; }
        public TreeData? Trees { get; }
    }
}
