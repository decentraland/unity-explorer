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
        private readonly bool useRandomSpawnChance;
        private readonly bool useValidations;

        [ReadOnly] private NativeArray<float>.ReadOnly treeNoise;
        [ReadOnly] private ObjectRandomization treeRandomization;
        [ReadOnly] private readonly NativeParallelHashMap<int2, EmptyParcelNeighborData>.ReadOnly emptyParcelResult;
        [ReadOnly] private readonly float treeRadius;
        [ReadOnly] private readonly int treeIndex;
        [ReadOnly] private readonly int offsetX;
        [ReadOnly] private readonly int offsetZ;
        [ReadOnly] private readonly int chunkSize;
        [ReadOnly] private readonly int chunkDensity;
        [ReadOnly] private readonly int2 minWorldParcel;

        private readonly int2 UP;
        private readonly int2 RIGHT;
        private readonly int2 DOWN;
        private readonly int2 LEFT;

        public GenerateTreeInstancesJob(
            NativeArray<float>.ReadOnly treeNoise,
            NativeParallelHashMap<int2, TreeInstance>.ParallelWriter treeInstances,
            NativeParallelHashMap<int2, EmptyParcelNeighborData>.ReadOnly emptyParcelResult,
            in ObjectRandomization treeRandomization,
            float treeRadius,
            int treeIndex,
            int offsetX,
            int offsetZ,
            int chunkSize,
            int chunkDensity,
            in int2 minWorldParcel,
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
            this.offsetX = offsetX;
            this.offsetZ = offsetZ;
            this.chunkSize = chunkSize;
            this.chunkDensity = chunkDensity;
            this.minWorldParcel = minWorldParcel;
            this.randoms = randoms;
            this.useRandomSpawnChance = useRandomSpawnChance;
            this.useValidations = useValidations;

            UP = new int2(0, 1);
            RIGHT = new int2(1, 0);
            DOWN = new int2(0, -1);
            LEFT = new int2(-1, 0);
        }

        public void Execute(int index)
        {
            int x = index / chunkDensity;
            int y = index % chunkDensity;

            float value = treeNoise[index];
            Random random = randoms[index];

            float3 randomness = treeRandomization.GetRandomizedPositionOffset(ref random) / chunkDensity;
            float3 positionWithinTheChunk = new float3((float)x / chunkDensity, 0, (float)y / chunkDensity) + randomness;
            float3 worldPosition = (positionWithinTheChunk * chunkSize) + new float3(offsetX, 0, offsetZ);
            int2 parcelCoord = WorldToParcelCoord(worldPosition);
            float3 parcelWorldPos = ParcelToWorld(parcelCoord);

            if (!(value > 0)) return;

            Vector2 randomScale = treeRandomization.randomScale;
            float scale = Mathf.Lerp(randomScale.x, randomScale.y, random.NextInt(0, 100) / 100f);

            if (useValidations)
            {
                if(!emptyParcelResult.TryGetValue(parcelCoord, out EmptyParcelNeighborData item)) return;

                float radius = treeRadius * scale * value;

                // We check nearby boundaries (there has to be a simpler way)
                bool u = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, UP, 0, radius);
                bool ur = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, UP + RIGHT, 0, radius);
                bool r = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, RIGHT, 0, radius);
                bool rd = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, RIGHT + DOWN, 0, radius);
                bool d = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, DOWN, 0, radius);
                bool dl = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, DOWN + LEFT, 0, radius);
                bool l = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, LEFT, 0, radius);
                bool lu = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, LEFT + UP, 0, radius);

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

        private int2 WorldToParcelCoord(float3 worldPos)
        {
            var parcelX = (int)math.floor(worldPos.x / 16f);
            var parcelZ = (int)math.floor(worldPos.z / 16f);
            return new int2(minWorldParcel.x + parcelX, minWorldParcel.y + parcelZ);
        }

        private float3 ParcelToWorld(int2 parcel)
        {
            int posX = (parcel.x - minWorldParcel.x) * 16;
            int posZ = (parcel.y - minWorldParcel.y) * 16;
            return new float3(posX, 0, posZ);
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
                var v3Dir = new Vector3(direction.x, 0, direction.y);
                Vector3 centerOfParcel = parcelWorldPos + new float3(8, 0, 8);
                Vector3 posToCheck = centerOfParcel + (v3Dir * 8f) + (depth * v3Dir * 16);

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
            if (dir.Equals(UP)) return item.UpHeight;
            if (dir.Equals(UP + RIGHT)) return item.UpRightHeight;
            if (dir.Equals(RIGHT)) return item.RightHeight;
            if (dir.Equals(RIGHT + DOWN)) return item.DownRightHeight;
            if (dir.Equals(DOWN)) return item.DownHeight;
            if (dir.Equals(DOWN + LEFT)) return item.DownLeftHeight;
            if (dir.Equals(LEFT)) return item.LeftHeight;
            if (dir.Equals(LEFT + UP)) return item.UpLeftHeight;
            return -1;
        }
    }
}
