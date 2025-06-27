#include "MountainsNoise.cs"

void Noise_float(float3 PositionIn, float ParcelSize, float4 TerrainBounds, UnityTexture2D OccupancyMap,
    out float3 PositionOut, out float3 Normal)
{
    PositionOut.x = clamp(PositionIn.x, TerrainBounds.x, TerrainBounds.y);
    PositionOut.z = clamp(PositionIn.z, TerrainBounds.z, TerrainBounds.w);

    // The occupancy map has a 1 pixel border around the terrain.
    float2 scale = float2(1.0 / (TerrainBounds.y - TerrainBounds.x + ParcelSize * 2.0),
        1.0 / (TerrainBounds.w - TerrainBounds.z + ParcelSize * 2.0));

    float occupancy = SAMPLE_TEXTURE2D_LOD(OccupancyMap, OccupancyMap.samplerstate,
        (PositionOut.xz - TerrainBounds.xz + ParcelSize) * scale, 0.0).r;

    // In the "worst case", if occupancy is 0.25, it can mean that the current vertex is on a corner
    // between one occupied parcel and three free ones, and height must be zero.
    if (occupancy < 0.25)
    {
        float height = GetHeight(PositionOut.x, PositionOut.z);
        PositionOut.y = lerp(height, 0.0, occupancy * 4.0);
        Normal = GetNormal(PositionOut.x, PositionOut.z);
    }
    else
    {
        PositionOut.y = 0.0;
        Normal = float3(0.0, 1.0, 0.0);
    }
}
