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
    float2 uv0 : TEXCOORD0;
    float3 normalDir : TEXCOORD1;
    float3 tangentDir : TEXCOORD2;
    float3 bitangentDir : TEXCOORD3;

    UNITY_VERTEX_OUTPUT_STEREO
};

VertexOutput vert_highlight (VertexInput v)
{
    VertexOutput o = (VertexOutput)0;

    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    o.uv0 = v.texcoord0;
    float4 objPos = mul ( unity_ObjectToWorld, float4(0,0,0,1) );
    float2 Set_UV0 = o.uv0;
    float4 _Outline_Sampler_var = float4(1,1,1,1);//tex2Dlod(_Outline_Sampler,float4(TRANSFORM_TEX(Set_UV0, _Outline_Sampler),0.0,0));
    //v.2.0.4.3 baked Normal Texture for Outline

    #ifdef _DCL_COMPUTE_SKINNING
    o.normalDir = UnityObjectToWorldNormal(_GlobalAvatarBuffer[_lastAvatarVertCount + _lastWearableVertCount + v.index].normal.xyz);
    float4 skinnedTangent = _GlobalAvatarBuffer[_lastAvatarVertCount + _lastWearableVertCount + v.index].tangent;
    o.tangentDir = normalize( mul( unity_ObjectToWorld, float4( skinnedTangent.xyz, 0.0 ) ).xyz );
    o.bitangentDir = normalize(cross(o.normalDir, o.tangentDir) * skinnedTangent.w);
    #else
    o.normalDir = UnityObjectToWorldNormal(v.normal);
    o.tangentDir = normalize( mul( unity_ObjectToWorld, float4( v.tangent.xyz, 0.0 ) ).xyz );
    o.bitangentDir = normalize(cross(o.normalDir, o.tangentDir) * v.tangent.w);
    #endif
    
    float3x3 tangentTransform = float3x3( o.tangentDir, o.bitangentDir, o.normalDir);
    //UnpackNormal() can't be used, and so as follows. Do not specify a bump for the texture to be used.
    float4 _BakedNormal_var = (float4(1,1,1,1) * 2 - 1);//(tex2Dlod(_BakedNormal,float4(TRANSFORM_TEX(Set_UV0, _BakedNormal),0.0,0)) * 2 - 1);
    float3 _BakedNormalDir = normalize(mul(_BakedNormal_var.rgb, tangentTransform));
    //end
    float Set_Outline_Width = (_Outline_Width*0.001*smoothstep( _Farthest_Distance, _Nearest_Distance, distance(objPos.rgb,_WorldSpaceCameraPos) )*_Outline_Sampler_var.rgb).r;
    Set_Outline_Width *= (1.0f - _ZOverDrawMode);

    float4 _ClipCameraPos = mul(UNITY_MATRIX_VP, float4(_WorldSpaceCameraPos.xyz, 1));
    
    #if defined(UNITY_REVERSED_Z)
        _Offset_Z = _Offset_Z * -0.01;
    #else
        _Offset_Z = _Offset_Z * 0.01;
    #endif
    
    Set_Outline_Width = Set_Outline_Width*50;
    float signVar = dot(normalize(v.vertex.xyz),normalize(v.normal))<0 ? -1 : 1;
    float4 vertOffset = _HighlightObjectOffset;
    //vertOffset = float4(0.0f, 0.0f, 0.0f, 0.0f);
    #ifdef _DCL_COMPUTE_SKINNING
        float4 vVert = float4(_GlobalAvatarBuffer[_lastAvatarVertCount + _lastWearableVertCount + v.index].position.xyz, 1.0f);
        o.pos = UnityObjectToClipPos(float4(vVert.xyz + signVar*normalize(vVert - vertOffset)*Set_Outline_Width, 1));
    #else
        o.pos = UnityObjectToClipPos(float4(v.vertex.xyz + signVar*normalize(v.vertex)*Set_Outline_Width, 1));
    #endif

    o.pos.z = o.pos.z + _Offset_Z * _ClipCameraPos.z;
    return o;
}

float4 frag_highlight(VertexOutput i) : SV_Target
{
    return _HighlightColour + float4(1.0f, 1.0f, 1.0f, 0.0f);
}