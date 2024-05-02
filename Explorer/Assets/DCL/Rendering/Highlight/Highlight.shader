Shader "DCL/Highlight"
{
    HLSLINCLUDE
        #pragma editor_sync_compilation
    ENDHLSL

    Properties
    {
//        _OutlineThickness ("Outline Thickness", Range(0,5)) = 1.0
//        _DepthSensitivity ("Depth Sensitivity", Range(0,5)) = 1.0
//        _NormalsSensitivity ("Normals Sensitivity", Range(0,5)) = 1.0
//        _ColorSensitivity ("Color Sensitivity", Range(0,5)) = 1.0
//        _OutlineColor ("Outline Color", Vector) = (0.0, 0.0, 0.0, 1.0)
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

        // 0 - Highlight Input
        Pass
        {
            Name "Highlight_Input"

            Blend SrcAlpha OneMinusSrcAlpha
            ZTest Off
            ZWrite Off
            Cull Off

            
            HLSLPROGRAM
                #include "Highlight_Vert.hlsl"
                #include "HighlightOutput_Frag.hlsl"
                #pragma vertex hl_vert
                #pragma fragment hl_Output_frag
                #pragma target 4.5                
            ENDHLSL
        }

//        // 1 - Highlight Output
//        Pass
//        {
//            Name "Highlight_Output"
//
//            ZTest Always
//            ZWrite Off
//            Cull Off
//
//            HLSLPROGRAM
//                #include "Highlight_Vert.hlsl"
//                #include "HighlightOutput_Frag.hlsl"
//                #pragma vertex hl_vert
//                #pragma fragment hl_Output_frag
//                #pragma target 4.5                
//            ENDHLSL
//        }
    }

    Fallback Off
}