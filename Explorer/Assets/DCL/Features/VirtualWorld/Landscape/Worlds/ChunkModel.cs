using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.Landscape
{
    public class ChunkModel
    {
        public readonly int2 MinParcel;
        public readonly int2 MaxParcel;

        private readonly List<int2> occupiedParcels;
        private readonly List<int2> outOfTerrainParcels;

        public IReadOnlyList<int2> OutOfTerrainParcels => outOfTerrainParcels;
        public IReadOnlyList<int2> OccupiedParcels => occupiedParcels;

        public TerrainData TerrainData { get; set; }

        public ChunkModel(int2 minParcel, int2 maxParcel)
        {
            MinParcel = minParcel;
            MaxParcel = maxParcel;
            occupiedParcels = new List<int2>();
            outOfTerrainParcels = new List<int2>();
            TerrainData = null;
        }

        public void AddOccupiedParcel(int2 parcel)
        {
            occupiedParcels.Add(parcel);
        }

        public void ProcessOverlap(in int2 overlap)
        {
            if (overlap.x != 0)
                CutHorizontally(overlap.x);

            if (overlap.y != 0)
                CutVertically(overlap.y);
        }

        private void CutHorizontally(int overlap)
        {
            bool fromRight = overlap < 0;
            int amount = Mathf.Abs(overlap);

            for (var i = 0; i < amount; i++)
            {
                int x = fromRight ? MaxParcel.x - i : MinParcel.x + i;
                int xParcel = Mathf.Clamp(x, MinParcel.x, MaxParcel.x);

                for (int y = MinParcel.y; y <= MaxParcel.y; y++)
                    outOfTerrainParcels.Add(new int2(xParcel, y));
            }
        }

        private void CutVertically(int overlap)
        {
            bool fromTop = overlap < 0;
            int amount = Mathf.Abs(overlap);

            for (var i = 0; i < amount; i++)
            {
                int y = fromTop ? MaxParcel.y - i : MinParcel.y + i;
                int yParcel = Mathf.Clamp(y, MinParcel.y, MaxParcel.y);

                for (int x = MinParcel.x; x <= MaxParcel.x; x++)
                    outOfTerrainParcels.Add(new int2(x, yParcel));
            }
        }
    }
}
