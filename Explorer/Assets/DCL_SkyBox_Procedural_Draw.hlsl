#ifndef DCL_SKYBOX_PROCEDURAL_DRAW
#define DCL_SKYBOX_PROCEDURAL_DRAW

#include "UnityCG.cginc"
#include "Lighting.cginc"

uniform half _Exposure;     // HDR exposure
uniform half3 _GroundColor;
uniform half _SunSize;
uniform half _SunSizeConvergence;
uniform half3 _SkyTint;
uniform half _AtmosphereThickness;

samplerCUBE _SkyBox_Cubemap_Texture;
half4 _Tex_HDR;
half4 _Tint;
//half _Exposure;
float _Rotation;

float3 RotateAroundYInDegrees (float3 vertex, float degrees)
{
    float alpha = degrees * UNITY_PI / 180.0;
    float sina, cosa;
    sincos(alpha, sina, cosa);
    float2x2 m = float2x2(cosa, -sina, sina, cosa);
    return float3(mul(m, vertex.xz), vertex.y).xzy;
}

struct appdata_t {
    float4 vertex : POSITION;
    float2 UV0    : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f {
    float4 vertex : SV_POSITION;
    float3 texcoord : TEXCOORD0;
    UNITY_VERTEX_OUTPUT_STEREO
};

v2f vert (appdata_t v)
{
    v2f o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float3 rotated = RotateAroundYInDegrees(v.vertex, _Rotation);
    //float3 rotated = RotateAroundYInDegrees(v.vertex, 45.0f);
    o.vertex = UnityObjectToClipPos(rotated);
    o.vertex.z = 0.0f;
    o.texcoord = normalize(mul(UNITY_MATRIX_I_V, normalize(v.vertex) ).xyz);
    return o;
}

fixed4 frag (v2f i) : SV_Target
{
    half4 tex = texCUBE (_SkyBox_Cubemap_Texture, i.texcoord);
    half3 c = DecodeHDR (tex, _Tex_HDR);
    c = c * _Tint.rgb * unity_ColorSpaceDouble.rgb;
    c *= _Exposure;
    return tex;
    return half4(c, 1);
}

#endif // DCL_SKYBOX_PROCEDURAL_DRAW