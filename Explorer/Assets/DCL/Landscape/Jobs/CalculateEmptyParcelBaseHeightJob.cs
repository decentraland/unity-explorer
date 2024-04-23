using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DCL.Landscape.Jobs
{
    /// <summary>
    ///     CalculateEmptyParcelBaseHeightJob
    ///     This job iterates every empty parcel and checks its neighbouring owned parcels to determine the base height
    /// </summary>
    [BurstCompile]
    public struct CalculateEmptyParcelBaseHeightJob : IJobParallelFor
    {
        [ReadOnly] private readonly NativeArray<int2> emptyParcels;
        [ReadOnly] private readonly NativeParallelHashSet<int2>.ReadOnly ownedParcels;
        [ReadOnly] private readonly float heightNerf;
        [ReadOnly] private readonly int2 minBoundsInParcels;
        [ReadOnly] private readonly int2 maxBoundsInParcels;
        private NativeParallelHashMap<int2, int>.ParallelWriter result;

        public CalculateEmptyParcelBaseHeightJob(
            in NativeArray<int2> emptyParcels,
            in NativeParallelHashSet<int2>.ReadOnly ownedParcels,
            NativeParallelHashMap<int2, int>.ParallelWriter result,
            float heightScaleNerf,
            in int2 minBoundsInParcels,
            in int2 maxBoundsInParcels)
        {
            this.emptyParcels = emptyParcels;
            this.ownedParcels = ownedParcels;
            this.result = result;
            heightNerf = heightScaleNerf;
            this.minBoundsInParcels = minBoundsInParcels;
            this.maxBoundsInParcels = maxBoundsInParcels;
        }

        public void Execute(int index)
        {
            int2 position = emptyParcels[index];
            var height = (int)(GetNearestParcelDistance(position, 1) / heightNerf);
            result.TryAdd(position, height);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetNearestParcelDistance(int2 emptyParcelCoords, int radius)
        {
            for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
            {
                var direction = new int2(x, y);
                int2 nextPos = emptyParcelCoords + direction;

                if (IsOutOfBounds(nextPos) || ownedParcels.Contains(nextPos))
                    return radius - 1;
            }

            return GetNearestParcelDistance(emptyParcelCoords, radius + 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsOutOfBounds(int2 parcel) =>
            parcel.x < minBoundsInParcels.x || parcel.x > maxBoundsInParcels.x ||
            parcel.y < minBoundsInParcels.y || parcel.y > maxBoundsInParcels.y;
    }
}
