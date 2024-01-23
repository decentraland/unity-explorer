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
    /// This job gathers the treeNoise result from other job and does additional checks like:
    ///     - Is there a neighbour owned parcel that overlaps with my asset radius?
    ///     - Is there another asset of the same type within my radius?
    /// </summary>

    // TODO: Convert NativeHashMap into NativeArray's so we can use IJobParallelFor
    [BurstCompile]
    public struct GenerateTreeInstancesJob : IJob
    {
        [ReadOnly] private NativeArray<float> treeNoise;
        private NativeHashMap<int2, TreeInstance> treeInstances;
        [ReadOnly] private NativeHashMap<int2, EmptyParcelData> emptyParcelResult;
        [ReadOnly] private ObjectRandomization treeRandomization;
        [ReadOnly] private readonly float treeRadius;
        [ReadOnly] private readonly int treeIndex;
        [ReadOnly] private readonly int offsetX;
        [ReadOnly] private readonly int offsetZ;
        [ReadOnly] private readonly int chunkSize;
        [ReadOnly] private readonly int chunkDensity;
        private Random random;

        private readonly int2 UP;
        private readonly int2 RIGHT;
        private readonly int2 DOWN;
        private readonly int2 LEFT;

        public GenerateTreeInstancesJob(
            in NativeArray<float> treeNoise,
            ref NativeHashMap<int2, TreeInstance> treeInstances,
            in NativeHashMap<int2, EmptyParcelData> emptyParcelResult,
            in ObjectRandomization treeRandomization,
            float treeRadius,
            int treeIndex,
            int offsetX,
            int offsetZ,
            int chunkSize,
            int chunkDensity,
            ref Random random)
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
            this.random = random;

            UP = new int2(0, 1);
            RIGHT = new int2(1, 0);
            DOWN = new int2(0, -1);
            LEFT = new int2(-1, 0);
        }

        public void Execute()
        {
            var bailOut = false;
            var count = 0;
            for (int y = 0; y < chunkDensity; y++)
            {
                for (int x = 0; x < chunkDensity; x++)
                {
                    if (bailOut)
                        break;

                    int index = x + (y * chunkDensity);
                    float value = treeNoise[index];

                    float3 randomness = treeRandomization.GetRandomizedPositionOffset(ref random) / chunkDensity;
                    float3 positionWithinTheChunk = new float3((float)x / chunkDensity, 0, (float)y / chunkDensity) + randomness;
                    float3 worldPosition = (positionWithinTheChunk * chunkSize) + new float3(offsetX, 0, offsetZ);
                    int2 parcelCoord = WorldToParcelCoord(worldPosition);
                    float3 parcelWorldPos = ParcelToWorld(parcelCoord);

                    if (!(value > 0) || !emptyParcelResult.TryGetValue(parcelCoord, out EmptyParcelData item)) continue;

                    Vector2 randomScale = treeRandomization.randomScale;
                    float scale = Mathf.Lerp(randomScale.x, randomScale.y, random.NextInt(0, 100) / 100f);

                    float radius = treeRadius * scale * value;

                    bool u = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, UP, 0, radius);
                    bool ur = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, UP + RIGHT, 0, radius);
                    bool r = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, RIGHT, 0, radius);
                    bool rd = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, RIGHT + DOWN, 0, radius);
                    bool d = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, DOWN, 0, radius);
                    bool dl = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, DOWN + LEFT, 0, radius);
                    bool l = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, LEFT, 0, radius);
                    bool lu = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, LEFT + UP, 0, radius);

                    if (!u || !ur || !r || !rd || !d || !dl || !l || !lu)
                        continue;

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

                    // we check the chances of this object to spawn
                    bool canAssetSpawn = random.NextFloat(value * 100, 100) > 80;

                    if (canAssetSpawn)
                    {
                        var intRadius = (int)math.ceil(radius);
                        var isValid = true;

                        isValid = CheckAssetSpatialAvailability(intRadius, x, y);

                        if (!isValid)
                            continue;

                        treeInstances.Add(new int2(x, y), treeInstance);
                        count++;
                    }

                    //if (count > 3)

                    //    bailOut = true;
                }
            }
        }

        // Jobs do not support nested collections and doing a quadtree would be too much work,
        // since landscape assets spawn on fixed positions,
        // we can check those positions around a radius to see if we can spawn another
        private bool CheckAssetSpatialAvailability(int intRadius, int x, int y)
        {
            var isValid = true;

            for (int i = -intRadius / 2; i < intRadius * 2; i++)
            {
                if (!isValid)
                    break;

                for (int j = -intRadius / 2; j < intRadius * 2; j++)
                {
                    if (!isValid)
                        break;

                    var pointer = new int2(x + i, y + j);

                    if (treeInstances.ContainsKey(pointer))

                        // this means that we have an object too near us
                        isValid = false;
                }
            }

            return isValid;
        }

        private int2 WorldToParcelCoord(float3 worldPos)
        {
            var parcelX = (int)math.floor(worldPos.x / 16f);
            var parcelZ = (int)math.floor(worldPos.z / 16f);
            return new int2(-150 + parcelX, -150 + parcelZ);
        }

        private float3 ParcelToWorld(int2 parcel)
        {
            int posX = (parcel.x + 150) * 16;
            int posZ = (parcel.y + 150) * 16;
            return new float3(posX, 0, posZ);
        }

        // We check the boundaries of our object to see if it can spawn based on the neighbor scenes
        private bool CheckAssetPosition(EmptyParcelData item, int2 currentParcel, float3 parcelWorldPos, float3 assetPosition, int2 direction,
            int depth, float radius)
        {
            if (GetHeightDirection(item, direction) >= 0)
            {
                int nextDepth = depth + 1;

                if (emptyParcelResult.TryGetValue(currentParcel + (direction * nextDepth), out EmptyParcelData parcel))
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

        private int GetHeightDirection(EmptyParcelData item, int2 dir)
        {
            if (dir.Equals(UP)) return item.upHeight;
            if (dir.Equals(UP + RIGHT)) return item.upRigthHeight;
            if (dir.Equals(RIGHT)) return item.rightHeight;
            if (dir.Equals(RIGHT + DOWN)) return item.downRightHeight;
            if (dir.Equals(DOWN)) return item.downHeight;
            if (dir.Equals(DOWN + LEFT)) return item.downLeftHeight;
            if (dir.Equals(LEFT)) return item.leftHeight;
            if (dir.Equals(LEFT + UP)) return item.upLeftHeight;
            return -1;
        }
    }
}
