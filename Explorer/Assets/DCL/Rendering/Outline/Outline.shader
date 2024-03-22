Shader "Avatar/Outline"
{
    HLSLINCLUDE
        #pragma editor_sync_compilation
        #include "Outline_ToonRender.hlsl"
    ENDHLSL

    Properties
    {
    }

    SubShader
    {
        Tags
        {
            //"Queue"="Background"
            //"RenderType"="Background"
            //"PreviewType"="Skybox"
        }

        // 0 - OutlineRender
        Pass
        {
            Name "OutlineRender"

            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
                #pragma vertex ol_vert
                #pragma fragment ol_Render_frag
                #pragma target 4.5                
            ENDHLSL
        }
    }

    Fallback Off
}