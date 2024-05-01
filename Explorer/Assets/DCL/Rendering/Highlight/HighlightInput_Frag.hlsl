#ifndef DCL_HIGHLIGHT_INPUT_FRAGMENT_INCLUDED
#define DCL_HIGHLIGHT_INPUT_FRAGMENT_INCLUDED

// Includes
#include "Assets/DCL/Rendering/Highlight/Highlight_Data.hlsl"
#include "Assets/DCL/Rendering/Highlight/Highlight.hlsl"
#include "UnityCG.cginc"

float _OutlineThickness;
float _DepthSensitivity;
float _NormalsSensitivity;
float _ColorSensitivity;
float4 _OutlineColor;

float4 hl_Input_frag(hl_v2f IN) : SV_Target
{
    float4 vCol = float4(1.0, 0.0, 0.0, 1.0);

    // Outline_float(IN.localTexcoord,
    //     _OutlineThickness,
    //     _DepthSensitivity,
    //     _NormalsSensitivity,
    //     _ColorSensitivity,
    //     _OutlineColor,
    //     vCol);

    return vCol;
}

#endif // DCL_HIGHLIGHT_INPUT_FRAGMENT_INCLUDED