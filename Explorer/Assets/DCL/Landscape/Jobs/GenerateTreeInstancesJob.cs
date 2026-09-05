using DCL.Landscape.Config;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace DCL.Landscape.Jobs
{
    /// <summary>
    ///     This job gathers the treeNoise result from other job and does additional checks like:
    ///     - Is there a neighbour owned parcel that overlaps with my asset radius?
    ///     - Is there another asset of the same type within my radius?
    /// </summary>
    [BurstCompile]
    public struct GenerateTreeInstancesJob : IJobParallelFor
    {
        private NativeParallelHashMap<int2, TreeInstance>.ParallelWriter treeInstances;
        private NativeArray<Random> randoms;
        private readonly int parcelSize;
        private readonly bool useRandomSpawnChance;
        private readonly bool useValidations;

        [ReadOnly] private NativeArray<float>.ReadOnly treeNoise;
        [ReadOnly] private ObjectRandomization treeRandomization;
        [ReadOnly] private readonly NativeParallelHashMap<int2, EmptyParcelNeighborData>.ReadOnly emptyParcelResult;
        [ReadOnly] private readonly TreeRadiusPair treeRadius;
        [ReadOnly] private readonly int treeIndex;
        [ReadOnly] private readonly int chunkSize;
        [ReadOnly] private readonly int2 chunkMinParcel;

        private readonly int2 up;
        private readonly int2 right;
        private readonly int2 down;
        private readonly int2 left;

        public GenerateTreeInstancesJob(
            NativeArray<float>.ReadOnly treeNoise,
            NativeParallelHashMap<int2, TreeInstance>.ParallelWriter treeInstances,
            NativeParallelHashMap<int2, EmptyParcelNeighborData>.ReadOnly emptyParcelResult,
            in ObjectRandomization treeRandomization,
            TreeRadiusPair treeRadius,
            int treeIndex,
            in int2 chunkMinParcel,
            int chunkSize,
            int parcelSize,
            NativeArray<Random> randoms,
            bool useRandomSpawnChance = true,
            bool useValidations = true)
        {
            this.treeNoise = treeNoise;
            this.treeInstances = treeInstances;
            this.emptyParcelResult = emptyParcelResult;
            this.treeRandomization = treeRandomization;
            this.treeRadius = treeRadius;
            this.treeIndex = treeIndex;

            this.chunkMinParcel = chunkMinParcel;
            this.chunkSize = chunkSize;
            this.parcelSize = parcelSize;

            this.randoms = randoms;
            this.useRandomSpawnChance = useRandomSpawnChance;
            this.useValidations = useValidations;

            up = new int2(0, 1);
            right = new int2(1, 0);
            down = new int2(0, -1);
            left = new int2(-1, 0);
        }

        public void Execute(int index)
        {
            float value = treeNoise[index];
            Random random = randoms[index];

            // position inside TerrainChunk (units)
            int x = index / chunkSize;
            int y = index % chunkSize;

            float3 randomness = treeRandomization.GetRandomizedPositionOffset(ref random) / chunkSize;
            // position is scaled from 0 to 1 relative TerrainChunk size. This is how it works in Unity Terrain Data
            float3 positionWithinTheChunk = new float3((float)x / chunkSize, 0, (float)y / chunkSize) + randomness;
            float3 treeWorldPosition = (new float3(chunkMinParcel.x, 0, chunkMinParcel.y) * parcelSize) + (positionWithinTheChunk * chunkSize);

            // related world parcel (in parcels)
            int2 parcel = new int2(chunkMinParcel.x + (x / parcelSize), chunkMinParcel.y + (y / parcelSize));
            float3 parcelWorldPos = new float3(parcel.x, 0, parcel.y) * parcelSize;

            if (value <= 0) return;

            Vector2 randomScale = treeRandomization.randomScale;
            float scale = Mathf.Lerp(randomScale.x, randomScale.y, random.NextInt(0, 100) / 100f);

            if (useValidations)
            {
                if(!emptyParcelResult.TryGetValue(parcel, out EmptyParcelNeighborData item)) return;

                float radius = treeRadius.radius * scale * value;

                // We check nearby boundaries (there has to be a simpler way)
                bool u = CheckAssetPosition(item, parcel, parcelWorldPos, treeWorldPosition, up, 0, radius);
                bool ur = CheckAssetPosition(item, parcel, parcelWorldPos, treeWorldPosition, up + right, 0, radius);
                bool r = CheckAssetPosition(item, parcel, parcelWorldPos, treeWorldPosition, right, 0, radius);
                bool rd = CheckAssetPosition(item, parcel, parcelWorldPos, treeWorldPosition, right + down, 0, radius);
                bool d = CheckAssetPosition(item, parcel, parcelWorldPos, treeWorldPosition, down, 0, radius);
                bool dl = CheckAssetPosition(item, parcel, parcelWorldPos, treeWorldPosition, down + left, 0, radius);
                bool l = CheckAssetPosition(item, parcel, parcelWorldPos, treeWorldPosition, left, 0, radius);
                bool lu = CheckAssetPosition(item, parcel, parcelWorldPos, treeWorldPosition, left + up, 0, radius);

                if (!u || !ur || !r || !rd || !d || !dl || !l || !lu)
                    return;
            }

            Vector2 randomRotation = treeRandomization.randomRotationY * Mathf.Deg2Rad;
            float rotation = Mathf.Lerp(randomRotation.x, randomRotation.y, random.NextInt(0, 100) / 100f);
            int3 randColor = random.NextInt3(0, 1);

            var treeInstance = new TreeInstance
            {
                position = positionWithinTheChunk,
                prototypeIndex = treeIndex,
                rotation = rotation,
                widthScale = scale * value,
                heightScale = scale * value,
                color = new Color32((byte)randColor.x, (byte)randColor.y, (byte)randColor.z, 1),
                lightmapColor = Color.white,
            };

            if (useRandomSpawnChance)
            {
                bool canAssetSpawn = random.NextFloat(value * 100, 100) > 80; // we check the chances of this object to spawn

                if (canAssetSpawn)
                    treeInstances.TryAdd(new int2(x, y), treeInstance);
            }
            else
                treeInstances.TryAdd(new int2(x, y), treeInstance);
        }

        // We check the boundaries of our object to see if it can spawn based on the neighbor scenes
        private bool CheckAssetPosition(EmptyParcelNeighborData item, int2 currentParcel, float3 parcelWorldPos, float3 assetPosition, int2 direction,
            int depth, float radius)
        {
            if (GetHeightDirection(item, direction) >= 0)
            {
                int nextDepth = depth + 1;

                if (emptyParcelResult.TryGetValue(currentParcel + (direction * nextDepth), out EmptyParcelNeighborData parcel))
                    return CheckAssetPosition(parcel, currentParcel, parcelWorldPos, assetPosition, direction, nextDepth, radius);
            }
            else
            {
                int halfParcel = parcelSize / 2;

                var v3Dir = new Vector3(direction.x, 0, direction.y);
                Vector3 centerOfParcel = parcelWorldPos + new float3(halfParcel, 0, halfParcel);
                Vector3 posToCheck = centerOfParcel + (v3Dir * halfParcel) + (depth * v3Dir * parcelSize);

                var xIsValid = true;
                var zIsValid = true;

                if (direction.x > 0)
                    xIsValid = assetPosition.x + radius < posToCheck.x;
                else if (direction.x < 0)
                    xIsValid = assetPosition.x - radius > posToCheck.x;

                if (direction.y > 0)
                    zIsValid = assetPosition.z + radius < posToCheck.z;
                else if (direction.y < 0)
                    zIsValid = assetPosition.z - radius > posToCheck.z;

                return xIsValid && zIsValid;
            }

            return false;
        }

        private int GetHeightDirection(EmptyParcelNeighborData item, int2 dir)
        {
            if (dir.Equals(up)) return item.UpHeight;
            if (dir.Equals(up + right)) return item.UpRightHeight;
            if (dir.Equals(right)) return item.RightHeight;
            if (dir.Equals(right + down)) return item.DownRightHeight;
            if (dir.Equals(down)) return item.DownHeight;
            if (dir.Equals(down + left)) return item.DownLeftHeight;
            if (dir.Equals(left)) return item.LeftHeight;
            if (dir.Equals(left + up)) return item.UpLeftHeight;
            return -1;
        }
    }
}
