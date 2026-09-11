uniform float4 _LightColor0; // this is not set in c# code ?

#ifdef _DCL_COMPUTE_SKINNING
// Skinning structure
struct VertexInfo
{
    float3 position;
    float3 normal;
    float4 tangent;
};
StructuredBuffer<VertexInfo> _GlobalAvatarBuffer;
#endif

float4 _Highlight_Colour;
float _Highlight_Width;

struct VertexInput
{
    uint index : SV_VertexID;
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
    float2 texcoord0 : TEXCOORD0;

    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VertexOutput
{
    float4 pos : SV_POSITION;
    float4 positionCS : TEXCOORD4;

    UNITY_VERTEX_OUTPUT_STEREO
};

VertexOutput vert_highlight (VertexInput v)
{
    VertexOutput o = (VertexOutput)0;

    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    float4 objPos = mul ( unity_ObjectToWorld, float4(0,0,0,1) );

    float4 vVert;
    float3 vNormal;
    float4 vTangent;
    float3 normalDir;
    float4 skinnedTangent;
    float3 tangentDir;
    float3 bitangentDir;

    float Set_Outline_Width = _Highlight_Width;
    int lastWearableVertCount = _lastWearableVertCount;
    int lastAvatarVertCount = _lastAvatarVertCount;

    #ifdef _DCL_COMPUTE_SKINNING
        vVert = float4(_GlobalAvatarBuffer[lastAvatarVertCount + lastWearableVertCount + v.index].position.xyz, 1.0f);
        vNormal = _GlobalAvatarBuffer[lastAvatarVertCount + lastWearableVertCount + v.index].normal.xyz;
        normalDir = UnityObjectToWorldNormal(vNormal);
        skinnedTangent = _GlobalAvatarBuffer[lastAvatarVertCount + lastWearableVertCount + v.index].tangent;
        tangentDir = normalize( mul( unity_ObjectToWorld, float4( skinnedTangent.xyz, 0.0 ) ).xyz );
        bitangentDir = normalize(cross(normalDir, tangentDir) * skinnedTangent.w);

        float4 clipPosition = mul(UNITY_MATRIX_VP, mul(unity_ObjectToWorld, float4(vVert.xyz, 1.0)));
        float3 clipNormal = mul((float3x3) UNITY_MATRIX_VP, mul((float3x3) UNITY_MATRIX_M, vNormal));

        float2 offset = normalize(clipNormal.xy) / _ScreenParams.xy * Set_Outline_Width * clipPosition.w * 2.0f;
        clipPosition.xy += offset;
        
        o.pos = clipPosition;
        return o;
    #else
        vVert = v.vertex;
        vNormal = v.normal;
        vTangent = v.tangent;
        normalDir = UnityObjectToWorldNormal(vNormal);
        tangentDir = normalize( mul( unity_ObjectToWorld, float4( vTangent.xyz, 0.0 ) ).xyz );
        bitangentDir = normalize(cross(normalDir, tangentDir) * vTangent.w);

        float2 offset = normalize(normalDir.xy) / _ScreenParams.xy * Set_Outline_Width * vVert.w * 2.0f;
        vVert.xy += offset;
        
        o.pos = vVert;
        return o;
    #endif
}

float4 frag_highlight(VertexOutput i) : SV_Target
{
    Dithering(_FadeDistance, i.positionCS, _EndFadeDistance, _StartFadeDistance);
    return _Highlight_Colour;
}