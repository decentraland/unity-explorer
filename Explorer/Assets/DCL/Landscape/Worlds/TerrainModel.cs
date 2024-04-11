using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.Landscape
{
    public class TerrainModel
    {
        // Note: [units] = Unity units (1 unit = 1 meter)

        private const int MAX_CHUNK_SIZE = 512; // Maximum size of a chunk in Unity [units]
        private const int PARCEL_SIZE = 16; // Size of a [parcel] in Unity [units]
        private const int MIN_CHUNKS_PER_SIDE = 2; // Minimum number of chunks along one side of terrain, ensuring at least a 2x2 grid

        public readonly int2 sizeInParcels;
        public readonly int2 centerInParcels;
        public readonly int2 minParcel;
        public readonly int2 maxParcel;

        public readonly int2 sizeInUnits;
        public readonly int2 centerInUnits;
        public readonly int2 minInUnits;
        public readonly int2 maxInUnits;

        public int ChunkSizeInUnits; //  in [units]
        public int ChunkSizeInParcels; //  in [parcels]

        public int SizeInChunks; // Number of chunks along one side of the square terrain

        public List<ChunkModel> ChunkModels; // List to store ChunkModel instances

        public TerrainModel(WorldModel world, int paddingInParcels)
        {
            ChunkSizeInUnits = 0;
            ChunkSizeInParcels = 0;
            SizeInChunks = 0;

            sizeInParcels = world.sizeInParcels + (2 * paddingInParcels);
            centerInParcels = world.centerInParcels;
            minParcel = centerInParcels - (sizeInParcels / 2);
            maxParcel = minParcel + (sizeInParcels - 1); // last parcel is inclusive

            sizeInUnits = sizeInParcels * PARCEL_SIZE;
            centerInUnits = centerInParcels * PARCEL_SIZE;
            minInUnits = minParcel * PARCEL_SIZE;
            maxInUnits = minInUnits + sizeInUnits;

            ChunkModels = new List<ChunkModel>();

            CalculateChunkSizeAndCount();
            GenerateChunkModels(world.ownedParcels);
        }

        private void CalculateChunkSizeAndCount()
        {
            int maxSideLengthInUnits = Mathf.Max(sizeInUnits.x, sizeInUnits.y);

            // Determine the number of chunks needed along one side to cover the largest dimension of the terrain
            SizeInChunks = Mathf.Max(MIN_CHUNKS_PER_SIDE, Mathf.CeilToInt((float)maxSideLengthInUnits / MAX_CHUNK_SIZE));

            ChunkSizeInUnits = Mathf.ClosestPowerOfTwo(maxSideLengthInUnits / SizeInChunks);
            ChunkSizeInUnits = Mathf.Min(ChunkSizeInUnits, MAX_CHUNK_SIZE); // Ensure it doesn't exceed the max size
            ChunkSizeInParcels = ChunkSizeInUnits / PARCEL_SIZE;

            if (maxSideLengthInUnits > ChunkSizeInUnits * SizeInChunks)
                SizeInChunks = Mathf.CeilToInt((float)maxSideLengthInUnits / ChunkSizeInUnits);
        }

        private void GenerateChunkModels(NativeParallelHashSet<int2> worldOwnedParcels)
        {
            ChunkModels = new List<ChunkModel>(SizeInChunks * SizeInChunks);

            for (var x = 0; x < SizeInChunks; x++)
            for (var y = 0; y < SizeInChunks; y++)
            {
                var minChunkParcel = minParcel + new int2(x * ChunkSizeInParcels, y * ChunkSizeInParcels);
                int2 maxParcelPosition = minChunkParcel + new int2(ChunkSizeInParcels, ChunkSizeInParcels) - new int2(1, 1);
                var model = new ChunkModel(minChunkParcel, maxParcelPosition);

                if (TryOverlap(model, out var overlap))
                    model.ProcessOverlap(overlap);

                ChunkModels.Add(model);
            }

            foreach (var parcel in worldOwnedParcels)
            foreach (var chunk in ChunkModels)
                if (ChunkContains(chunk, parcel))
                {
                    chunk.OccupiedParcels.Add(parcel);
                    break;
                }
        }

        private bool ChunkContains(ChunkModel chunk, int2 parcel) =>
            parcel.x >= chunk.MinParcel.x &&
            parcel.x < chunk.MinParcel.x + ChunkSizeInParcels &&
            parcel.y >= chunk.MinParcel.y &&
            parcel.y < chunk.MinParcel.y + ChunkSizeInParcels;

        private bool TryOverlap(in ChunkModel chunk, out int2 overlap)
        {
            var horizontalOverlap = 0;
            var verticalOverlap = 0;

            // Horizontal overlap
            if (chunk.MinParcel.x < minParcel.x)
                horizontalOverlap = minParcel.x - chunk.MinParcel.x; // Left overlap
            else if (chunk.MaxParcel.x > maxParcel.x)
                horizontalOverlap = maxParcel.x - chunk.MaxParcel.x; // Right overlap

            // Vertical overlap
            if (chunk.MinParcel.y < minParcel.y)
                verticalOverlap = minParcel.y - chunk.MaxParcel.y; // Bottom overlap
            else if (chunk.MaxParcel.y > maxParcel.y)
                verticalOverlap = maxParcel.y - chunk.MaxParcel.y; // Top overlap

            overlap = new int2(horizontalOverlap, verticalOverlap);

            return overlap.x != 0 || overlap.y != 0;
        }
    }
}
