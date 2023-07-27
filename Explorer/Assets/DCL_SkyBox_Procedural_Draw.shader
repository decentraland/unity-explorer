Shader "Skybox/DCL_SkyBox_Procedural_Draw"
{
    HLSLINCLUDE
        #pragma editor_sync_compilation
        #include "Assets/DCL_SkyBox_Procedural_Draw.hlsl"
    ENDHLSL

    Properties
    {
        //_SkyBox_Cubemap_Texture ("Cubemap", CUBE) = "" {}
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
            //Blend One SrcAlpha, Zero One
            //BlendOp Add, Add

            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma target 3.0                
            ENDHLSL
        }

        // 1 - SkyBox
        Pass
        {
            Name "SkyBox_Procedural_Draw_SkyBox"

            ZTest Less
            ZWrite Off
            Cull Off
            //Blend One SrcAlpha, Zero One
            //BlendOp Add, Add

            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma target 3.0                
            ENDHLSL
        }
    }

    Fallback Off
    //CustomEditor "SkyboxProceduralShaderGUI"
}