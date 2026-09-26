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
    /// </summary>
    [BurstCompile]
    public struct InvalidateTreesJob : IJobParallelFor
    {
        private readonly NativeParallelHashMap<int2, TreeInstance>.ReadOnly treeInstances;
        private readonly NativeHashMap<int, TreeRadiusPair>.ReadOnly radius;
        private NativeParallelHashMap<int2, bool>.ParallelWriter treeInvalidationMap;
        private readonly int mapSize;

        public InvalidateTreesJob(
            NativeParallelHashMap<int2, TreeInstance>.ReadOnly treeInstances,
            NativeParallelHashMap<int2, bool>.ParallelWriter treeInvalidationMap,
            NativeHashMap<int, TreeRadiusPair>.ReadOnly radius,
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

            TreeRadiusPair treeRadiusPair = radius[treeInstanceValue.prototypeIndex];
            float radiusWithScale = treeRadiusPair.radius * treeInstanceValue.widthScale;
            bool isValid = CheckAssetSpatialAvailability((int)math.ceil(radiusWithScale), treeRadiusPair.secondaryRadius, key.x, key.y, treeInstanceValue.prototypeIndex, true);
            treeInvalidationMap.TryAdd(key, !isValid);
        }

        private bool CheckAssetSpatialAvailability(int intRadius, float secondaryRadius, int x, int y, int itemPrototypeIndex,
            bool recursive)
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

                    TreeRadiusPair treeRadiusPair = radius[otherInstance.prototypeIndex];
                    float otherRadius = treeRadiusPair.secondaryRadius;

                    // if we want certain assets to not overlap with other types, we can do an overlap matrix like the collisions one and implement that logic here
                    bool isPrototypeDifferent = otherInstance.prototypeIndex != itemPrototypeIndex;

                    if (isPrototypeDifferent)
                    {
                        // we check the secondary radius of that asset to see if we overlap with it
                        float sum = otherRadius + secondaryRadius;
                        isValid = math.distancesq(new int2(x, y), pointer) > sum * sum;
                    }
                    else
                        isValid = otherInstance.prototypeIndex != itemPrototypeIndex;

                    if (!recursive || isValid || index > pointerIndex)
                        continue;

                    // We do a recursive lookup to check if that asset is going to be invalid as well (specially made for IJobParallelFor)
                    if (isPrototypeDifferent)
                    {
                        bool isOtherValid = CheckAssetSpatialAvailability((int)math.ceil(treeRadiusPair.radius), otherRadius, pointer.x, pointer.y, otherInstance.prototypeIndex, false);
                        isValid = !isOtherValid;
                    }
                    else
                    {
                        bool isOtherValid = CheckAssetSpatialAvailability(intRadius, secondaryRadius, pointer.x, pointer.y, otherInstance.prototypeIndex, false);
                        isValid = !isOtherValid;
                    }
                }
            }

            return isValid;
        }
    }
}
