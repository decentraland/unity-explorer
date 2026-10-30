Shader "DCL/Avatar_CelShading"
{
    Properties
    {
        [HideInInspector] [PerRendererData] _BaseMapArr_ID ("BaseMap Array ID", Integer) = -1
        [HideInInspector] [PerRendererData] _AlphaTextureArr_ID ("AlphaTexture Array ID", Integer) = -1
        [HideInInspector] [PerRendererData] _MetallicGlossMapArr_ID ("MetallicGlossMap Array ID", Integer) = -1
        [HideInInspector] [PerRendererData] _BumpMapArr_ID ("BumpMap Array ID", Integer) = -1
        [HideInInspector] [PerRendererData] _EmissionMapArr_ID ("EmissionMap Array ID", Integer) = -1
        [HideInInspector] [PerRendererData] _OcclusionMapArr_ID ("OcclusionMap Array ID", Integer) = -1
        
        [HideInInspector] [PerRendererData] _lastWearableVertCount ("Last wearable Vert Count", Integer) = -1
        [HideInInspector] [PerRendererData] _lastAvatarVertCount ("Last avatar vert count", Integer) = -1

        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [HideInInspector] _BaseMapArr ("AlbedoArray", 2DArray) = "white" {}
        [HideInInspector] _AlphaTextureArr("Alpha Texture", 2DArray) = "white" {}
        [HideInInspector] _MetallicGlossMapArr("Metallic", 2DArray) = "white" {}
        [HideInInspector] _BumpMapArr("Normal Map", 2DArray) = "bump" {}
        [HideInInspector] _EmissionMapArr("Emission", 2DArray) = "white" {}
        
        _MatCap("MatCap Texture", 2D) = "white" {}
        
        [PerRendererData] _BaseColor("Color", Color) = (0.5,0.5,0.5,1)
        
        _DiffuseRampInnerMin("Diffuse Ramp Inner Min", Range(0.0, 1.0)) = 0.0
        _DiffuseRampInnerMax("Diffuse Ramp Inner Max", Range(0.0, 1.0)) = 1.0
        _DiffuseRampOuterMin("Diffuse Ramp Outer Min", Range(0.0, 1.0)) = 0.0
        _DiffuseRampOuterMax("Diffuse Ramp Outer Max", Range(0.0, 1.0)) = 1.0
        _SpecularRampInnerMin("Specular Ramp Inner Min", Range(0.0, 1.0)) = 0.0
        _SpecularRampInnerMax("Specular Ramp Inner Max", Range(0.0, 1.0)) = 1.0
        _SpecularRampOuterMin("Specular Ramp Outer Min", Range(0.0, 1.0)) = 0.0
        _SpecularRampOuterMax("Specular Ramp Outer Max", Range(0.0, 1.0)) = 1.0

        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _GlossMapScale("Smoothness Scale", Range(0.0, 1.0)) = 1.0
        _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0

        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0

        _SpecColor("Specular", Color) = (0.2, 0.2, 0.2)

        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0

        _BumpScale("Scale", Float) = 1.0
        
        _BaseMapUVs ("Albedo UV Channel", Int) = 0
        _NormalMapUVs ("Normal UV Channel", Int) = 0
        _MetallicMapUVs ("Metallic UV Channel", Int) = 0
        _EmissiveMapUVs ("Emissive UV Channel", Int) = 0
        
        [HDR] _EmissionColor("Color", Color) = (0,0,0)
        
        // Blending state
        [HideInInspector] _Surface("__surface", Float) = 0.0
        [HideInInspector] _Blend("__blend", Float) = 0.0
        [HideInInspector] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _Cull("__cull", Float) = 2.0

        _ReceiveShadows("Receive Shadows", Float) = 1.0
        // Editmode props
        [HideInInspector] _QueueOffset("Queue offset", Float) = 0.0
        
        [PerRendererData] _CullYPlane ("Cull Y Plane", Float) = 0
        _FadeThickness ("Fade Thickness", Float) = 5
        _FadeDirection ("Fade Direction", Float) = 0
        
        //[HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }

    SubShader
    {
        // Universal Pipeline tag is required. If Universal render pipeline is not set in the graphics settings
        // this Subshader will fail. One can add a subshader below or fallback to Standard built-in to make this
        // material work with both Universal Render Pipeline and Builtin Unity Pipeline
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
            "ShaderModel"="4.5"
        }
        LOD 300

        // ------------------------------------------------------------------
        //  Forward pass. Shades all light in a single pass. GI + emission + Fog
        Pass
        {
            // Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
            // no LightMode tag are also rendered by Universal Render Pipeline
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Blend[_SrcBlend][_DstBlend]
            ZWrite[_ZWrite]
            Cull[_Cull]

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            // -------------------------------------
            // Material Keywords
            //#pragma shader_feature_local _NORMALMAP
            //#pragma shader_feature_local_fragment _EMISSION
            //#pragma shader_feature_local_fragment _OCCLUSIONMAP
            //#pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON
            //#pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            //#pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            // #pragma shader_feature_local _MAIN_LIGHT_SHADOWS
            // #pragma shader_feature_local _MAIN_LIGHT_SHADOWS_CASCADE
            // #pragma shader_feature_local _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            // #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            // #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            // #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            //#pragma shader_feature_local_fragment _ADDITIONAL_LIGHT_SHADOWS
            //#pragma shader_feature_local_fragment _REFLECTION_PROBE_BLENDING
            //#pragma shader_feature_local_fragment _REFLECTION_PROBE_BOX_PROJECTION
            //#pragma multi_compile_fragment _ _SHADOWS_SOFT
            //#pragma shader_feature_local_fragment _SHADOWS_SOFT
            //#pragma shader_feature_local_fragment _SCREEN_SPACE_OCCLUSION
            //#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            //#pragma multi_compile_fragment _ _LIGHT_LAYERS
            //#pragma shader_feature_local_fragment _LIGHT_COOKIES
            #pragma multi_compile _ _FORWARD_PLUS
            
            //#pragma shader_feature_local _CLUSTERED_RENDERING
            // #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTERED_RENDERING

            // -------------------------------------
            // Unity defined keywords
            //#pragma multi_compile_fog
            //#pragma multi_compile_fragment _ DEBUG_DISPLAY

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #pragma require 2darray

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment           

            #include "Avatar_CelShading_LitInput.hlsl"
            #include "Avatar_CelShading_LitForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            // This is used during shadow map generation to differentiate between directional and punctual light shadows, as they use different formulas to apply Normal Bias
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #pragma require 2darray
            
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Avatar_CelShading_LitInput.hlsl"
            #include "Avatar_CelShading_ShadowCasterPass.hlsl"
            ENDHLSL
        }
        
        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite [_ZWrite]
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            #pragma require 2darray
            
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #include "Avatar_CelShading_LitInput.hlsl"
            #include "Avatar_CelShading_DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // This pass is used when drawing to a _CameraNormalsTexture texture
        Pass
        {
            Name "DepthNormals"
            Tags{"LightMode" = "DepthNormals"}

            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            #pragma require 2darray
            
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            // -------------------------------------
            // Material Keywords
            //#pragma shader_feature_local _NORMALMAP
            //#pragma shader_feature_local _PARALLAXMAP
            //#pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #include "Avatar_CelShading_LitInput.hlsl"
            #include "Avatar_CelShading_DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
