#ifndef DCL_OUTLINE_RENDER_FRAGMENT_INCLUDED
#define DCL_OUTLINE_RENDER_FRAGMENT_INCLUDED

// Includes
#include "Assets/DCL/Rendering/Outline/Outline_Data.hlsl"

UNITY_DECLARE_TEX2D(_OutlineTexture);

float4 ol_Render_frag(ol_v2f IN) : SV_Target
{
    return half4(UNITY_SAMPLE_TEX2D(_OutlineTexture, IN.localTexcoord).rgb, 1.0);
}

#endif // DCL_OUTLINE_RENDER_FRAGMENT_INCLUDED