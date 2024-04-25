Shader "Avatar/Outline"
{
    HLSLINCLUDE
        #pragma editor_sync_compilation
        #include "Outline_Vert.hlsl"
        #include "Outline_Render_Frag.hlsl"
        #include "Outline_Draw_Frag.hlsl"
    ENDHLSL

    Properties
    {
        _OutlineThickness ("Outline Thickness", Range(0,5)) = 1.0
        _DepthSensitivity ("Depth Sensitivity", Range(0,5)) = 1.0
        _NormalsSensitivity ("Normals Sensitivity", Range(0,5)) = 1.0
        _ColorSensitivity ("Color Sensitivity", Range(0,5)) = 1.0
        _OutlineColor ("Outline Color", Vector) = (0.0, 0.0, 0.0, 1.0)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Background"
            "RenderType"="Background"
            "PreviewType"="Skybox"
        }
        Cull Off
        ZWrite Off

        // 0 - OutlineRender
        Pass
        {
            Name "OutlineRender"

            ZTest LEqual
            ZWrite Off
            Cull Off

            HLSLPROGRAM
                #pragma vertex ol_vert
                #pragma fragment ol_Draw_frag
                #pragma target 3.0                
            ENDHLSL
        }

        // 1 - OutlineDraw
        Pass
        {
            Name "OutlineDraw"

            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
                #pragma vertex ol_vert
                #pragma fragment ol_Render_frag
                #pragma target 3.0                
            ENDHLSL
        }
    }

    Fallback Off
}