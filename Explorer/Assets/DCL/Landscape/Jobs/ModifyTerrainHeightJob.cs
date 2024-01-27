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
        [ReadOnly] private NativeArray<float> terrainNoise;
        [ReadOnly] private NativeHashMap<int2, EmptyParcelData> emptyParcelData;
        public int terrainWidth;
        public int offsetX;
        public int offsetZ;
        public int maxHeight;
        public float edgeRadius;
        private float minHeight;
        private float pondDepth;

        public ModifyTerrainHeightJob(
            ref NativeArray<float> heights,
            in NativeHashMap<int2, EmptyParcelData> emptyParcelData,
            in NativeArray<float> terrainNoise,
            float edgeRadius,
            float minHeight,
            float pondDepth) : this()
        {
            this.heights = heights;
            this.emptyParcelData = emptyParcelData;
            this.terrainNoise = terrainNoise;
            this.edgeRadius = edgeRadius;
            this.minHeight = minHeight;
            this.pondDepth = pondDepth;
        }

        public void Execute(int index)
        {
            float rMinHeight = minHeight / maxHeight;
            var radius = edgeRadius;

            int x = index % terrainWidth;
            int z = index / terrainWidth;

            int worldX = x + offsetX; // * 1f / terrainScale;
            int worldZ = z + offsetZ; // * 1f / terrainScale;

            int parcelX = worldX / 16;
            int parcelZ = worldZ / 16;

            var coord = new int2(-150 + parcelX, -150 + parcelZ);

            if (emptyParcelData.TryGetValue(coord, out EmptyParcelData data))
            {
                //float noise = Mathf.PerlinNoise((x + offsetX) * 1f / terrainScale, (z + offsetZ) * 1f / terrainScale);
                float noise = terrainNoise[index];
                float currentHeight = data.minIndex;

                // get the pixel position within the parcel coords
                float lx = x % 16 / 16f;
                float lz = z % 16 / 16f;

                float lxRight = (lx - radius) * 2;
                float lxLeft = lx * 2;

                float lzUp = (lz - radius) * 2;
                float lzDown = lz * 2;

                float xLerp = lx >= radius
                    ? math.lerp(currentHeight, data.rightHeight, lxRight)
                    : math.lerp(data.leftHeight, currentHeight, lxLeft);

                float zLerp = lz >= radius
                    ? math.lerp(currentHeight, data.upHeight, lzUp)
                    : math.lerp(data.downHeight, currentHeight, lzDown);

                float corner = currentHeight;

                if (lx >= radius && lz >= radius) // up right
                    corner = math.min(
                        math.lerp(currentHeight, data.upRigthHeight, lxRight),
                        math.lerp(currentHeight, data.upRigthHeight, lzUp));

                if (lx < radius && lz >= radius) // up left
                    corner = math.min(
                        math.lerp(data.upLeftHeight, currentHeight, lxLeft),
                        math.lerp(currentHeight, data.upLeftHeight, lzUp));

                if (lx >= radius && lz < radius) // down right
                    corner = math.min(
                        math.lerp(currentHeight, data.downRightHeight, lxRight),
                        math.lerp(data.downRightHeight, currentHeight, lzDown));

                if (lx < radius && lz < radius) // down left
                    corner = math.min(
                        math.lerp(data.downLeftHeight, currentHeight, lxLeft),
                        math.lerp(data.downLeftHeight, currentHeight, lzDown));

                float finalHeight = math.max(math.max(corner, math.max(xLerp, zLerp)), currentHeight);

                if (noise < 0)
                {
                    noise *= pondDepth;
                }
                heights[index] = rMinHeight + (finalHeight * noise / maxHeight);

                //heights[index] = currentHeight / maxHeight;
            }
            else
                heights[index] = rMinHeight;
        }
    }
}
