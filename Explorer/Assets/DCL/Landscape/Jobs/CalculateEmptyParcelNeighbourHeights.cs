using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DCL.Landscape.Jobs
{
    /// <summary>
    ///     CalculateEmptyParcelBaseHeightJob
    ///     This job iterates every empty parcel and checks its closest neighbours to determine the slopes
    /// </summary>
    [BurstCompile]
    public struct CalculateEmptyParcelNeighbourHeights : IJobParallelFor
    {
        private NativeParallelHashMap<int2, EmptyParcelNeighborData>.ParallelWriter result;

        [ReadOnly] private readonly NativeParallelHashMap<int2, int>.ReadOnly emptyParcelHeight;
        [ReadOnly] private readonly NativeArray<int2> emptyParcels;
        [ReadOnly] private NativeParallelHashSet<int2> ownedParcels;

        [ReadOnly] private readonly int2 up;
        [ReadOnly] private readonly int2 right;
        [ReadOnly] private readonly int2 down;
        [ReadOnly] private readonly int2 left;

        public CalculateEmptyParcelNeighbourHeights(
            in NativeArray<int2> emptyParcels,
            in NativeParallelHashSet<int2> ownedParcels,
            NativeParallelHashMap<int2, EmptyParcelNeighborData>.ParallelWriter result,
            in NativeParallelHashMap<int2, int>.ReadOnly emptyParcelHeight)
        {
            this.emptyParcels = emptyParcels;
            this.ownedParcels = ownedParcels;
            this.emptyParcelHeight = emptyParcelHeight;
            this.result = result;
            up = new int2(0, 1);
            right = new int2(1, 0);
            down = new int2(0, -1);
            left = new int2(-1, 0);
        }

        public void Execute(int index)
        {
            int2 position = emptyParcels[index];
            var neighborData = new EmptyParcelNeighborData();

            neighborData.LeftHeight = SafeGet(position + left);
            neighborData.RightHeight = SafeGet(position + right);
            neighborData.UpHeight = SafeGet(position + up);
            neighborData.DownHeight = SafeGet(position + down);
            neighborData.UpLeftHeight = SafeGet(position + up + left);
            neighborData.UpRightHeight = SafeGet(position + up + right);
            neighborData.DownLeftHeight = SafeGet(position + down + left);
            neighborData.DownRightHeight = SafeGet(position + down + right);

            result.TryAdd(position, neighborData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int SafeGet(int2 pos)
        {
            if (IsOutOfBounds(pos.x) || IsOutOfBounds(pos.y) || ownedParcels.Contains(pos))
                return -1;

            if (emptyParcelHeight.TryGetValue(pos, out int item))
                return item;

            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsOutOfBounds(int value) =>
            value is > 150 or < -150;
    }
}
