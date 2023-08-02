#ifndef DCL_SKYBOX_VERTEX_INCLUDED
#define DCL_SKYBOX_VERTEX_INCLUDED

// Includes
#include "Assets/DCL_SkyBox_Data.hlsl"

// float4 _CurrentCubeFace;
// #define _Current_CubeFace _CurrentCubeFace.x
static const int _CubeFaceSTUFF = 5;
#define _Current_CubeFace _CubeFaceSTUFF


float3 ComputeCubeDirection(float2 globalTexcoord)
{
    float2 xy = (globalTexcoord * 2.0) - 1.0;
    float3 direction;

    if(_Current_CubeFace == 0.0f)
    {
        direction = (float3(1.0, -xy.y, -xy.x));
    }
    else if(_Current_CubeFace == 1.0f)
    {
        direction = (float3(-1.0, -xy.y, xy.x));
    }
    else if(_Current_CubeFace == 2.0f)
    {
        direction = (float3(xy.x, 1.0, xy.y));
    }
    else if(_Current_CubeFace == 3.0f)
    {
        direction = (float3(xy.x, -1.0, -xy.y));
    }
    else if(_Current_CubeFace == 4.0f)
    {
        direction = (float3(xy.x, -xy.y, 1.0));
    }
    else if(_Current_CubeFace == 5.0f)
    {
        direction = (float3(-xy.x, -xy.y, -1.0));
    }
    else
    {
        direction = float3(0, 0, 0);
    }

    return direction;
}

sk_v2f sk_vert(sk_appdata IN)
{
    sk_v2f OUT;

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
    // OUT.direction = OUT.globalTexcoord; // HACK_TEST
    // OUT.direction.xy = (OUT.direction.xy * 2.0) -1.0; // HACK_TEST
    // OUT.direction = float3(-OUT.direction.xy.x, -OUT.direction.xy.y, -1.0);
    OUT.direction = ComputeCubeDirection(OUT.globalTexcoord.xy);
    return OUT;
}

#endif // DCL_SKYBOX_VERTEX_INCLUDED