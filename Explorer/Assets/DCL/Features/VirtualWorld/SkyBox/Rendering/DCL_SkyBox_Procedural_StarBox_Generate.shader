Shader "DCL/StarBox_Procedural_Generate"
{
    Properties
    {

    }

    HLSLINCLUDE
        #pragma editor_sync_compilation
        #pragma enable_d3d11_debug_symbols
        #include "./DCL_StarBox_Vertex.hlsl"
        #include "./DCL_StarBox_Fragment.hlsl"
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
            Name "StarBox_Procedural_Generate_Right"

            HLSLPROGRAM
                #pragma multi_compile _CUBEMAP_FACE_RIGHT // shader variant approach to fix Unity Issue: 1621458
                #pragma target 3.0
                #pragma vertex st_vert
                #pragma fragment st_frag             
            ENDHLSL
        }

        // 1 - NegX - Left
        Pass
        {
            Name "StarBox_Procedural_Generate_Left"

            HLSLPROGRAM
                #pragma multi_compile _CUBEMAP_FACE_LEFT // shader variant approach to fix Unity Issue: 1621458
                #pragma target 3.0
                #pragma vertex st_vert
                #pragma fragment st_frag          
            ENDHLSL
        }

        // 2 - PosY - Up
        Pass
        {
            Name "StarBox_Procedural_Generate_Up"

            HLSLPROGRAM
                #pragma multi_compile _CUBEMAP_FACE_UP // shader variant approach to fix Unity Issue: 1621458
                #pragma target 3.0
                #pragma vertex st_vert
                #pragma fragment st_frag
            ENDHLSL
        }

        // 3 - NegY - Down
        Pass
        {
            Name "StarBox_Procedural_Generate_Down"

            HLSLPROGRAM

                #pragma multi_compile _CUBEMAP_FACE_DOWN // shader variant approach to fix Unity Issue: 1621458
                #pragma target 3.0
                #pragma vertex st_vert
                #pragma fragment st_frag
            ENDHLSL
        }

        // 4 - PosZ - Front
        Pass
        {
            Name "StarBox_Procedural_Generate_Front"

            HLSLPROGRAM
                #pragma multi_compile _CUBEMAP_FACE_FRONT // shader variant approach to fix Unity Issue: 1621458
                #pragma target 3.0
                #pragma vertex st_vert
                #pragma fragment st_frag
            ENDHLSL
        }

        // 5 - NegZ - Back
        Pass
        {
            Name "StarBox_Procedural_Generate_Back"

            HLSLPROGRAM
                #pragma multi_compile _CUBEMAP_FACE_BACK // shader variant approach to fix Unity Issue: 1621458
                #pragma target 3.0
                #pragma vertex st_vert
                #pragma fragment st_frag
            ENDHLSL
        }
    }
}
