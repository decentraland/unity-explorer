Shader "DCL/ObjectHighlight/Output"
{
    HLSLINCLUDE
        #pragma editor_sync_compilation
    ENDHLSL

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

        Pass
        {
            Name "Highlight_Output"

            Blend SrcAlpha OneMinusSrcAlpha
            ZTest Off
            ZWrite Off
            Cull Off

            HLSLPROGRAM
                #include "ObjectHighlightOutput_Vert.hlsl"
                #include "ObjectHighlightOutput_Frag.hlsl"
                #pragma vertex hl_Output_vert
                #pragma fragment hl_Output_frag
                #pragma target 4.5
            ENDHLSL
        }
    }

    Fallback Off
}
