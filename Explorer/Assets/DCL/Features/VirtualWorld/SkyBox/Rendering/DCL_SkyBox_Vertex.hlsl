#ifndef DCL_SKYBOX_VERTEX_INCLUDED
#define DCL_SKYBOX_VERTEX_INCLUDED

// Includes
#include "./DCL_SkyBox_Data.hlsl"

// Due to an issue on AMD GPUs this doesn't work as expected so instead we moved to
// a shader variant system. If fixed or work around from Unity is created then
// switch to this look up to reduce shader variants
// https://support.unity.com/hc/requests/1621458
/*
float4 _CurrentCubeFace;
#define _Current_CubeFace _CurrentCubeFace.x
*/

float3 ComputeCubeDirection(float2 globalTexcoord)
{
    float2 xy = (globalTexcoord * 2.0) - 1.0;

    // Due to an issue on AMD GPUs this doesn't work as expected so instead we moved to
    // a shader variant system. If fixed or work around from Unity is created then
    // switch to this look up to reduce shader variants
    // https://support.unity.com/hc/requests/1621458
    /*
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
    */

    #if defined(_CUBEMAP_FACE_RIGHT)
        return float3(1.0, -xy.y, -xy.x);
    #elif defined(_CUBEMAP_FACE_LEFT)
        return float3(-1.0, -xy.y, xy.x);
    #elif defined(_CUBEMAP_FACE_UP)
        return float3(xy.x, 1.0, xy.y);
    #elif defined(_CUBEMAP_FACE_DOWN)
        return float3(xy.x, -1.0, -xy.y);
    #elif defined(_CUBEMAP_FACE_FRONT)
        return float3(xy.x, -xy.y, 1.0);
    #elif defined(_CUBEMAP_FACE_BACK)
        return float3(-xy.x, -xy.y, -1.0);
    #else
        return float3(0, 0, 0);
    #endif
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
    OUT.direction = ComputeCubeDirection(OUT.globalTexcoord.xy);
    return OUT;
}

#endif // DCL_SKYBOX_VERTEX_INCLUDED