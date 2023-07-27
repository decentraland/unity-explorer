#ifndef DCL_SKYBOX_DATA_INCLUDED
#define DCL_SKYBOX_DATA_INCLUDED

#include "UnityCG.cginc"
//#include "UnityCustomRenderTexture.cginc"
//#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"


struct sk_appdata
{
    uint vertexID : SV_VertexID;
    //UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct sk_v2f
{
    float4 vertex           : SV_POSITION;
    float3 localTexcoord    : TEXCOORD0;    // Texcoord local to the update zone (== globalTexcoord if no partial update zone is specified)
    float3 globalTexcoord   : TEXCOORD1;    // Texcoord relative to the complete custom texture
    uint primitiveID        : TEXCOORD2;    // Index of the update zone (correspond to the index in the updateZones of the Custom Texture)
    float3 direction        : TEXCOORD3;    // For cube textures, direction of the pixel being rendered in the cubemap


    //float4  pos             : SV_POSITION;
    // for HQ sun disk, we need vertex itself to calculate ray-dir per-pixel
    float3  vertexRayDir          : TEXCOORD4;

    // calculate sky colors in vprog
    half3   groundColor     : TEXCOORD5;
    half3   skyColor        : TEXCOORD6;
    half3   sunColor        : TEXCOORD7;

    //UNITY_VERTEX_OUTPUT_STEREO
};

#endif // DCL_SKYBOX_DATA_INCLUDED