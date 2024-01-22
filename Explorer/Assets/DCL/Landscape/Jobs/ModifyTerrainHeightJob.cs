using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.Landscape.Jobs
{
    /// <summary>
    /// TODO: DOCUMENT THIS JOB's PURPOSE
    /// </summary>
    [BurstCompile]
    public struct ModifyTerrainHeightJob : IJobParallelFor
    {
        private NativeArray<float> heights;
        [ReadOnly] private NativeHashMap<Vector2Int, EmptyParcelData> emptyParcelData;
        public int terrainWidth;
        public int offsetX;
        public int offsetZ;
        public int maxHeight;
        public float terrainScale;

        public ModifyTerrainHeightJob(ref NativeArray<float> heights, in NativeHashMap<Vector2Int, EmptyParcelData> emptyParcelData) : this()
        {
            this.heights = heights;
            this.emptyParcelData = emptyParcelData;
        }

        public void Execute(int index)
        {
            int x = index % terrainWidth;
            int z = index / terrainWidth;

            int worldX = x + offsetX; // * 1f / terrainScale;
            int worldZ = z + offsetZ; // * 1f / terrainScale;

            int parcelX = worldX / 16;
            int parcelZ = worldZ / 16;

            var coord = new Vector2Int(-150 + parcelX, -150 + parcelZ);

            if (emptyParcelData.TryGetValue(coord, out EmptyParcelData data))
            {
                float noise = Mathf.PerlinNoise((x + offsetX) * 1f / terrainScale, (z + offsetZ) * 1f / terrainScale);
                float currentHeight = data.minIndex;

                float lx = x % 16 / 16f;
                float lz = z % 16 / 16f;

                float lxRight = (lx - 0.5f) * 2;
                float lxLeft = lx * 2;

                float lzUp = (lz - 0.5f) * 2;
                float lzDown = lz * 2;

                float xLerp = lx >= 0.5f
                    ? math.lerp(currentHeight, data.rightHeight, lxRight)
                    : math.lerp(data.leftHeight, currentHeight, lxLeft);

                float zLerp = lz >= 0.5f
                    ? math.lerp(currentHeight, data.upHeight, lzUp)
                    : math.lerp(data.downHeight, currentHeight, lzDown);

                float corner = currentHeight;

                if (lx >= 0.5f && lz >= 0.5f) // up right
                    corner = math.min(
                        math.lerp(currentHeight, data.upRigthHeight, lxRight),
                        math.lerp(currentHeight, data.upRigthHeight, lzUp));

                if (lx < 0.5f && lz >= 0.5f) // up left
                    corner = math.min(
                        math.lerp(data.upLeftHeight, currentHeight, lxLeft),
                        math.lerp(currentHeight, data.upLeftHeight, lzUp));

                if (lx >= 0.5f && lz < 0.5f) // down right
                    corner = math.min(
                        math.lerp(currentHeight, data.downRightHeight, lxRight),
                        math.lerp(data.downRightHeight, currentHeight, lzDown));

                if (lx < 0.5f && lz < 0.5f) // down left
                    corner = math.min(
                        math.lerp(data.downLeftHeight, currentHeight, lxLeft),
                        math.lerp(data.downLeftHeight, currentHeight, lzDown));

                float finalHeight = math.max(math.max(corner, math.max(xLerp, zLerp)), currentHeight);
                heights[index] = finalHeight * noise / maxHeight;

                //heights[index] = currentHeight / maxHeight;
            }
            else
                heights[index] = 0;
        }
    }
}
