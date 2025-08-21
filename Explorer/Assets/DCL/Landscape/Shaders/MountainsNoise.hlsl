// #include "MountainsNoise.cs"

void Noise_float(float3 PositionIn, float ParcelSize, float4 TerrainBounds,
                 UnityTexture2D OccupancyMap, float HeightScale, float MinDistOccupancy, out float3 PositionOut, out float3 Normal)
{
    PositionOut.x = clamp(PositionIn.x, TerrainBounds.x, TerrainBounds.z);
    PositionOut.z = clamp(PositionIn.z, TerrainBounds.y, TerrainBounds.w);

    float2 uv = (PositionOut.xz * ParcelSize + OccupancyMap.texelSize.z * 0.5)
        * OccupancyMap.texelSize.x;

    float height = SAMPLE_TEXTURE2D_LOD(OccupancyMap, OccupancyMap.samplerstate, uv, 0.0).r;
    
    if (height <= MinDistOccupancy) 
    {
        PositionOut.y = 0.0;
        Normal = float3(0.0, 1.0, 0.0);
    }
    else 
    {
        float normalizedHeight = (height - MinDistOccupancy) / (1 - MinDistOccupancy);

        float saturationFactor = 20;
        float noiseH = 0; //GetHeight(PositionOut.x, PositionOut.z) * saturate( normalizedHeight * saturationFactor);

        PositionOut.y = normalizedHeight * HeightScale + noiseH;
        Normal =  float3(0.0, 1.0, 0.0); //GetNormal(PositionOut.x, PositionOut.z);

        // Ensure no negative heights
        if (PositionOut.y < 0.0)
        {
            PositionOut.y = 0.0;
            Normal = float3(0.0, 1.0, 0.0);
        }
    }
}
