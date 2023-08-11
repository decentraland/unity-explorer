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
        _SunPos("Sun Position", Vector) = (-0.36, -0.4, 0.9, 1.0)
        _SunCol("Sun Colour", Vector) = (1.0, 1.0, 1.0, 1.0)
    }

    HLSLINCLUDE
        #pragma editor_sync_compilation
        #include "Assets/DCL_SkyBox_Vertex.hlsl"
        #include "Assets/DCL_SkyBox_Fragment.hlsl"
    ENDHLSL

    SubShader
    {
        Blend One Zero

        // Due to an issue on AMD GPUs this doesn't work as expected so instead we moved to
        // a shader variant system. If fixed or work around from Unity is created then
        // switch to this look up to reduce shader variants
        // https://support.unity.com/hc/requests/1621458

        // 0 - PosX - Right
        Pass
        {
            Name "SkyBox_Procedural_Generate_Right"

            HLSLPROGRAM
                #pragma multi_compile _CUBEMAP_FACE_RIGHT // shader variant approach to fix Unity Issue: 1621458
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
                #pragma multi_compile _CUBEMAP_FACE_LEFT // shader variant approach to fix Unity Issue: 1621458
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
                #pragma multi_compile _CUBEMAP_FACE_UP // shader variant approach to fix Unity Issue: 1621458
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

                #pragma multi_compile _CUBEMAP_FACE_DOWN // shader variant approach to fix Unity Issue: 1621458
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
                #pragma multi_compile _CUBEMAP_FACE_FRONT // shader variant approach to fix Unity Issue: 1621458
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
                #pragma multi_compile _CUBEMAP_FACE_BACK // shader variant approach to fix Unity Issue: 1621458
                #pragma target 5.0
                #pragma vertex sk_vert
                #pragma fragment sk_frag
            ENDHLSL
        }
    }
}
