Shader "CustomRenderTexture/SkyBox_Procedural_Generate"
{
    Properties
    {
        _SkyTint ("Sky Tint", Color) = (.5, .5, .5, 1)
        _GroundColor ("Ground", Color) = (.369, .349, .341, 1)
        _SunSize ("Sun Size", Range(0,1)) = 0.04
        _SunSizeConvergence("Sun Size Convergence", Range(1,10)) = 5
        _AtmosphereThickness ("Atmosphere Thickness", Range(0,5)) = 1.0
        _Exposure("Exposure", Range(0, 8)) = 1.3
    }

    HLSLINCLUDE
        #pragma editor_sync_compilation
        #include "Assets/DCL_SkyBox_Vertex.hlsl"
        #include "Assets/DCL_SkyBox_Fragment.hlsl"
    ENDHLSL

    SubShader
    {
        Blend One Zero

        // 0 - PosX - Right
        Pass
        {
            Name "SkyBox_Procedural_Generate_Right"

            HLSLPROGRAM
                #pragma target 5.0
                #pragma vertex sk_vert
                #pragma fragment sk_frag             
            ENDHLSL
        }

        // 1 - NegX - Left
        Pass
        {
            Name "SkyBox_Procedural_Generate_Left"

            HLSLPROGRAM
                #pragma target 5.0
                #pragma vertex sk_vert
                #pragma fragment sk_frag          
            ENDHLSL
        }

        // 2 - PosY - Up
        Pass
        {
            Name "SkyBox_Procedural_Generate_Up"

            HLSLPROGRAM
                #pragma target 5.0
                #pragma vertex sk_vert
                #pragma fragment sk_frag
            ENDHLSL
        }

        // 3 - NegY - Down
        Pass
        {
            Name "SkyBox_Procedural_Generate_Down"

            HLSLPROGRAM
                #pragma target 5.0
                #pragma vertex sk_vert
                #pragma fragment sk_frag
            ENDHLSL
        }

        // 4 - PosZ - Front
        Pass
        {
            Name "SkyBox_Procedural_Generate_Front"

            HLSLPROGRAM
                #pragma target 5.0
                #pragma vertex sk_vert
                #pragma fragment sk_frag
            ENDHLSL
        }

        // 5 - NegZ - Back
        Pass
        {
            Name "SkyBox_Procedural_Generate_Back"

            HLSLPROGRAM
                #pragma target 5.0
                #pragma vertex sk_vert
                #pragma fragment sk_frag
            ENDHLSL
        }
    }
}
