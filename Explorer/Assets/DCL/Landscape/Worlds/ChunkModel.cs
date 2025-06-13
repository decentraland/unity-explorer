using Unity.Mathematics;
using UnityEngine;

namespace DCL.Landscape
{
    public class ChunkModel
    {
        public readonly int2 MinParcel;
        public readonly int2 MaxParcel;

        public TerrainData TerrainData { get; set; }

        public ChunkModel(int2 minParcel, int2 maxParcel)
        {
            MinParcel = minParcel;
            MaxParcel = maxParcel;
            TerrainData = null;
        }

        public void AddOccupiedParcel(int2 parcel)
        {
        }

        public void ProcessOverlap(in int2 overlap)
        {
        }
    }
}
