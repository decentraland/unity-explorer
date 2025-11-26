using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace DCL.Landscape
{
    public interface ITerrain
    {
        public bool IsTerrainShown { get; }
        public float MaxHeight { get; }
        public int OccupancyFloor { get; }
        public Texture2D? OccupancyMap { get; }
        public NativeArray<byte> OccupancyMapData { get; }
        public int OccupancyMapSize { get; }
        public int ParcelSize { get; }
        public TerrainModel? TerrainModel { get; }
        public TreeData? Trees { get; }
        public IReadOnlyList<Transform> Cliffs { get; }

        public int GetChunkSize();
    }
}
