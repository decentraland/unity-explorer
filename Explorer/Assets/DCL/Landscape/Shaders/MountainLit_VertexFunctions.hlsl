#ifndef MOUNTAIN_LIT_VERTEX_FUNCTIONS_INCLUDED
#define MOUNTAIN_LIT_VERTEX_FUNCTIONS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "GeoffNoise.hlsl"
//#include "PerlinNoise.hlsl"

// Use existing material property from MountainLit.shader
int _UseHeightMap; // 0 = use noise, 1 = use HeightMap
float _HeightMapScale; // 0 = use noise, 1 = use HeightMap

VertexPositionInputs GetVertexPositionInputs_Mountain(float3 positionOS, float4 terrainBounds, out float fOccupancy)
{
    VertexPositionInputs input;
    input.positionWS = TransformObjectToWorld(positionOS);
    input.positionWS = ClampPosition(input.positionWS, terrainBounds);

    // Convert extent to parcels and compute pow2 size in pixels (parcels), with 1px border on each side
    float maxExtent = max(max(abs(terrainBounds.x), abs(terrainBounds.z)), max(abs(terrainBounds.y), abs(terrainBounds.w)));
    float occupancyMapSize = exp2(ceil(log2(2.0 * maxExtent / _ParcelSize + 2.0)));
    float2 occupancyUV = ((input.positionWS.xz / _ParcelSize) + occupancyMapSize * 0.5f) / occupancyMapSize;

    // IMPORTANT: Should be aligned with CPU TerrainGenerator.CreateOccupancyMap()/GetParcelNoiseHeight() and ScatterFunctions.cginc
    fOccupancy = SAMPLE_TEXTURE2D_LOD(_OccupancyMap, sampler_OccupancyMap, occupancyUV, 0).r;

    float minValue = _MinDistOccupancy;

    if (fOccupancy <= minValue)
    {
        // Flat surface (occupied parcels and above minValue threshold)
        input.positionWS.y = 0.0;
    }
    else
    {
        float2 heightUV = (input.positionWS.xz + 4096.0f) / 8192.0f;

        /// Value taken from generating HeightMap via TerrainGeneratorWithAnalysis. 
        float min = -4.135159f; // min value of the GeoffNoise.GetHeight
        float range = 8.236154f; // (max - min) of the GeoffNoise.GetHeight
        float saturationFactor = 20.f;
        
        float fHeightMapValue = SAMPLE_TEXTURE2D_LOD(_HeightMap, sampler_HeightMap, heightUV, 0).x;

        // the result from the heightmap should be equal to this function
        // float noiseH = GetHeight(input.positionWS.x, input.positionWS.z);
        float noiseH = fHeightMapValue * range + min;

        // Calculate normalized height first
        float normalizedHeight = (fOccupancy - minValue) / (1.0f - minValue);
        input.positionWS.y = max(0.0f, normalizedHeight * _DistanceFieldScale) + (noiseH * saturate( normalizedHeight * saturationFactor));
    }

    input.positionVS = TransformWorldToView(input.positionWS);
    input.positionCS = TransformWorldToHClip(input.positionWS);

    float4 ndc = input.positionCS * 0.5f;
    input.positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
    input.positionNDC.zw = input.positionCS.zw;

    return input;
}

#endif // MOUNTAIN_LIT_VERTEX_FUNCTIONS_INCLUDED
