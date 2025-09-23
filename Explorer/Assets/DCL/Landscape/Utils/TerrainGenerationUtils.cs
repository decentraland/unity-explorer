using DCL.Landscape.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DCL.Landscape
{
    public static class TerrainGenerationUtils
    {
        public static void ExtractEmptyParcels(int2 minParcel, int2 maxParcel,
            ref NativeList<int2> emptyParcels, ref NativeParallelHashSet<int2> ownedParcels)
        {
            if (!emptyParcels.IsCreated)
                emptyParcels = new NativeList<int2>(Allocator.Persistent);

            for (int x = minParcel.x; x <= maxParcel.x; x++)
            for (int y = minParcel.y; y <= maxParcel.y; y++)
            {
                var currentParcel = new int2(x, y);

                if (!ownedParcels.Contains(currentParcel))
                    emptyParcels.Add(currentParcel);
            }
        }

        public static JobHandle SetupEmptyParcelsJobs(
            ref NativeParallelHashMap<int2, int> emptyParcelsData,
            ref NativeParallelHashMap<int2, EmptyParcelNeighborData> emptyParcelsNeighborData,
            in NativeArray<int2> emptyParcels,
            ref NativeParallelHashSet<int2> ownedParcels,
            int2 minParcel, int2 maxParcel,
            float heightScaleNerf)
        {
            emptyParcelsData = new NativeParallelHashMap<int2, int>(emptyParcels.Length, Allocator.Persistent);
            emptyParcelsNeighborData = new NativeParallelHashMap<int2, EmptyParcelNeighborData>(emptyParcels.Length, Allocator.Persistent);

            var job = new CalculateEmptyParcelBaseHeightJob(in emptyParcels, ownedParcels.AsReadOnly(), emptyParcelsData.AsParallelWriter(),
                heightScaleNerf, minParcel, maxParcel);

            JobHandle handle = job.Schedule(emptyParcels.Length, 32);

            var job2 = new CalculateEmptyParcelNeighbourHeights(in emptyParcels, in ownedParcels, emptyParcelsNeighborData.AsParallelWriter(),
                emptyParcelsData.AsReadOnly(), minParcel, maxParcel);

            return job2.Schedule(emptyParcels.Length, 32, handle);
        }
    }
}
