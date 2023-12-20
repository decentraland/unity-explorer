Shader "Grass/DCL_Grass"
{
    Properties
    {
        [MainColor]   _BaseColor("Base Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Grass"

            ZTest Greater
            ZWrite On
            Cull Back

            HLSLPROGRAM
                #pragma target 4.5
                #pragma vertex vert
                #pragma fragment frag
                
                #pragma editor_sync_compilation
                #pragma enable_d3d11_debug_symbols
                //#pragma multi_compile _ _FORWARD_PLUS
                #pragma multi_compile_instancing
                #pragma instancing_options renderinglayer
                #pragma multi_compile _ DOTS_INSTANCING_ON
                #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
                
                //#include "UnityCG.cginc"
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

                CBUFFER_START(UnityPerMaterial)
                    float4 _BaseMap_ST;
                    half4 _BaseColor;
                    half4 _SpecColor;
                    half4 _EmissionColor;
                    half _Cutoff;
                    half _Surface;
                CBUFFER_END
                
                #ifdef UNITY_DOTS_INSTANCING_ENABLED
                    UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                        UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
                        UNITY_DOTS_INSTANCED_PROP(float4, _SpecColor)
                        UNITY_DOTS_INSTANCED_PROP(float4, _EmissionColor)
                        UNITY_DOTS_INSTANCED_PROP(float , _Cutoff)
                        UNITY_DOTS_INSTANCED_PROP(float , _Surface)
                    UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
                
                    #define _BaseColor          UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4 , _BaseColor)
                    #define _SpecColor          UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4 , _SpecColor)
                    #define _EmissionColor      UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4 , _EmissionColor)
                    #define _Cutoff             UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float  , _Cutoff)
                    #define _Surface            UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float  , _Surface)
                #endif
                
                struct sk_appdata
                {
                    uint vertexID : SV_VertexID;
                    UNITY_VERTEX_INPUT_INSTANCE_ID
                };

                struct sk_v2f
                {
                    float4 vertex           : SV_POSITION;
                    float3 localTexcoord    : TEXCOORD0;    // Texcoord local to the update zone (== globalTexcoord if no partial update zone is specified)
                    float3 globalTexcoord   : TEXCOORD1;    // Texcoord relative to the complete custom texture
                    uint primitiveID        : TEXCOORD2;    // Index of the update zone (correspond to the index in the updateZones of the Custom Texture)
                    float3 direction        : TEXCOORD3;    // For cube textures, direction of the pixel being rendered in the cubemap
                    UNITY_VERTEX_INPUT_INSTANCE_ID
                    UNITY_VERTEX_OUTPUT_STEREO
                };


                sk_v2f vert(sk_appdata IN)
                {
                    sk_v2f OUT;

                    UNITY_SETUP_INSTANCE_ID(IN);
                    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

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
                    //OUT.direction = ComputeCubeDirection(OUT.globalTexcoord.xy);
                    return OUT;
                }

                float4 frag(sk_v2f IN) : SV_Target
                {
                    UNITY_SETUP_INSTANCE_ID(IN);
                    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                    return half4(1.0, 0.0, 0.0, 1.0);
                }
            ENDHLSL
        }
    }

    Fallback Off
}