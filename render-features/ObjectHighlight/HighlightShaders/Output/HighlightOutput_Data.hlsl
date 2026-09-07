#ifndef DCL_HIGHLIGHT_DATA_INCLUDED
#define DCL_HIGHLIGHT_DATA_INCLUDED

#include "UnityCG.cginc"

struct hl_appdata
{
    uint vertexID : SV_VertexID;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct hl_v2f
{
    float4 vertex           : SV_POSITION;
    float3 localTexcoord    : TEXCOORD0;    // Texcoord local to the update zone (== globalTexcoord if no partial update zone is specified)
    float3 globalTexcoord   : TEXCOORD1;    // Texcoord relative to the complete custom texture
    uint primitiveID        : TEXCOORD2;    // Index of the update zone (correspond to the index in the updateZones of the Custom Texture)

    UNITY_VERTEX_OUTPUT_STEREO
};

#endif // DCL_HIGHLIGHT_DATA_INCLUDED