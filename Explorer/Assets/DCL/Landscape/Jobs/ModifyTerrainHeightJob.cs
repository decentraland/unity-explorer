using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DCL.Landscape.Jobs
{
    /// <summary>
    ///     This jobs checks every pixel from the terrain heightmap and set's up its noise value based on the EmptyParcelData as well
    ///     it also checks the sides and corners so the height variation blends in
    /// </summary>
    [BurstCompile]
    public struct ModifyTerrainHeightJob : IJobParallelFor
    {
        [ReadOnly] private readonly int terrainWidth;
        [ReadOnly] private readonly int offsetX;
        [ReadOnly] private readonly int offsetZ;
        [ReadOnly] private readonly int maxHeight;
        [ReadOnly] private readonly int2 minWorldParcel;
        [ReadOnly] private readonly int parcelSize;
        [ReadOnly] private readonly float edgeRadius;
        [ReadOnly] private readonly float minHeight;
        [ReadOnly] private readonly float pondDepth;
        private NativeArray<float> heights;
        [ReadOnly] private NativeArray<float> terrainNoise;
        [ReadOnly] private NativeParallelHashMap<int2, EmptyParcelNeighborData> emptyParcelNeighborData;
        [ReadOnly] private NativeParallelHashMap<int2, int> emptyParcelHeight;

        public ModifyTerrainHeightJob(
            ref NativeArray<float> heights,
            in NativeParallelHashMap<int2, EmptyParcelNeighborData> emptyParcelNeighborData,
            in NativeParallelHashMap<int2, int> emptyParcelHeight,
            in NativeArray<float> terrainNoise,
            float edgeRadius,
            float minHeight,
            float pondDepth,
            int resolution,
            int offsetX,
            int offsetZ,
            int maxHeightIndex,
            int2 minWorldParcel,
            int parcelSize) : this()
        {
            this.heights = heights;
            this.emptyParcelNeighborData = emptyParcelNeighborData;
            this.emptyParcelHeight = emptyParcelHeight;
            this.terrainNoise = terrainNoise;
            this.edgeRadius = edgeRadius;
            this.minHeight = minHeight;
            this.pondDepth = pondDepth;

            terrainWidth = resolution;
            this.offsetX = offsetX;
            this.offsetZ = offsetZ;
            maxHeight = maxHeightIndex;
            this.minWorldParcel = minWorldParcel;
            this.parcelSize = parcelSize;
        }

        public void Execute(int index)
        {
            float rMinHeight = minHeight / maxHeight;
            float radius = edgeRadius;

            int x = index % terrainWidth;
            int z = index / terrainWidth;

            int worldX = x + offsetX;
            int worldZ = z + offsetZ;

            int parcelX = worldX / parcelSize;
            int parcelZ = worldZ / parcelSize;

            var coord = new int2(minWorldParcel.x + parcelX, minWorldParcel.y + parcelZ);

            if (emptyParcelNeighborData.TryGetValue(coord, out EmptyParcelNeighborData data))
            {
                float noise = terrainNoise[index];
                float currentHeight = emptyParcelHeight[coord];

                // get the pixel position within the parcel coords
                float lx = x % parcelSize / (float)parcelSize;
                float lz = z % parcelSize / (float)parcelSize;

                float lxRight = (lx - radius) * 2;
                float lxLeft = lx * 2;

                float lzUp = (lz - radius) * 2;
                float lzDown = lz * 2;

                float xLerp = lx >= radius
                    ? math.lerp(currentHeight, data.RightHeight, lxRight)
                    : math.lerp(data.LeftHeight, currentHeight, lxLeft);

                float zLerp = lz >= radius
                    ? math.lerp(currentHeight, data.UpHeight, lzUp)
                    : math.lerp(data.DownHeight, currentHeight, lzDown);

                float corner = currentHeight;

                if (lx >= radius && lz >= radius) // up right
                    corner = math.min(
                        math.lerp(currentHeight, data.UpRightHeight, lxRight),
                        math.lerp(currentHeight, data.UpRightHeight, lzUp));

                if (lx < radius && lz >= radius) // up left
                    corner = math.min(
                        math.lerp(data.UpLeftHeight, currentHeight, lxLeft),
                        math.lerp(currentHeight, data.UpLeftHeight, lzUp));

                if (lx >= radius && lz < radius) // down right
                    corner = math.min(
                        math.lerp(currentHeight, data.DownRightHeight, lxRight),
                        math.lerp(data.DownRightHeight, currentHeight, lzDown));

                if (lx < radius && lz < radius) // down left
                    corner = math.min(
                        math.lerp(data.DownLeftHeight, currentHeight, lxLeft),
                        math.lerp(data.DownLeftHeight, currentHeight, lzDown));

                float finalHeight = math.max(math.max(corner, math.max(xLerp, zLerp)), currentHeight);

                if (noise < 0)
                    noise *= pondDepth;

                heights[index] = rMinHeight + (finalHeight * noise / maxHeight);
            }
            else
                heights[index] = rMinHeight;
        }
    }
}
