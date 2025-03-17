Shader "DCL/SkyBox_Procedural_Generate"
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
        _MoonPos("Moon Position", Vector) = (-0.36, -0.4, 0.9, 1.0)
        _LightDir("Light Direction", Vector) = (-0.36, -0.4, 0.9, 1.0)
        _SunCol("Sun Colour", Color) = (1.0, 1.0, 1.0, 1.0)
        
        _kDefaultScatteringWavelength("kDefaultScatteringWavelength", Vector) = (0.65, 0.57, 0.475, 0.0)
        _kVariableRangeForScatteringWavelength("kVariableRangeForScatteringWavelength", Vector) = (0.15, 0.15, 0.15, 0.0)
        _OUTER_RADIUS("OUTER_RADIUS", Range(0,1)) = 1.025
        _kInnerRadius("kInnerRadius", Range(0,1)) = 1.0
        _kInnerRadius2("kInnerRadius2", Range(0,1)) =  1.0
        _kCameraHeight("kCameraHeight", Range(0,1)) =  0.000001
        _kRAYLEIGH_MAX ("kRAYLEIGH_MAX", Range(0,1)) = 0.0025
        _kRAYLEIGH_POW ("kRAYLEIGH_POW", Range(0,5)) = 2.5
        _kMIE("kMIE", Range(0,1)) = 0.0010
        _kSUN_BRIGHTNESS("kSUN_BRIGHTNESS", Range(0,1)) = 20.0
        _kMAX_SCATTER("kMAX_SCATTER", Range(0,1)) = 50.0
        _kHDSundiskIntensityFactor("kHDSundiskIntensityFactor", Range(0,1)) =  15.0
        _kSimpleSundiskIntensityFactor("kSimpleSundiskIntensityFactor", Range(0,1)) =  27.0
        _kSunScale_Multiplier("kSunScale_Multiplier", Range(0,1000)) = 400.0
        _kKm4PI_Multi("kKm4PI_Multi", Range(0,10)) = 4.0
        _kScaleDepth("kScaleDepth", Range(0,2)) = 0.25
        _kScaleOverScaleDepth_Multi("kScaleOverScaleDepth_Multi", Range(0,2)) = 0.25
        _kSamples("kSamples", Range(0,4)) = 2.0
        _MIE_G("MIE_G", Range(-2,2)) = -0.990
        _MIE_G2("MIE_G2", Range(0,2)) = 0.9801
        _SKY_GROUND_THRESHOLD("SKY_GROUND_THRESHOLD", Range(0,1)) = 0.02
    }

    HLSLINCLUDE
        #pragma editor_sync_compilation
        #pragma enable_d3d11_debug_symbols
        #include "./DCL_SkyBox_Vertex.hlsl"
        #include "./DCL_SkyBox_Fragment.hlsl"
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
                #pragma target 3.0
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
                #pragma target 3.0
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
                #pragma target 3.0
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
                #pragma target 3.0
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
                #pragma target 3.0
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
                #pragma target 3.0
                #pragma vertex sk_vert
                #pragma fragment sk_frag
            ENDHLSL
        }
    }
}
