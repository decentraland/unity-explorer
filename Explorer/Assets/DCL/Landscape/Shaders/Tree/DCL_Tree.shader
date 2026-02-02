// Shader targeted for low end devices. Single Pass Forward Rendering.
Shader "DCL/Tree"
{
    // Keep properties of StandardSpecular shader for upgrade reasons.
    Properties
    {
        [MainTexture] _BaseMap("Base Map (RGB) Smoothness / Alpha (A)", 2D) = "white" {}
        [MainColor]   _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        _Cutoff("Alpha Clipping", Range(0.0, 1.0)) = 0.5

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _SpecColor("Specular Color", Color) = (0.5, 0.5, 0.5, 0.5)
        _SpecGlossMap("Specular Map", 2D) = "white" {}
        _SmoothnessSource("Smoothness Source", Float) = 0.0
        _SpecularHighlights("Specular Highlights", Float) = 1.0
        
        _RequiresWind("Requires Wind", Float) = 0.0
        // Wind settings
        _WindSpeed ("Wind Speed", Range(0, 5)) = 0.1
        _WindStrength ("Wind Strength", Range(0, 1)) = 0.1
        _WindFrequency ("Wind Frequency", Range(0, 10)) = 0.1
        _WindTurbulence ("Wind Turbulence", Range(0, 2)) = 0.2
        _WindDirection ("Wind Direction", Vector) = (1, 0, 0.5, 0)
        
        // Flutter settings (individual leaf movement)
        _FlutterSpeed ("Flutter Speed", Range(0, 20)) = 1.0
        _FlutterStrength ("Flutter Strength", Range(0, 0.5)) = 0.05
        _FlutterFrequency ("Flutter Frequency", Range(0, 50)) = 1.0

        [HideInInspector] _BumpScale("Scale", Float) = 1.0
        [NoScaleOffset] _BumpMap("Normal Map", 2D) = "bump" {}

        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0)
        [NoScaleOffset]_EmissionMap("Emission Map", 2D) = "white" {}

        // Blending state
        _Surface("__surface", Float) = 0.0
        _Blend("__blend", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _BlendModePreserveSpecular("_BlendModePreserveSpecular", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0
        [HideInInspector] _AddPrecomputedVelocity("_AddPrecomputedVelocity", Float) = 0.0
        [HideInInspector] _XRMotionVectorsPass("_XRMotionVectorsPass", Float) = 1.0

        [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0
        // Editmode props
        _QueueOffset("Queue offset", Float) = 0.0

        // ObsoleteProperties
        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _Shininess("Smoothness", Float) = 0.0
        [HideInInspector] _GlossinessSource("GlossinessSource", Float) = 0.0
        [HideInInspector] _SpecSource("SpecularHighlights", Float) = 0.0

        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "SimpleLit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            // -------------------------------------
            // Render State Commands
            // Use same blending / depth states as Standard shader
            Blend[_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            ZWrite[_ZWrite]
            Cull[_Cull]
            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex LitPassVertexSimple
            #pragma fragment LitPassFragmentSimple

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            //#pragma shader_feature_local_fragment _EMISSION
            //#pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            //#pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            //#pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON _ALPHAMODULATE_ON
            //#pragma shader_feature_local_fragment _ _SPECGLOSSMAP _SPECULAR_COLOR
            #pragma shader_feature_local_fragment _ _SPECULAR_COLOR
            //#pragma shader_feature_local_fragment _GLOSSINESS_FROM_BASE_ALPHA

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            //#pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            //#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            //#pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            //#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            //#pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            //#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            //#pragma multi_compile_fragment _ _LIGHT_COOKIES
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            //#pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            //#pragma multi_compile _ DYNAMICLIGHTMAP_ON
            //#pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            //#pragma multi_compile_fragment _ DEBUG_DISPLAY
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            //--------------------------------------
            // GPU Instancing
            //#pragma multi_compile_instancing
            //#pragma instancing_options renderinglayer
            //#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            
            //--------------------------------------
            // Defines
            #define BUMP_SCALE_NOT_SUPPORTED 1

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include_with_pragmas "Packages/com.gurbu.gpui-pro/Runtime/Shaders/Include/GPUInstancerSetup.hlsl"
            #pragma instancing_options procedural:setupGPUI
            #pragma multi_compile_instancing
            
            // -------------------------------------
            // Includes
            #include "DCL_Tree_Input.hlsl"
            #include "DCL_Tree_ForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON
            //#pragma shader_feature_local_fragment _GLOSSINESS_FROM_BASE_ALPHA

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // This is used during shadow map generation to differentiate between directional and punctual light shadows, as they use different formulas to apply Normal Bias
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include_with_pragmas "Packages/com.gurbu.gpui-pro/Runtime/Shaders/Include/GPUInstancerSetup.hlsl"
            #pragma instancing_options procedural:setupGPUI
            #pragma multi_compile_instancing
            
            // -------------------------------------
            // Includes
            #include "DCL_Tree_Input.hlsl"
            #include "DCL_Tree_ShadowCasterPass.hlsl"
            ENDHLSL
        }
        
        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ColorMask R
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON
            //#pragma shader_feature_local_fragment _GLOSSINESS_FROM_BASE_ALPHA

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include_with_pragmas "Packages/com.gurbu.gpui-pro/Runtime/Shaders/Include/GPUInstancerSetup.hlsl"
            #pragma instancing_options procedural:setupGPUI
            #pragma multi_compile_instancing
            
            // -------------------------------------
            // Includes
            #include "DCL_Tree_Input.hlsl"
            #include "DCL_Tree_DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // This pass is used when drawing to a _CameraNormalsTexture texture
        Pass
        {
            Name "DepthNormals"
            Tags
            {
                "LightMode" = "DepthNormals"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _ALPHATEST_ON
            //#pragma shader_feature_local_fragment _GLOSSINESS_FROM_BASE_ALPHA

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            // Universal Pipeline keywords
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include_with_pragmas "Packages/com.gurbu.gpui-pro/Runtime/Shaders/Include/GPUInstancerSetup.hlsl"
            #pragma instancing_options procedural:setupGPUI
            #pragma multi_compile_instancing
            
            // -------------------------------------
            // Includes
            #include "DCL_Tree_Input.hlsl"
            #include "DCL_Tree_DepthNormalsPass.hlsl"
            ENDHLSL
        }

//        Pass
//        {
//            Name "MotionVectors"
//            Tags { "LightMode" = "MotionVectors" }
//            ColorMask RG
//
//            HLSLPROGRAM
//            #pragma shader_feature_local _ALPHATEST_ON
//            #pragma multi_compile _ LOD_FADE_CROSSFADE
//            #pragma shader_feature_local_vertex _ADD_PRECOMPUTED_VELOCITY
//
//            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitInput.hlsl"
//            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
//            ENDHLSL
//        }
//
//        Pass
//        {
//            Name "XRMotionVectors"
//            Tags { "LightMode" = "XRMotionVectors" }
//            ColorMask RGBA
//
//            // Stencil write for obj motion pixels
//            Stencil
//            {
//                WriteMask 1
//                Ref 1
//                Comp Always
//                Pass Replace
//            }
//
//            HLSLPROGRAM
//            #pragma shader_feature_local _ALPHATEST_ON
//            #pragma multi_compile _ LOD_FADE_CROSSFADE
//            #pragma shader_feature_local_vertex _ADD_PRECOMPUTED_VELOCITY
//            #define APPLICATION_SPACE_WARP_MOTION 1
//            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitInput.hlsl"
//            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
//            ENDHLSL
//        }
    }

    Fallback  "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "TreeShaderGUI"
}
