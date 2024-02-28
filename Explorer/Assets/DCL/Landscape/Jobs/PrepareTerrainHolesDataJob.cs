using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace DCL.Landscape.Jobs
{
    /// <summary>
    ///     The Holes array are true when there's terrain and false when there's a hole, so we fill the array here to be faster
    /// </summary>
    [BurstCompile]
    public struct SetupTerrainHolesDataJob : IJobParallelFor
    {
        [NativeDisableContainerSafetyRestriction]
        private NativeArray<bool> nativeDataHoles;

        public SetupTerrainHolesDataJob(NativeArray<bool> nativeDataHoles)
        {
            this.nativeDataHoles = nativeDataHoles;
        }

        public void Execute(int index)
        {
            nativeDataHoles[index] = true;
        }
    }

    /// <summary>
    ///     The holes are the size of a parcel which is 16x16, so we just do a simple double for loop to set all flags to false depending on the owned parcel position
    /// </summary>
    [BurstCompile]
    public struct PrepareTerrainHolesDataJob : IJobParallelFor
    {
        [NativeDisableContainerSafetyRestriction]
        private NativeArray<bool> nativeDataHoles;

        [ReadOnly]
        private NativeArray<int2>.ReadOnly ownedParcels;

        private readonly int resolution;

        public PrepareTerrainHolesDataJob(
            NativeArray<bool> nativeDataHoles,
            NativeArray<int2>.ReadOnly ownedParcels,
            int resolution)
        {
            this.nativeDataHoles = nativeDataHoles;
            this.ownedParcels = ownedParcels;
            this.resolution = resolution;
        }

        public void Execute(int index)
        {
            int2 coord = ownedParcels[index];

            for (var i = 0; i < 16; i++)
            for (var j = 0; j < 16; j++)
            {
                int holePixelIndex = coord.x + j + ((coord.y + i) * resolution);
                nativeDataHoles[holePixelIndex] = false;
            }
        }
    }
}
