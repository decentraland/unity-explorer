using DCL.Landscape.Config;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace DCL.Landscape.Jobs
{
    /// <summary>
    /// TODO: DOCUMENT THIS JOB's PURPOSE
    /// </summary>
    [BurstCompile]
    public struct GenerateTreeInstancesJob : IJob
    {
        [ReadOnly] private NativeArray<float> treeNoise;
        private NativeList<TreeInstance> treeInstances;
        [ReadOnly] private NativeHashMap<Vector2Int, EmptyParcelData> emptyParcelResult;
        private ObjectRandomization treeRandomization;
        private readonly float treeRadius;
        private readonly int treeIndex;
        private readonly int offsetX;
        private readonly int offsetZ;
        private readonly int chunkSize;
        private readonly int chunkDensity;
        private Random random;

        public GenerateTreeInstancesJob(
            in NativeArray<float> treeNoise,
            ref NativeList<TreeInstance> treeInstances,
            in NativeHashMap<Vector2Int, EmptyParcelData> emptyParcelResult,
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
        }

        public void Execute()
        {
            for (int y = 0; y < chunkDensity; y++)
            {
                for (int x = 0; x < chunkDensity; x++)
                {
                    int index = x + (y * chunkDensity);
                    float value = treeNoise[index];

                    Vector3 randomness = treeRandomization.GetRandomizedPositionOffset(ref random) / chunkDensity;
                    Vector3 positionWithinTheChunk = new Vector3((float)x / chunkDensity, 0, (float)y / chunkDensity) + randomness;
                    Vector3 worldPosition = (positionWithinTheChunk * chunkSize) + new Vector3(offsetX, 0, offsetZ);
                    Vector2Int parcelCoord = WorldToParcelCoord(worldPosition);
                    Vector3 parcelWorldPos = ParcelToWorld(parcelCoord);

                    if (!(value > 0) || !emptyParcelResult.TryGetValue(parcelCoord, out EmptyParcelData item)) continue;

                    Vector2 randomScale = treeRandomization.randomScale;
                    float scale = Mathf.Lerp(randomScale.x, randomScale.y, random.NextInt(0, 100) / 100f);

                    float radius = treeRadius * scale;

                    bool u = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, Vector2Int.up, 0, radius);
                    bool ur = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, Vector2Int.up + Vector2Int.right, 0, radius);
                    bool r = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, Vector2Int.right, 0, radius);
                    bool rd = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, Vector2Int.right + Vector2Int.down, 0, radius);
                    bool d = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, Vector2Int.down, 0, radius);
                    bool dl = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, Vector2Int.down + Vector2Int.left, 0, radius);
                    bool l = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, Vector2Int.left, 0, radius);
                    bool lu = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, Vector2Int.left + Vector2Int.up, 0, radius);

                    if (!u || !ur || !r || !rd || !d || !dl || !l || !lu)
                        continue;

                    Vector2 randomRotation = treeRandomization.randomRotationY * Mathf.Deg2Rad;
                    float rotation = Mathf.Lerp(randomRotation.x, randomRotation.y, random.NextInt(0, 100) / 100f);

                    var treeInstance = new TreeInstance
                    {
                        position = positionWithinTheChunk,
                        prototypeIndex = treeIndex,
                        rotation = rotation,
                        widthScale = scale * value,
                        heightScale = scale * value,
                        color = Color.white,
                        lightmapColor = Color.white,
                    };

                    treeInstances.Add(treeInstance);
                }
            }
        }

        private Vector2Int WorldToParcelCoord(Vector3 worldPos)
        {
            int parcelX = Mathf.FloorToInt(worldPos.x / 16f);
            int parcelZ = Mathf.FloorToInt(worldPos.z / 16f);
            return new Vector2Int(-150 + parcelX, -150 + parcelZ);
        }

        private Vector3 ParcelToWorld(Vector2Int parcel)
        {
            int posX = (parcel.x + 150) * 16;
            int posZ = (parcel.y + 150) * 16;
            return new Vector3(posX, 0, posZ);
        }

        private bool CheckAssetPosition(EmptyParcelData item, Vector2Int currentParcel, Vector3 parcelWorldPos, Vector3 assetPosition, Vector2Int direction,
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
                Vector3 posToCheck = parcelWorldPos + (v3Dir * 8f) + (depth * v3Dir * 16);
                float distance = Vector3.Distance(assetPosition, posToCheck);
                return distance > radius;
            }

            return false;
        }

        private int GetHeightDirection(EmptyParcelData item, Vector2Int dir)
        {
            if (dir == Vector2Int.up) return item.upHeight;
            if (dir == Vector2Int.up + Vector2Int.right) return item.upRigthHeight;
            if (dir == Vector2Int.right) return item.rightHeight;
            if (dir == Vector2Int.right + Vector2Int.down) return item.downRightHeight;
            if (dir == Vector2Int.down) return item.downHeight;
            if (dir == Vector2Int.down + Vector2Int.left) return item.downLeftHeight;
            if (dir == Vector2Int.left) return item.leftHeight;
            if (dir == Vector2Int.left + Vector2Int.up) return item.upLeftHeight;
            return -1;
        }
    }
}
