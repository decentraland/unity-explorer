#ifndef DCL_HIGHLIGHT_RENDER_FRAGMENT_INCLUDED
#define DCL_HIGHLIGHT_RENDER_FRAGMENT_INCLUDED

// Includes
#include "HighlightOutput_Data.hlsl"

UNITY_DECLARE_TEX2D(_HighlightTexture);

float4 hl_Output_frag(hl_v2f IN) : SV_Target
{
    return half4(UNITY_SAMPLE_TEX2D(_HighlightTexture, IN.localTexcoord).rgba);
}

#endif // DCL_HIGHLIGHT_RENDER_FRAGMENT_INCLUDED