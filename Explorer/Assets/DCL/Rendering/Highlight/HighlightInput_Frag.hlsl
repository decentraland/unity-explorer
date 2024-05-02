#ifndef DCL_HIGHLIGHT_INPUT_FRAGMENT_INCLUDED
#define DCL_HIGHLIGHT_INPUT_FRAGMENT_INCLUDED

// Includes
#include "Assets/DCL/Rendering/Highlight/Highlight_Data.hlsl"
#include "Assets/DCL/Rendering/Highlight/Highlight.hlsl"
#include "UnityCG.cginc"

float3 _HighlightColour;

float4 hl_Input_frag(hl_v2f IN) : SV_Target
{
    float4 vCol = float4(_HighlightColour.rgb, 1.0);
    
    return vCol;
}

#endif // DCL_HIGHLIGHT_INPUT_FRAGMENT_INCLUDED