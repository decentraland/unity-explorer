using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.Landscape
{
    public class TerrainModel
    {
        // Note: [units] = Unity units (1 unit = 1 meter)

        private const int MAX_CHUNK_SIZE = 512; // Maximum size of a chunk in Unity [units]
        private const int MIN_CHUNKS_PER_SIDE = 2; // Minimum number of chunks along one side of terrain, ensuring at least a 2x2 grid

        public readonly int2 MinParcel;
        public readonly int2 MaxParcel;

        public readonly int2 SizeInUnits;
        public readonly int2 MinInUnits;
        public readonly int2 MaxInUnits;

        public readonly ChunkModel[] ChunkModels;

        private readonly int parcelSize; // Size of a [parcel] in Unity [units]

        public int ChunkSizeInUnits; //  in [units]

        private int chunkSizeInParcels; //  in [parcels]
        private int sizeInChunks; // Number of chunks along one side of the square terrain

        public TerrainModel(int parcelSize, WorldModel world, int paddingInParcels)
        {
            this.parcelSize = parcelSize;

            ChunkSizeInUnits = 0;
            chunkSizeInParcels = 0;
            sizeInChunks = 0;

            int2 sizeInParcels = world.SizeInParcels + (2 * paddingInParcels);
            int2 centerInParcels = world.CenterInParcels;
            MinParcel = centerInParcels - (sizeInParcels / 2);
            MaxParcel = MinParcel + (sizeInParcels - 1); // last parcel is inclusive

            SizeInUnits = sizeInParcels * parcelSize;
            MinInUnits = MinParcel * parcelSize;
            MaxInUnits = MinInUnits + SizeInUnits;

            CalculateChunkSizeAndCount();

            // Generate chunk models
            {
                ChunkModels = new ChunkModel[sizeInChunks * sizeInChunks];

                for (var x = 0; x < sizeInChunks; x++)
                for (var y = 0; y < sizeInChunks; y++)
                {
                    int2 minChunkParcel = MinParcel + new int2(x * chunkSizeInParcels, y * chunkSizeInParcels);
                    int2 maxParcelPosition = minChunkParcel + new int2(chunkSizeInParcels, chunkSizeInParcels) - new int2(1, 1);
                    var model = new ChunkModel(minChunkParcel, maxParcelPosition);

                    if (TryOverlap(model, out int2 overlap))
                        model.ProcessOverlap(overlap);

                    ChunkModels[x + (y * sizeInChunks)] = model;
                }

                foreach (int2 parcel in world.OwnedParcels)
                foreach (ChunkModel chunk in ChunkModels)
                    if (ChunkContains(chunk, parcel))
                    {
                        chunk.AddOccupiedParcel(parcel);
                        break;
                    }
            }
        }

        private void CalculateChunkSizeAndCount()
        {
            int maxSideLengthInUnits = Mathf.Max(SizeInUnits.x, SizeInUnits.y);

            // Determine the number of chunks needed along one side to cover the largest dimension of the terrain
            sizeInChunks = Mathf.Max(MIN_CHUNKS_PER_SIDE, Mathf.CeilToInt((float)maxSideLengthInUnits / MAX_CHUNK_SIZE));

            ChunkSizeInUnits = Mathf.ClosestPowerOfTwo(maxSideLengthInUnits / sizeInChunks);
            ChunkSizeInUnits = Mathf.Min(ChunkSizeInUnits, MAX_CHUNK_SIZE); // Ensure it doesn't exceed the max size
            chunkSizeInParcels = ChunkSizeInUnits / parcelSize;

            if (maxSideLengthInUnits > ChunkSizeInUnits * sizeInChunks)
                sizeInChunks = Mathf.CeilToInt((float)maxSideLengthInUnits / ChunkSizeInUnits);
        }

        private bool ChunkContains(ChunkModel chunk, int2 parcel) =>
            parcel.x >= chunk.MinParcel.x &&
            parcel.x < chunk.MinParcel.x + chunkSizeInParcels &&
            parcel.y >= chunk.MinParcel.y &&
            parcel.y < chunk.MinParcel.y + chunkSizeInParcels;

        private bool TryOverlap(in ChunkModel chunk, out int2 overlap)
        {
            var horizontalOverlap = 0;
            var verticalOverlap = 0;

            // Horizontal overlap
            if (chunk.MinParcel.x < MinParcel.x)
                horizontalOverlap = MinParcel.x - chunk.MinParcel.x; // Left overlap
            else if (chunk.MaxParcel.x > MaxParcel.x)
                horizontalOverlap = MaxParcel.x - chunk.MaxParcel.x; // Right overlap

            // Vertical overlap
            if (chunk.MinParcel.y < MinParcel.y)
                verticalOverlap = MinParcel.y - chunk.MaxParcel.y; // Bottom overlap
            else if (chunk.MaxParcel.y > MaxParcel.y)
                verticalOverlap = MaxParcel.y - chunk.MaxParcel.y; // Top overlap

            overlap = new int2(horizontalOverlap, verticalOverlap);

            return overlap.x != 0 || overlap.y != 0;
        }
    }
}
