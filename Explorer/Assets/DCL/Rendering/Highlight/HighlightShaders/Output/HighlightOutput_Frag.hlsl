#ifndef DCL_HIGHLIGHT_RENDER_FRAGMENT_INCLUDED
#define DCL_HIGHLIGHT_RENDER_FRAGMENT_INCLUDED

// Includes
#include "HighlightOutput_Data.hlsl"

UNITY_DECLARE_TEX2D(_HighlightTexture);

float4 hl_Output_frag(hl_v2f IN) : SV_Target
{
    return half4(UNITY_SAMPLE_TEX2D(_HighlightTexture, IN.localTexcoord).rgba);

    float2 uv = IN.localTexcoord;
    float2 texelSize = float2(1.0f / (_ScreenParams.x * 0.5f), 1.0f / (_ScreenParams.y * 0.5f));
    float4 o = texelSize.xyxy * float2(-0.5f , 0.5f).xxyy;
    half4 s =   UNITY_SAMPLE_TEX2D(_HighlightTexture, uv + o.xy) +
                UNITY_SAMPLE_TEX2D(_HighlightTexture, uv + o.zy) +
                UNITY_SAMPLE_TEX2D(_HighlightTexture, uv + o.xw) +
                UNITY_SAMPLE_TEX2D(_HighlightTexture, uv + o.zw);
    return s * 0.5f;
}

#endif // DCL_HIGHLIGHT_RENDER_FRAGMENT_INCLUDED