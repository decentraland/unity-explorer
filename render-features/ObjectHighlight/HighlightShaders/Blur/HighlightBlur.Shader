Shader "DCL/HighlightInput_Blur"
{
    Properties
    {

    }
    
    SubShader
    {
        Blend One Zero

        // Highlight Blur - Horizontal
        Pass
        {
            Name "Highlight Blur - Horizontal"

            ZWrite Off
            ZTest Always
            
            HLSLPROGRAM
                #pragma target 4.5
                #pragma vertex hl_blur_vert
                #pragma fragment hl_blur_frag
                #pragma enable_d3d11_debug_symbols
                #include "UnityCG.cginc"

                struct sk_appdata
                {
                    uint vertexID : SV_VertexID;
                };

                struct sk_v2f
                {
                    float4 vertex           : SV_POSITION;
                    float3 localTexcoord    : TEXCOORD0;    // Texcoord local to the update zone (== globalTexcoord if no partial update zone is specified)
                    float3 globalTexcoord   : TEXCOORD1;    // Texcoord relative to the complete custom texture
                    uint primitiveID        : TEXCOORD2;    // Index of the update zone (correspond to the index in the updateZones of the Custom Texture)
                };

                sk_v2f hl_blur_vert(sk_appdata IN)
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
                    return OUT;
                }

                UNITY_DECLARE_TEX2D(_HighlightTexture);
                static const float offset[3] = {0.0, 1.3846153846, 3.2307692308};
                static const float weight[3] = {0.2270270270, 0.3162162162, 0.0702702703};
                
                float4 hl_blur_frag(sk_v2f IN) : SV_Target
                {
                    float4 vFragOut = UNITY_SAMPLE_TEX2D(_HighlightTexture, IN.localTexcoord).rgba * weight[0];
                    for (int i=1; i<3; i++)
                    {
                        vFragOut += UNITY_SAMPLE_TEX2D(_HighlightTexture, IN.localTexcoord + float3(offset[i] / _ScreenParams.x, 0.0f, 0.0f)).rgba * weight[i];
                        vFragOut += UNITY_SAMPLE_TEX2D(_HighlightTexture, IN.localTexcoord - float3(offset[i] / _ScreenParams.x, 0.0f, 0.0f)).rgba * weight[i];
                    }
                    return vFragOut;
                }
            ENDHLSL
        }

// Highlight Blur - Vertical
        Pass
        {
            Name "Highlight Blur - Vertical"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            
            HLSLPROGRAM
                #pragma target 4.5
                #pragma vertex hl_blur_vert
                #pragma fragment hl_blur_frag
                #pragma enable_d3d11_debug_symbols
                #include "UnityCG.cginc"

                struct sk_appdata
                {
                    uint vertexID : SV_VertexID;
                };

                struct sk_v2f
                {
                    float4 vertex           : SV_POSITION;
                    float3 localTexcoord    : TEXCOORD0;    // Texcoord local to the update zone (== globalTexcoord if no partial update zone is specified)
                    float3 globalTexcoord   : TEXCOORD1;    // Texcoord relative to the complete custom texture
                    uint primitiveID        : TEXCOORD2;    // Index of the update zone (correspond to the index in the updateZones of the Custom Texture)
                };

                sk_v2f hl_blur_vert(sk_appdata IN)
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
                    return OUT;
                }

                UNITY_DECLARE_TEX2D(_HighlightTexture);
                static const float offset[3] = {0.0, 1.3846153846, 3.2307692308};
                static const float weight[3] = {0.2270270270, 0.3162162162, 0.0702702703};
                
                float4 hl_blur_frag(sk_v2f IN) : SV_Target
                {
                    float4 vFragOut = UNITY_SAMPLE_TEX2D(_HighlightTexture, IN.localTexcoord).rgba * weight[0];
                    for (int i=1; i<3; i++)
                    {
                        vFragOut += UNITY_SAMPLE_TEX2D(_HighlightTexture, IN.localTexcoord + float3(0.0, offset[i] / _ScreenParams.y, 0.0f)).rgba * weight[i];
                        vFragOut += UNITY_SAMPLE_TEX2D(_HighlightTexture, IN.localTexcoord - float3(0.0, offset[i] / _ScreenParams.y, 0.0f)).rgba * weight[i];
                    }
                    return vFragOut;
                }
            ENDHLSL
        }
    }
}
