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
        [ReadOnly] private readonly int resolution;
        [ReadOnly] private readonly int2 chunkMinParcel;
        [ReadOnly] private readonly int maxHeight;
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
            int2 chunkMinParcel,
            int maxHeightIndex,
            int parcelSize) : this()
        {
            this.heights = heights;
            this.emptyParcelNeighborData = emptyParcelNeighborData;
            this.emptyParcelHeight = emptyParcelHeight;
            this.terrainNoise = terrainNoise;
            this.edgeRadius = edgeRadius;
            this.minHeight = minHeight;
            this.pondDepth = pondDepth;

            this.resolution = resolution;
            maxHeight = maxHeightIndex;
            this.chunkMinParcel = chunkMinParcel;
            this.parcelSize = parcelSize;
        }

        public void Execute(int index)
        {
            float rMinHeight = minHeight / maxHeight;
            float radius = edgeRadius;

            int x = index % resolution;
            int z = index / resolution;

            int parcelX = chunkMinParcel.x + (x / parcelSize);
            int parcelZ = chunkMinParcel.y + (z / parcelSize);

            var parcel = new int2(parcelX, parcelZ);

            if (emptyParcelNeighborData.TryGetValue(parcel, out EmptyParcelNeighborData data))
            {
                float noise = terrainNoise[index];
                float currentHeight = emptyParcelHeight[parcel];

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
