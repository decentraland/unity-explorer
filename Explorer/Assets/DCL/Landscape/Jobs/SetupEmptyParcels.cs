using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace DCL.Landscape.Jobs
{
    /// <summary>
    /// TODO: DOCUMENT THIS JOB's PURPOSE
    /// </summary>
    [BurstCompile]
    public struct SetupEmptyParcels : IJob     // not a parallel job since NativeHashMap does not support parallel write, we need to figure out a better way of doing this
    {
        [ReadOnly] private readonly NativeArray<Vector2Int> emptyParcels;
        [ReadOnly] private NativeHashSet<Vector2Int> ownedParcels;
        private NativeHashMap<Vector2Int, EmptyParcelData> result;
        public float heightNerf;

        public SetupEmptyParcels(in NativeArray<Vector2Int> emptyParcels, in NativeHashSet<Vector2Int> ownedParcels, ref NativeHashMap<Vector2Int, EmptyParcelData> result)
        {
            this.emptyParcels = emptyParcels;
            this.ownedParcels = ownedParcels;
            this.result = result;
            heightNerf = 0;
        }

        public void Execute()
        {
            // first calculate the base height
            foreach (Vector2Int position in emptyParcels)
            {
                EmptyParcelData data = result[position];

                data.minIndex = (int)Empower(GetNearestParcelDistance(position, 1));
                result[position] = data;
            }

            // then get all the neighbour heights
            foreach (Vector2Int position in emptyParcels)
            {
                EmptyParcelData data = result[position];

                data.leftHeight = SafeGet(position + Vector2Int.left);
                data.rightHeight = SafeGet(position + Vector2Int.right);
                data.upHeight = SafeGet(position + Vector2Int.up);
                data.downHeight = SafeGet(position + Vector2Int.down);

                data.upLeftHeight = SafeGet(position + Vector2Int.up + Vector2Int.left);
                data.upRigthHeight = SafeGet(position + Vector2Int.up + Vector2Int.right);
                data.downLeftHeight = SafeGet(position + Vector2Int.down + Vector2Int.left);
                data.downRightHeight = SafeGet(position + Vector2Int.down + Vector2Int.right);

                result[position] = data;
            }
        }

        private float Empower(float height) =>
            height / heightNerf;

        //math.pow(height, 2f) / heightNerf;

        private int SafeGet(Vector2Int pos)
        {
            if (IsOutOfBounds(pos.x) || IsOutOfBounds(pos.y) || ownedParcels.Contains(pos))
                return -1;

            return result[pos].minIndex;
        }

        private int GetNearestParcelDistance(Vector2Int emptyParcelCoords, int radius)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    var direction = new Vector2Int(x, y);
                    Vector2Int nextPos = emptyParcelCoords + direction;

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
