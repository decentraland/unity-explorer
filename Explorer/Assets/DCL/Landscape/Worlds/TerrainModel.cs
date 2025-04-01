using Unity.Mathematics;
using UnityEngine;

namespace DCL.Landscape
{
    public class TerrainModel
    {
        // Note: [units] = Unity units (1 unit = 1 meter)

        private const int MAX_CHUNK_SIZE = 1024; // Maximum size of a chunk in Unity [units]
        private const int MIN_CHUNKS_PER_SIDE = 2; // Minimum number of chunks along one side of terrain, ensuring at least a 2x2 grid

        public readonly int2 MinParcel;
        public readonly int2 MaxParcel;

        public readonly int2 SizeInUnits;
        public readonly int2 MinInUnits;
        public readonly int2 MaxInUnits;

        public readonly ChunkModel[] ChunkModels;

        private readonly int parcelSize; // Size of a [parcel] in Unity [units]

        public int ChunkSizeInUnits; //  in [units]

        public readonly int ChunkSizeInParcels; //  in [parcels]
        public readonly int SizeInChunks; // Number of chunks along one side of the square terrain

        public TerrainModel(int parcelSize, WorldModel world, int paddingInParcels)
        {
            this.parcelSize = parcelSize;

            ChunkSizeInUnits = 0;
            ChunkSizeInParcels = 0;

            int2 sizeInParcels = world.SizeInParcels + (2 * paddingInParcels);
            int2 centerInParcels = world.CenterInParcels;
            MinParcel = centerInParcels - (sizeInParcels / 2);
            MaxParcel = MinParcel + (sizeInParcels - 1); // last parcel is inclusive

            SizeInUnits = sizeInParcels * parcelSize;
            MinInUnits = MinParcel * parcelSize;
            MaxInUnits = MinInUnits + SizeInUnits;

            (SizeInChunks, ChunkSizeInParcels) = CalculateChunkSizeAndCount();

            // Generate chunk models
            {
                ChunkModels = new ChunkModel[SizeInChunks * SizeInChunks];

                for (var x = 0; x < SizeInChunks; x++)
                for (var y = 0; y < SizeInChunks; y++)
                {
                    int2 minChunkParcel = MinParcel + new int2(x * ChunkSizeInParcels, y * ChunkSizeInParcels);
                    int2 maxParcelPosition = minChunkParcel + new int2(ChunkSizeInParcels, ChunkSizeInParcels) - new int2(1, 1);
                    var model = new ChunkModel(minChunkParcel, maxParcelPosition);

                    if (TryOverlap(model, out int2 overlap))
                        model.ProcessOverlap(overlap);

                    ChunkModels[x + (y * SizeInChunks)] = model;
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

        public bool IsInsideBounds(Vector2Int parcel) =>
            parcel.x >= MinParcel.x && parcel.x <= MaxParcel.x && parcel.y >= MinParcel.y && parcel.y <= MaxParcel.y;

        private (int size, int count) CalculateChunkSizeAndCount()
        {
            int maxSideLengthInUnits = Mathf.Max(SizeInUnits.x, SizeInUnits.y);

            // Determine the number of chunks needed along one side to cover the largest dimension of the terrain
            int sizeInChunks = Mathf.Max(MIN_CHUNKS_PER_SIDE, Mathf.CeilToInt((float)maxSideLengthInUnits / MAX_CHUNK_SIZE));

            ChunkSizeInUnits = Mathf.ClosestPowerOfTwo(maxSideLengthInUnits / sizeInChunks);
            ChunkSizeInUnits = Mathf.Min(ChunkSizeInUnits, MAX_CHUNK_SIZE); // Ensure it doesn't exceed the max size
            int chunkSizeInParcels = ChunkSizeInUnits / parcelSize;

            if (maxSideLengthInUnits > ChunkSizeInUnits * sizeInChunks)
                sizeInChunks = Mathf.CeilToInt((float)maxSideLengthInUnits / ChunkSizeInUnits);

            return (sizeInChunks, chunkSizeInParcels);
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
