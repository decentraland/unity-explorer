using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.Landscape.Jobs
{
    /// <summary>
    ///     This job's purpose is to invalidate trees that are too close to each other,
    ///     running this in parallel would imply that all overlapping trees will be invalidated, so we get no trees as a result
    /// </summary>

    //[BurstCompile]
    public struct InvalidateTreesJob : IJobParallelFor
    {
        private readonly NativeParallelHashMap<int2, TreeInstance>.ReadOnly treeInstances;
        private readonly NativeHashMap<int, float>.ReadOnly radius;
        private NativeParallelHashMap<int2, bool>.ParallelWriter treeInvalidationMap;
        private readonly int mapSize;

        public InvalidateTreesJob(
            NativeParallelHashMap<int2, TreeInstance>.ReadOnly treeInstances,
            NativeParallelHashMap<int2, bool>.ParallelWriter treeInvalidationMap,
            NativeHashMap<int, float>.ReadOnly radius,
            int mapSize)
        {
            this.treeInstances = treeInstances;
            this.treeInvalidationMap = treeInvalidationMap;
            this.radius = radius;
            this.mapSize = mapSize;
        }

        public void Execute(int index)
        {
            int x = index / mapSize;
            int y = index % mapSize;

            var key = new int2(x, y);

            if (!treeInstances.TryGetValue(key, out TreeInstance treeInstanceValue))
                return;

            float radiusWithScale = radius[treeInstanceValue.prototypeIndex] * treeInstanceValue.widthScale;
            bool isValid = CheckAssetSpatialAvailability((int)math.ceil(radiusWithScale), key.x, key.y, treeInstanceValue.prototypeIndex, true);
            treeInvalidationMap.TryAdd(key, !isValid);
        }

        /*public void Execute()
        {
            foreach (KeyValue<int2,TreeInstance> treeInstance in treeInstances)
            {
                var key = treeInstance.Key;
                TreeInstance treeInstanceValue = treeInstance.Value;

                float radiusWithScale = radius[treeInstanceValue.prototypeIndex] * treeInstanceValue.widthScale;
                bool isValid = CheckAssetSpatialAvailability((int)math.ceil(radiusWithScale), key.x, key.y, treeInstanceValue.prototypeIndex, true);
                treeInvalidationMap.TryAdd(key, !isValid);
            }
        }*/

        private bool CheckAssetSpatialAvailability(int intRadius, int x, int y, int prototypeIndex, bool recursive)
        {
            var isValid = true;
            int index = x + (y * mapSize);

            for (int i = -intRadius / 2; i < intRadius * 2; i++)
            {
                if (!isValid)
                    break;

                for (int j = -intRadius / 2; j < intRadius * 2; j++)
                {
                    if (!isValid)
                        break;

                    var pointer = new int2(x + i, y + j);
                    int pointerIndex = pointer.x + (pointer.y * mapSize);

                    // on this pointer we try to get another tree instance
                    if (!treeInstances.TryGetValue(pointer, out TreeInstance otherInstance))
                        continue;

                    // if we want certain assets to not overlap with other types, we can do an overlap matrix like the collisions one and implement that logic here
                    isValid = otherInstance.prototypeIndex != prototypeIndex;

                    if (!recursive || isValid || index > pointerIndex)
                        continue;

                    // We do a recursive lookup to check if that asset is going to be invalid as well (specially made for IJobParallelFor)
                    bool isOtherValid = CheckAssetSpatialAvailability(intRadius, pointer.x, pointer.y, prototypeIndex, false);
                    isValid = !isOtherValid;
                }
            }

            return isValid;
        }
    }
}
