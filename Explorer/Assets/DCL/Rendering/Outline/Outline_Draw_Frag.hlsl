#ifndef DCL_OUTLINE_DRAW_FRAGMENT_INCLUDED
#define DCL_OUTLINE_DRAW_FRAGMENT_INCLUDED

// Includes
#include "Assets/DCL/Rendering/Outline/Outline_Data.hlsl"
#include "Assets/DCL/Rendering/Outline/Outline.hlsl"
#include "UnityCG.cginc"

float _OutlineThickness;
float _DepthSensitivity;
float _NormalsSensitivity;
float _ColorSensitivity;
float4 _OutlineColor;

float4 ol_Draw_frag(ol_v2f IN) : SV_Target
{
    float4 vCol = float4(0.0, 0.0, 0.0, 0.0);

    Outline_float(IN.localTexcoord,
        _OutlineThickness,
        _DepthSensitivity,
        _NormalsSensitivity,
        _ColorSensitivity,
        _OutlineColor,
        vCol);

    return vCol;
}

#endif // DCL_OUTLINE_DRAW_FRAGMENT_INCLUDED