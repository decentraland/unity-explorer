using DCL.Landscape.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DCL.Landscape
{
    public static class TerrainGenerationUtils
    {
        public static JobHandle SetupEmptyParcelsJobs(
            ref NativeParallelHashMap<int2, int> emptyParcelHeights,
            ref NativeParallelHashMap<int2, EmptyParcelNeighborData> emptyParcelNeighborHeightsData,
            in NativeArray<int2> emptyParcels,
            ref NativeParallelHashSet<int2> ownedParcels,
            int2 minParcel, int2 maxParcel,
            float heightScaleNerf)
        {
            emptyParcelHeights = new NativeParallelHashMap<int2, int>(emptyParcels.Length, Allocator.Persistent);
            emptyParcelNeighborHeightsData = new NativeParallelHashMap<int2, EmptyParcelNeighborData>(emptyParcels.Length, Allocator.Persistent);

            var job = new CalculateEmptyParcelBaseHeightJob(in emptyParcels, ownedParcels.AsReadOnly(), emptyParcelHeights.AsParallelWriter(),
                heightScaleNerf, minParcel, maxParcel);

            JobHandle handle = job.Schedule(emptyParcels.Length, 32);

            var job2 = new CalculateEmptyParcelNeighbourHeights(in emptyParcels, in ownedParcels, emptyParcelNeighborHeightsData.AsParallelWriter(),
                emptyParcelHeights.AsReadOnly(), minParcel, maxParcel);

            return job2.Schedule(emptyParcels.Length, 32, handle);
        }
    }
}
