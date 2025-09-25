Shader "DCL/CubeCopy"
{
    HLSLINCLUDE
        #include "UnityCG.cginc"
        #include "HLSLSupport.cginc"
    ENDHLSL

    Properties
    {
        _MainTex ("Main", CUBE) = "" {}
        _MipLevel ("Level", Float) = 0.0
        _Current_CubeFace ("Current_CubeFace", Float) = 1.0
    }
    SubShader
    {
        Tags
        {
            // "Queue"="Background"
            // "RenderType"="Background"
            // "PreviewType"="Skybox"
        }
        
        Pass
        {
            Name "DCL_CubeCopy"
            
            ZTest Off
            ZWrite Off
            Cull Off
            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma enable_d3d11_debug_symbols

                float _MipLevel;
                float _Current_CubeFace;
                
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

                struct appdata_copy
                {
                    uint vertexID : SV_VertexID;
                    //UNITY_VERTEX_INPUT_INSTANCE_ID
                };

                struct v2f {
                    // float4 pos : SV_POSITION;
                    // float4 uvw : TEXCOORD0;
                    float4 vertex           : SV_POSITION;
                    float3 localTexcoord    : TEXCOORD0;    // Texcoord local to the update zone (== globalTexcoord if no partial update zone is specified)
                    float3 globalTexcoord   : TEXCOORD1;    // Texcoord relative to the complete custom texture
                    uint primitiveID        : TEXCOORD2;    // Index of the update zone (correspond to the index in the updateZones of the Custom Texture)
                    float3 direction        : TEXCOORD3;    // For cube textures, direction of the pixel being rendered in the cubemap
                };

                v2f vert(appdata_copy IN)
                {
                    v2f OUT;
                    uint primitiveID = IN.vertexID / 3;
                    uint vertexID = IN.vertexID % 3;

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
                    // o.pos = UnityObjectToClipPos(v.vertex);
                    // o.uvw = v.texcoord;
                    // return o;
                }

                UNITY_DECLARE_TEXCUBE(_MainTex);
                //UNITY_DECLARE_TEX2D_NOSAMPLER(_MainTex);
                //SAMPLER(_MainTex_linear_clamp_sampler);
                samplerCUBE _MainTex_linear_clamp_sampler;
                #define UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(tex, dir,  lod) max(half4(0.0, 0.0, 0.0, 0.0), SAMPLE_TEXTURECUBE_LOD(tex, _MainTex_point_clamp_sampler, dir, lod)) 
                //#define UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(tex, dir,  lod) max(half4(0.0, 0.0, 0.0, 0.0), UNITY_SAMPLE_TEXCUBE_LOD(tex, dir, lod))
                
                float4 frag(v2f  IN) : SV_Target
                {
                    float3 uvw = IN.direction.xyz;
                    return UNITY_SAMPLE_TEXCUBE_LOD(_MainTex, uvw.xyz, _MipLevel);
                    //return SAMPLE_TEXTURECUBE_LOD(_MainTex, _MainTex_linear_clamp_sampler, uvw.xyz, _MipLevel);
                }
            ENDHLSL
        }
    }
}
