using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DCL.Landscape.Jobs
{
    /// <summary>
    /// TODO: DOCUMENT THIS JOB's PURPOSE
    /// </summary>
    [BurstCompile]
    public struct SetupEmptyParcels : IJob     // not a parallel job since NativeHashMap does not support parallel write, we need to figure out a better way of doing this
    {
        [ReadOnly] private readonly NativeArray<int2> emptyParcels;
        [ReadOnly] private NativeHashSet<int2> ownedParcels;
        private NativeHashMap<int2, EmptyParcelData> result;
        public float heightNerf;

        private readonly int2 UP;
        private readonly int2 RIGHT;
        private readonly int2 DOWN;
        private readonly int2 LEFT;

        public SetupEmptyParcels(in NativeArray<int2> emptyParcels, in NativeHashSet<int2> ownedParcels, ref NativeHashMap<int2, EmptyParcelData> result)
        {
            this.emptyParcels = emptyParcels;
            this.ownedParcels = ownedParcels;
            this.result = result;
            heightNerf = 0;
            UP = new int2(0, 1);
            RIGHT = new int2(1, 0);
            DOWN = new int2(0, -1);
            LEFT = new int2(-1, 0);
        }

        public void Execute()
        {
            // first calculate the base height
            foreach (int2 position in emptyParcels)
            {
                EmptyParcelData data = result[position];

                data.minIndex = (int)Empower(GetNearestParcelDistance(position, 1));
                result[position] = data;
            }

            // then get all the neighbour heights
            foreach (int2 position in emptyParcels)
            {
                EmptyParcelData data = result[position];

                data.leftHeight = SafeGet(position + LEFT);
                data.rightHeight = SafeGet(position + RIGHT);
                data.upHeight = SafeGet(position + UP);
                data.downHeight = SafeGet(position + DOWN);

                data.upLeftHeight = SafeGet(position + UP + LEFT);
                data.upRigthHeight = SafeGet(position + UP + RIGHT);
                data.downLeftHeight = SafeGet(position + DOWN + LEFT);
                data.downRightHeight = SafeGet(position + DOWN + RIGHT);

                result[position] = data;
            }
        }

        private float Empower(float height) =>
            height / heightNerf;

        //math.pow(height, 2f) / heightNerf;

        private int SafeGet(int2 pos)
        {
            if (IsOutOfBounds(pos.x) || IsOutOfBounds(pos.y) || ownedParcels.Contains(pos))
                return -1;

            return result[pos].minIndex;
        }

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
