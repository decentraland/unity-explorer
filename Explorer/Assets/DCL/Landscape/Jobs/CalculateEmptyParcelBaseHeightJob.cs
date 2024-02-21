using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DCL.Landscape.Jobs
{
    /// <summary>CalculateEmptyParcelBaseHeightJob
    /// This job iterates every empty parcel and checks its neighbouring owned parcels to determine the base height
    /// </summary>
    [BurstCompile]
    public struct CalculateEmptyParcelBaseHeightJob : IJobParallelFor
    {
        private NativeParallelHashMap<int2, int>.ParallelWriter result;

        [ReadOnly] private readonly NativeArray<int2> emptyParcels;
        [ReadOnly] private readonly NativeParallelHashSet<int2>.ReadOnly ownedParcels;
        [ReadOnly] private readonly float heightNerf;

        public CalculateEmptyParcelBaseHeightJob(
            in NativeArray<int2> emptyParcels,
            in NativeParallelHashSet<int2>.ReadOnly ownedParcels,
            NativeParallelHashMap<int2, int>.ParallelWriter result,
            float heightScaleNerf)
        {
            this.emptyParcels = emptyParcels;
            this.ownedParcels = ownedParcels;
            this.result = result;
            heightNerf = heightScaleNerf;
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
            {
                for (int y = -radius; y <= radius; y++)
                {
                    var direction = new int2(x, y);
                    int2 nextPos = emptyParcelCoords + direction;

                    if (IsOutOfBounds(nextPos.x) || IsOutOfBounds(nextPos.y))
                        return radius - 1;

                    if (ownedParcels.Contains(nextPos))
                        return radius - 1;
                }
            }

            return GetNearestParcelDistance(emptyParcelCoords, radius + 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsOutOfBounds(int value) =>
            value is > 150 or < -150;
    }
}
