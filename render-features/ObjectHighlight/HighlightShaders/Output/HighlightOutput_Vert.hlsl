#ifndef DCL_HIGHLIGHT_VERTEX_INCLUDED
#define DCL_HIGHLIGHT_VERTEX_INCLUDED

// Includes
#include "HighlightOutput_Data.hlsl"

hl_v2f hl_Output_vert(hl_appdata IN)
{
    hl_v2f OUT;

    #if UNITY_UV_STARTS_AT_TOP
        const float2 vertexPositions[3] =
        {
            { -1.0f,  3.0f },
            { -1.0f, -1.0f },
            {  3.0f, -1.0f }
        };

        const float2 texCoords[3] =
        {
            { 0.0f, -1.0f },
            { 0.0f, 1.0f },
            { 2.0f, 1.0f }
        };
    #else
        const float2 vertexPositions[3] =
        {
            {  3.0f,  3.0f },
            { -1.0f, -1.0f },
            { -1.0f,  3.0f }
        };

        const float2 texCoords[3] =
        {
            { 2.0f, 1.0f },
            { 0.0f, -1.0f },
            { 0.0f, 1.0f }
        };
    #endif

    uint primitiveID = IN.vertexID / 3;
    uint vertexID = IN.vertexID % 3;

    float2 pos = vertexPositions[vertexID];
    OUT.vertex = float4(pos, 0.0, 1.0);
    OUT.primitiveID = primitiveID;
    OUT.localTexcoord = float3(texCoords[vertexID], 0.0f);
    OUT.globalTexcoord = float3(pos.xy * 0.5 + 0.5, 1.0);
    #if UNITY_UV_STARTS_AT_TOP
        OUT.globalTexcoord.y = 1.0 - OUT.globalTexcoord.y;
    #endif
    return OUT;
}

#endif // DCL_HIGHLIGHT_VERTEX_INCLUDED