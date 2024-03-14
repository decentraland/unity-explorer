#ifndef DCL_OUTLINE_TOON_RENDER_INCLUDED
#define DCL_OUTLINE_TOON_RENDER_INCLUDED

#include "UnityCG.cginc"

UNITY_DECLARE_TEX2D(_OutlineTexture);

struct ol_appdata
{
    uint vertexID : SV_VertexID;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct ol_v2f
{
    float4 vertex           : SV_POSITION;
    float3 localTexcoord    : TEXCOORD0;    // Texcoord local to the update zone (== globalTexcoord if no partial update zone is specified)
    float3 globalTexcoord   : TEXCOORD1;    // Texcoord relative to the complete custom texture
    uint primitiveID        : TEXCOORD2;    // Index of the update zone (correspond to the index in the updateZones of the Custom Texture)

    UNITY_VERTEX_OUTPUT_STEREO
};

ol_v2f ol_vert(ol_appdata IN)
{
    ol_v2f OUT;

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

float4 ol_Render_frag(ol_v2f IN) : SV_Target
{
    return half4(UNITY_SAMPLE_TEX2D(_OutlineTexture, IN.localTexcoord).rgb, 1.0);
}

#endif // DCL_OUTLINE_VERTEX_INCLUDED