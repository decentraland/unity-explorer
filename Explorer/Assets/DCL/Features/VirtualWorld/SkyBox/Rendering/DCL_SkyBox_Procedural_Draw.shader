Shader "DCL/DCL_SkyBox_Procedural_Draw"
{
    HLSLINCLUDE
        #pragma editor_sync_compilation
        #pragma enable_d3d11_debug_symbols
        #include "./DCL_SkyBox_Procedural_Draw.hlsl"
    ENDHLSL

    Properties
    {
        
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

        // 0 - Scenery
        Pass
        {
            Name "SkyBox_Procedural_Draw_Scenery"

            ZTest LEqual
            ZWrite Off
            Cull Off

            HLSLPROGRAM
                #pragma vertex vert_space
                #pragma fragment frag_space
                #pragma target 3.0                
            ENDHLSL
        }

        // 1 - StarBox
        Pass
        {
            Name "SkyBox_Procedural_Draw_StarBox"
            
            ZTest LEqual
            ZWrite Off
            Cull Off

            HLSLPROGRAM
                #pragma vertex vert_stars
                #pragma fragment frag_stars
                #pragma target 3.0                
            ENDHLSL
        }

        // 2 - SkyBox
        Pass
        {
            Name "SkyBox_Procedural_Draw_SkyBox"

            ZTest LEqual
            ZWrite Off
            Cull Off
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma target 3.0                
            ENDHLSL
        }
    }

    Fallback Off
}