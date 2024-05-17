Shader "DCL/HighlightInput_Blooming"
{
    Properties
    {

    }
    
    SubShader
    {
        Blend One Zero

        // Highlight Blooming - Downscale
        Pass
        {
            Name "Highlight Blooming - Downscale"

            ZWrite Off
            ZTest Always
            
            HLSLPROGRAM
				#include "UnityCG.cginc"
                #include "HighlightBlooming.hlsl"

                #pragma target 4.5
                #pragma vertex hl_blur_vert
                #pragma fragment hl_blur_frag
                #pragma enable_d3d11_debug_symbols
                
                UNITY_DECLARE_TEX2D(_HighlightTexture);
                
                float4 hl_blur_frag(sk_v2f IN) : SV_Target
                {
                    //float4 vFragOut = UNITY_SAMPLE_TEX2D(_HighlightTexture, IN.localTexcoord);
                    //return vFragOut;

                    float2 uv = IN.localTexcoord;
                    float2 texelSize = float2(1.0f / (_ScreenParams.x * 0.5f), 1.0f / (_ScreenParams.y * 0.5f));
			        float4 o = texelSize.xyxy * float2(-1.0f, 1.0f).xxyy;
			        half4 s =   UNITY_SAMPLE_TEX2D(_HighlightTexture, uv + o.xy) +
			                    UNITY_SAMPLE_TEX2D(_HighlightTexture, uv + o.zy) +
                                UNITY_SAMPLE_TEX2D(_HighlightTexture, uv + o.xw) +
                                UNITY_SAMPLE_TEX2D(_HighlightTexture, uv + o.zw);
			        return s * 0.25f;
                }
            ENDHLSL
        }

		// Highlight Blooming - Upscale
        Pass
        {
            Name "Highlight Blooming - Upscale"

            ZWrite Off
            ZTest Always
            
            HLSLPROGRAM
				#include "UnityCG.cginc"
                #include "HighlightBlooming.hlsl"

                #pragma target 4.5
                #pragma vertex hl_blur_vert
                #pragma fragment hl_blur_frag
                #pragma enable_d3d11_debug_symbols
                
                UNITY_DECLARE_TEX2D(_HighlightTexture);
                
                float4 hl_blur_frag(sk_v2f IN) : SV_Target
                {
                    //float4 vFragOut = UNITY_SAMPLE_TEX2D(_HighlightTexture, IN.localTexcoord);
                    //return vFragOut;

                    float2 uv = IN.localTexcoord;
                    float2 texelSize = float2(1.0f / (_ScreenParams.x * 0.5f), 1.0f / (_ScreenParams.y * 0.5f));
			        float4 o = texelSize.xyxy * float2(-0.5f , 0.5f).xxyy;
			        half4 s =   UNITY_SAMPLE_TEX2D(_HighlightTexture, uv + o.xy) +
			                    UNITY_SAMPLE_TEX2D(_HighlightTexture, uv + o.zy) +
                                UNITY_SAMPLE_TEX2D(_HighlightTexture, uv + o.xw) +
                                UNITY_SAMPLE_TEX2D(_HighlightTexture, uv + o.zw);
			        return s * 0.25f;
                }
            ENDHLSL
        }

		// Highlight Blooming - Combine
        Pass
        {
            Name "Highlight Blooming - Combine"

			Blend One One
            ZWrite Off
            ZTest Always
            
            HLSLPROGRAM
				#include "UnityCG.cginc"
				#include "HighlightBlooming.hlsl"
                #pragma target 4.5
                #pragma vertex hl_blur_vert
                #pragma fragment hl_blur_frag
                #pragma enable_d3d11_debug_symbols

                UNITY_DECLARE_TEX2D(_HighlightTexture);
                
                float4 hl_blur_frag(sk_v2f IN) : SV_Target
                {
                    float2 uv = IN.localTexcoord;
					float4 colourOutput = UNITY_SAMPLE_TEX2D(_HighlightTexture, uv);
					colourOutput.a *= 1.0f;
					return colourOutput;
                }
            ENDHLSL
        }
    }
}
