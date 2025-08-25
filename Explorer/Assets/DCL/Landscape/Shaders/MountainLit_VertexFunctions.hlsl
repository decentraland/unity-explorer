#ifndef MOUNTAIN_LIT_VERTEX_FUNCTIONS_INCLUDED
#define MOUNTAIN_LIT_VERTEX_FUNCTIONS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "GeoffNoise.hlsl"
//#include "PerlinNoise.hlsl"

VertexPositionInputs GetVertexPositionInputs_Mountain(float3 positionOS, float4 terrainBounds, out float fOccupancy, out float4 heightDerivative)
{
    heightDerivative = float4(0.0f, 0.0f, 0.0f, 0.0f);
    VertexPositionInputs input;
    input.positionWS = TransformObjectToWorld(positionOS);
    input.positionWS = ClampPosition(input.positionWS, terrainBounds);

    float2 heightUV = (input.positionWS.xz + 4096.0f) / 8192.0f;
    float heightDerivative2 = SAMPLE_TEXTURE2D_LOD(_HeightMap, sampler_HeightMap, heightUV, 0).x;
    fOccupancy = SAMPLE_TEXTURE2D_LOD(_OccupancyMap, sampler_OccupancyMap, heightUV, 0).r;

    float minValue = 175.0 / 255.0;

    if (fOccupancy <= minValue)
    {
        // Flat surface (occupied parcels and above minValue threshold)
        input.positionWS.y = 0.0;
    }
    else
    {
        // Calculate normalized height first
        float normalizedHeight = (fOccupancy - minValue) / (1 - minValue);

        float noiseH = GetHeight(input.positionWS.x, input.positionWS.z);
        float noiseIntensity = lerp(0.0f, 0.5f, normalizedHeight);

        input.positionWS.y += normalizedHeight * _DistanceFieldScale;// + noiseH * noiseIntensity;
        heightDerivative.x = heightDerivative2;

        // Ensure no negative heights
        if (input.positionWS.y < 0.0)
        {
            input.positionWS.y = 0.0;
        }
    }

    input.positionVS = TransformWorldToView(input.positionWS);
    input.positionCS = TransformWorldToHClip(input.positionWS);

    float4 ndc = input.positionCS * 0.5f;
    input.positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
    input.positionNDC.zw = input.positionCS.zw;

    return input;
}

#endif // MOUNTAIN_LIT_VERTEX_FUNCTIONS_INCLUDED
