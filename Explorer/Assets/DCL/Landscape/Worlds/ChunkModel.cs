using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.Landscape
{
    public class ChunkModel
    {
        public readonly int2 MinParcel;
        public readonly int2 MaxParcel;

        public readonly List<int2> OccupiedParcels;
        public readonly List<int2> OutOfTerrainParcels;

        public TerrainData TerrainData;

        public ChunkModel(int2 minParcel, int2 maxParcel)
        {
            MinParcel = minParcel;
            MaxParcel = maxParcel;
            OccupiedParcels = new List<int2>();
            OutOfTerrainParcels = new List<int2>();
            TerrainData = null;
        }

        public void ProcessOverlap(in int2 overlap)
        {
            if (overlap.x < 0) // Overlap to the right
                CutRight(Mathf.Abs(overlap.x));
            else if (overlap.x > 0) // Overlap to the left
                CutLeft(Mathf.Abs(overlap.x));

            if (overlap.y < 0) // Overlap at the top
                CutTop(Mathf.Abs(overlap.y));
            else if (overlap.y > 0)
                CutBottom(Mathf.Abs(overlap.y));
        }

        private void CutLeft(int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                int xPosition = MinParcel.x + i;
                for (int y = MinParcel.y; y <= MaxParcel.y; y++)
                {
                    OutOfTerrainParcels.Add(new int2(xPosition, y));
                }
            }
        }

        private void CutRight(int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                int xPosition = MaxParcel.x - i;
                for (int y = MinParcel.y; y <= MaxParcel.y; y++)
                {
                    OutOfTerrainParcels.Add(new int2(xPosition, y));
                }
            }
        }

        private void CutTop(int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                int yPosition = MaxParcel.y - i;
                for (int x = MinParcel.x; x <= MaxParcel.x; x++)
                {
                    OutOfTerrainParcels.Add(new int2(x, yPosition));
                }
            }
        }

        private void CutBottom(int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                int yPosition = MinParcel.y + i;
                for (int x = MinParcel.x; x <= MaxParcel.x; x++)
                {
                    OutOfTerrainParcels.Add(new int2(x, yPosition));
                }
            }
        }
    }
}
