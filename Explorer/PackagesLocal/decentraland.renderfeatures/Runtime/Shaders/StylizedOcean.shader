// Decentraland / Stylized Ocean Shader (URP)
//
// Clean-room stylized water for the DCL ocean. Authored in ShaderLab/HLSL
// against URP 17 (Unity 6). Ocean.mat / Pond.mat bind this shader by GUID.
// All pass code lives in StylizedOceanCore.hlsl; this file declares the
// material surface and the two SubShaders:
//
//   1. Tessellated (shader model 4.6): hull/domain stages subdivide the water
//      grid near the camera by _TessValue/_TessMin/_TessMax so the Gerstner
//      swell and ripple detail resolve finer than the authored grid spacing.
//   2. Vertex-only fallback (shader model 3.0) for GPUs without tessellation,
//      displacing the authored grid directly.
//
// The full authored property list is preserved so existing .mat values
// round-trip (Unity keys m_SavedProperties by name); the properties the core
// include consumes are listed first, the remaining legacy ones are hidden.

Shader "Decentraland/StylizedOcean"
{
    Properties
    {
        // --- Core surface ---------------------------------------------------
        [MainTexture] _BaseMap          ("Base Map", 2D)                       = "white" {}
        [MainColor]   _BaseColor        ("Deep Color", Color)                  = (0.08, 0.07, 0.24, 1)
                      _ShallowColor     ("Shallow Color", Color)               = (0.43, 0.93, 1.08, 1)
                      _HorizonColor     ("Horizon Color", Color)               = (0.41, 0.72, 1.0, 0)
                      _Color            ("Tint (legacy)", Color)               = (1,1,1,1)
        [HideInInspector] _MainTex      ("Albedo (legacy)", 2D)                = "white" {}

        _Cutoff         ("Alpha Cutoff", Range(0, 1))                          = 0.5
        _Smoothness     ("Smoothness",   Range(0, 1))                          = 0.85
        _Metallic       ("Metallic",     Range(0, 1))                          = 0
        _SpecColor      ("Specular Color", Color)                              = (0.2, 0.2, 0.2, 1)

        // --- Normal scrolling ----------------------------------------------
        [NoScaleOffset] _BumpMap        ("Normal Map A (small)", 2D)           = "bump" {}
        [NoScaleOffset] _BumpMapLarge   ("Normal Map B (large)", 2D)           = "bump" {}
                       _BumpScale       ("Normal Strength", Float)             = 1
                       _NormalTiling    ("Normal Tiling (xy=A, zw=B)", Vector) = (0.14, 0.14, 0, 0)
                       _NormalSpeed     ("Normal Speed (A)", Float)            = 0.5
                       _NormalSubSpeed  ("Normal Speed (B)", Float)            = 5
                       _NormalSubTiling ("Normal Sub Tiling", Float)           = 0.1
                       _NormalStrength  ("Normal Strength (legacy)", Float)    = 0
                       _Direction       ("Scroll Direction (xy=A, zw=B)", Vector) = (0.5, 0.5, 0, 0)

        // --- Horizon / Fresnel ---------------------------------------------
        _HorizonDistance     ("Horizon Distance", Float)                       = 8
        _ReflectionFresnel   ("Fresnel Power",    Float)                       = 5
        _ReflectionStrength  ("Reflection Strength", Float)                    = 1

        // --- Depth / refraction / foam -------------------------------------
        _Depth          ("Depth Fade Distance", Float)                        = 2.45
        _ColorAbsorption("Colour Absorption",   Float)                        = 0
        _EdgeFade       ("Edge Fade",           Float)                        = 1.01
        _RefractionStrength ("Refraction Strength", Float)                    = 0.06
        _FoamColor      ("Foam Color", Color)                                 = (1,1,1,1)
        [Toggle] _FoamOn ("Shoreline Foam", Float)                            = 1
        _FoamSize       ("Foam Size",  Float)                                 = 0.695
        _FoamSpeed      ("Foam Speed", Float)                                 = 0.15
        _FoamTiling     ("Foam Tiling", Vector)                               = (0.01,0.01,0,0)
        _ShoreLineLength("Shoreline Length", Float)                           = 2

        // --- Caustics (projected onto the refracted floor) -----------------
        [Toggle] _CausticsOn ("Caustics", Float)                              = 0
        [NoScaleOffset] _CausticsTex ("Caustics Texture", 2D)                 = "white" {}
        _CausticsBrightness ("Caustics Brightness", Float)                    = 2
        _CausticsDistortion ("Caustics Distortion", Float)                    = 1
        _CausticsTiling     ("Caustics Tiling", Float)                        = 0.5
        _CausticsSpeed      ("Caustics Speed", Float)                         = 0.1

        // --- Intersection foam ---------------------------------------------
        [NoScaleOffset] _IntersectionNoise ("Intersection Noise", 2D)         = "white" {}
        [HDR] _IntersectionColor ("Intersection Color (a = opacity)", Color)  = (4,4,4,0.035)
        [Enum(Sharp,0,Ripples,1)] _IntersectionStyle ("Intersection Style", Float) = 1
        [Enum(Depth,0,VertexColor,1,DepthMaskedByVertexColor,2)] _IntersectionSource ("Intersection Source", Float) = 2
        _IntersectionSpeed   ("Intersection Speed", Float)                    = 0.11
        _IntersectionTiling  ("Intersection Tiling", Float)                   = 0.1
        _IntersectionLength  ("Intersection Length", Float)                   = 5
        _IntersectionFalloff ("Intersection Falloff", Float)                  = 1
        _IntersectionClipping ("Intersection Clipping", Range(0, 1))          = 0.791
        _IntersectionRippleDist ("Intersection Ripple Distance", Float)       = 0.5
        _IntersectionRippleStrength ("Intersection Ripple Strength", Range(0, 1)) = 0.2
        [Toggle] _CrossPan_IntersectionOn ("Intersection Cross Pan", Float)   = 1

        // --- Sparkle -------------------------------------------------------
        _SparkleIntensity ("Sparkle Intensity", Float)                        = 1.06
        _SparkleSize      ("Sparkle Size", Range(0, 1))                       = 0.534

        // --- Slope foam ----------------------------------------------------
        [Toggle] _SlopeFoam ("Slope Foam", Float)                             = 1
        _SlopeAngleThreshold ("Slope Angle Threshold (deg)", Float)           = 76
        _SlopeAngleFalloff   ("Slope Angle Falloff (deg)", Float)             = 74.1
        _SlopeSpeed          ("Slope Foam Speed", Float)                      = 4
        _SlopeStretching     ("Slope Foam Stretching", Range(0, 1))           = 0.5
        _SlopeThreshold      ("Slope Foam Threshold", Range(0, 1))            = 0.25

        // --- Tessellation --------------------------------------------------
        _TessValue ("Tessellation Factor", Range(1, 64))                      = 45.3
        _TessMin   ("Tessellation Min Distance", Float)                       = 0
        _TessMax   ("Tessellation Max Distance", Float)                       = 15

        // --- Gerstner waves -------------------------------------------------
        _WaveDirection ("Wave Direction (xz)", Vector)                         = (1, 1, 1, 1)
        _WaveHeight    ("Wave Height",         Range(0, 4))                    = 0.5
        _WaveDistance  ("Wave Wavelength",     Range(0.01, 4))                 = 0.27
        _WaveSpeed     ("Wave Speed",          Range(0, 16))                   = 4.37
        _WaveSteepness ("Wave Steepness",      Range(0, 1))                    = 0
        _WaveCount     ("Wave Count (1..6)",   Range(1, 6))                    = 5
        _WaveTint      ("Wave Tint (crest darken)", Range(0, 1))               = 0
        _WaveNormalStr ("Wave Normal Strength", Range(0, 4))                   = 0
        _Speed         ("Master Time Multiplier", Float)                       = 1
        _AnimationSpeed ("Animation Speed",    Float)                          = 1

        // --- Render state --------------------------------------------------
        [HideInInspector] _Surface  ("__surface", Float)                       = 0
        [HideInInspector] _Blend    ("__blend",   Float)                       = 0
        [HideInInspector] _Cull     ("__cull",    Float)                       = 2
        [HideInInspector] _SrcBlend ("__src",     Float)                       = 1
        [HideInInspector] _DstBlend ("__dst",     Float)                       = 0
        [HideInInspector] _ZWrite   ("__zw",      Float)                       = 0
        [HideInInspector] _ZClip    ("__zclip",   Float)                       = 1
        [HideInInspector] _QueueOffset("__qo",    Float)                       = 0

        // --- Legacy / passthrough — kept declared so existing .mat files
        // retain authored values across the round trip even though this
        // shader does not consume them. All HideInInspector. ---------------
        [HideInInspector] _BumpMapSlope     ("BumpMapSlope", 2D)                = "bump" {}
        [HideInInspector] _DepthTex         ("DepthTex", 2D)                    = "white" {}
        [HideInInspector] _EmissionMap      ("EmissionMap", 2D)                 = "white" {}
        [HideInInspector] _FoamTex          ("FoamTex", 2D)                     = "white" {}
        [HideInInspector] _FoamTexDynamic   ("FoamTexDynamic", 2D)              = "white" {}
        [HideInInspector] _MetallicGlossMap ("MetallicGlossMap", 2D)            = "white" {}
        [HideInInspector] _OcclusionMap     ("OcclusionMap", 2D)                = "white" {}
        [HideInInspector] _PlanarReflection ("PlanarReflection", 2D)            = "white" {}
        [HideInInspector] _PlanarReflectionLeft ("PlanarReflectionLeft", 2D)    = "white" {}
        [HideInInspector] _SpecGlossMap     ("SpecGlossMap", 2D)                = "white" {}

        [HideInInspector] _AlphaClip                       ("_AlphaClip", Float) = 0
        [HideInInspector] _DepthExp                        ("_DepthExp", Float) = 1
        [HideInInspector] _DepthHorizontal                 ("_DepthHorizontal", Float) = 1.5
        [HideInInspector] _DepthMode                       ("_DepthMode", Float) = 0
        [HideInInspector] _DepthTexture                    ("_DepthTexture", Float) = 1
        [HideInInspector] _DepthVertical                   ("_DepthVertical", Float) = 0.01
        [HideInInspector] _DisableDepthTexture             ("_DisableDepthTexture", Float) = 0
        [HideInInspector] _DistanceNormalsOn               ("_DistanceNormalsOn", Float) = 0
        [HideInInspector] _DistanceNormalsTiling           ("_DistanceNormalsTiling", Float) = 0.1
        [HideInInspector] _EnvironmentReflections          ("_EnvironmentReflections", Float) = 1
        [HideInInspector] _EnvironmentReflectionsOn        ("_EnvironmentReflectionsOn", Float) = 1
        [HideInInspector] _FlatShadingOn                   ("_FlatShadingOn", Float) = 0
        [HideInInspector] _FoamBaseAmount                  ("_FoamBaseAmount", Float) = 0.109
        [HideInInspector] _FoamClipping                    ("_FoamClipping", Float) = 0.75
        [HideInInspector] _FoamDistortion                  ("_FoamDistortion", Float) = 1.58
        [HideInInspector] _FoamOpacity                     ("_FoamOpacity", Float) = 1
        [HideInInspector] _FoamSpeedDynamic                ("_FoamSpeedDynamic", Float) = 0.1
        [HideInInspector] _FoamSubSpeed                    ("_FoamSubSpeed", Float) = -0.25
        [HideInInspector] _FoamSubSpeedDynamic             ("_FoamSubSpeedDynamic", Float) = -0.1
        [HideInInspector] _FoamSubTiling                   ("_FoamSubTiling", Float) = 0.5
        [HideInInspector] _FoamSubTilingDynamic            ("_FoamSubTilingDynamic", Float) = 2
        [HideInInspector] _FoamTilingDynamic               ("_FoamTilingDynamic", Float) = 0.1
        [HideInInspector] _FoamWaveAmount                  ("_FoamWaveAmount", Float) = 0
        [HideInInspector] _FoamWaveMask                    ("_FoamWaveMask", Float) = 1
        [HideInInspector] _FoamWaveMaskExp                 ("_FoamWaveMaskExp", Float) = 1
        [HideInInspector] _GlossMapScale                   ("_GlossMapScale", Float) = 0
        [HideInInspector] _Glossiness                      ("_Glossiness", Float) = 0
        [HideInInspector] _GlossyReflections               ("_GlossyReflections", Float) = 0
        [HideInInspector] _LightingOn                      ("_LightingOn", Float) = 0
        [HideInInspector] _NormalMapOn                     ("_NormalMapOn", Float) = 1
        [HideInInspector] _OcclusionStrength               ("_OcclusionStrength", Float) = 1
        [HideInInspector] _PlanarReflectionsEnabled        ("_PlanarReflectionsEnabled", Float) = 0
        [HideInInspector] _PlanarReflectionsParams         ("_PlanarReflectionsParams", Float) = 0
        [HideInInspector] _PointSpotLightReflectionDistortion ("_PointSpotLightReflectionDistortion", Float) = 0.5
        [HideInInspector] _PointSpotLightReflectionExp     ("_PointSpotLightReflectionExp", Float) = 64
        [HideInInspector] _PointSpotLightReflectionSize    ("_PointSpotLightReflectionSize", Float) = 0
        [HideInInspector] _PointSpotLightReflectionStrength ("_PointSpotLightReflectionStrength", Float) = 10
        [HideInInspector] _ReceiveShadows                  ("_ReceiveShadows", Float) = 0
        [HideInInspector] _ReflectionBlur                  ("_ReflectionBlur", Float) = 0.205
        [HideInInspector] _ReflectionDistortion            ("_ReflectionDistortion", Float) = 0
        [HideInInspector] _ReflectionLighting              ("_ReflectionLighting", Float) = 0
        [HideInInspector] _RefractionChromaticAberration   ("_RefractionChromaticAberration", Float) = 0.1
        [HideInInspector] _RefractionOn                    ("_RefractionOn", Float) = 1
        [HideInInspector] _RiverModeOn                     ("_RiverModeOn", Float) = 0
        [HideInInspector] _SHARP_INERSECTIONOn             ("_SHARP_INERSECTIONOn", Float) = 1
        [HideInInspector] _ShadingMode                     ("_ShadingMode", Float) = 1
        [HideInInspector] _ShadowStrength                  ("_ShadowStrength", Float) = 1
        [HideInInspector] _ShoreLineWaveDistance           ("_ShoreLineWaveDistance", Float) = 64
        [HideInInspector] _ShoreLineWaveStr                ("_ShoreLineWaveStr", Float) = 0.008
        [HideInInspector] _SmoothnessTextureChannel        ("_SmoothnessTextureChannel", Float) = 0
        [HideInInspector] _SpecularHighlights              ("_SpecularHighlights", Float) = 1
        [HideInInspector] _SpecularReflectionsOn           ("_SpecularReflectionsOn", Float) = 0
        [HideInInspector] _SunReflectionDistortion         ("_SunReflectionDistortion", Float) = 2
        [HideInInspector] _SunReflectionPerturbance        ("_SunReflectionPerturbance", Float) = 1
        [HideInInspector] _SunReflectionSize               ("_SunReflectionSize", Float) = 0.95
        [HideInInspector] _SunReflectionStrength           ("_SunReflectionStrength", Float) = 1
        [HideInInspector] _Texture_IntersectionOn          ("_Texture_IntersectionOn", Float) = 0
        [HideInInspector] _Translucency                    ("_Translucency", Float) = 1
        [HideInInspector] _TranslucencyCurvatureMask       ("_TranslucencyCurvatureMask", Float) = 0.3
        [HideInInspector] _TranslucencyExp                 ("_TranslucencyExp", Float) = 10
        [HideInInspector] _TranslucencyOn                  ("_TranslucencyOn", Float) = 1
        [HideInInspector] _TranslucencyReflectionMask      ("_TranslucencyReflectionMask", Float) = 0
        [HideInInspector] _TranslucencyStrength            ("_TranslucencyStrength", Float) = 1.83
        [HideInInspector] _TranslucencyStrengthDirect      ("_TranslucencyStrengthDirect", Float) = 0.1
        [HideInInspector] _UnderwaterRefractionOffset      ("_UnderwaterRefractionOffset", Float) = 0.2
        [HideInInspector] _UnderwaterSurfaceSmoothness     ("_UnderwaterSurfaceSmoothness", Float) = 0.8
        [HideInInspector] _VertexColorDepth                ("_VertexColorDepth", Float) = 0
        [HideInInspector] _VertexColorFoam                 ("_VertexColorFoam", Float) = 0
        [HideInInspector] _VertexColorWaveFlattening       ("_VertexColorWaveFlattening", Float) = 0
        [HideInInspector] _WavesOn                         ("_WavesOn", Float) = 1
        [HideInInspector] _WorkflowMode                    ("_WorkflowMode", Float) = 1
        [HideInInspector] _WorldSpaceUV                    ("_WorldSpaceUV", Float) = 1

        [HideInInspector] _DepthMapBounds                  ("_DepthMapBounds", Vector) = (0,0,0,0)
        [HideInInspector] _DistanceNormalsFadeDist         ("_DistanceNormalsFadeDist", Vector) = (0,500,0,0)
        [HideInInspector] _EmissionColor                   ("_EmissionColor", Color)  = (0,0,0,1)
        [HideInInspector] _WaveFadeDistance                ("_WaveFadeDistance", Vector) = (0,500,0,0)

        [HideInInspector][NoScaleOffset] unity_Lightmaps    ("unity_Lightmaps",    2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks  ("unity_ShadowMasks",  2DArray) = "" {}
    }

    // -----------------------------------------------------------------
    // SubShader 1: tessellated surface (shader model 4.6).
    // -----------------------------------------------------------------
    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "Queue"           = "Transparent"
        }
        LOD 200

        HLSLINCLUDE
        #define OCEAN_TESSELLATION 1
        #include "StylizedOceanCore.hlsl"
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend][_DstBlend]
            ZWrite [_ZWrite]
            ZTest LEqual
            Cull   [_Cull]

            HLSLPROGRAM
            #pragma target 4.6
            #pragma vertex   OceanTessVertex
            #pragma hull     OceanHull
            #pragma domain   OceanDomainForward
            #pragma fragment OceanFragment

            // Only the main light, its shadows and fog are consumed; the
            // additional-light, SH and SSAO keyword sets would multiply the
            // variant count ~36x for branches this pass never takes.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.6
            #pragma vertex   OceanTessVertex
            #pragma hull     OceanHull
            #pragma domain   OceanDomainShadow
            #pragma fragment ShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite [_ZWrite]
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.6
            #pragma vertex   OceanTessVertex
            #pragma hull     OceanHull
            #pragma domain   OceanDomainDepth
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.6
            #pragma vertex   OceanTessVertex
            #pragma hull     OceanHull
            #pragma domain   OceanDomainDepthNormals
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }

    // -----------------------------------------------------------------
    // SubShader 2: vertex-displaced fallback (shader model 3.0).
    // -----------------------------------------------------------------
    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "Queue"           = "Transparent"
        }
        LOD 100

        HLSLINCLUDE
        #include "StylizedOceanCore.hlsl"
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend][_DstBlend]
            ZWrite [_ZWrite]
            ZTest LEqual
            Cull   [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   OceanVertex
            #pragma fragment OceanFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite [_ZWrite]
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   DepthVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
