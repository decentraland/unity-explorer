/* Integration: None */

//Stylized Grass Shader
//Staggart Creations (http://staggart.xyz)
//Copyright protected under Unity Asset Store EULA

Shader "GPUInstancerPro/Universal Render Pipeline/Nature/Stylized Grass"
{
	Properties
	{
		[MainTexture] _BaseMap("Albedo", 2D) = "white" {}
		_Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
		[MaterialEnum(Both,0,Front,1,Back,2)] _Cull("Render faces", Float) = 0
		[Toggle] _AlphaToCoverage("Alpha to coverage", Float) = 0.0
		
		[MaterialEnum(Red,0,Green,1,Blue,2,Alpha,3)] _VertexColorShadingChannel("Vertex Color Shading Channel", Float) = 0.0
		[MaterialEnum(Red,0,Green,1,Blue,2,Alpha,3)] _VertexColorWindChannel("Vertex Color Wind Channel", Float) = 0.0
		[MaterialEnum(Red,0,Green,1,Blue,2,Alpha,3)] _VertexColorBendingChannel("Vertex Color Bending Channel", Float) = 0.0

		//[Header(Shading)]
		[MainColor] _BaseColor("Color", Color) = (0.49, 0.89, 0.12, 1.0)
		_HueVariation("Hue Variation (Alpha = Intensity)", Color) = (1, 0.63, 0, 0.15)
		_HueVariationHeight("Hue Variation Height", Range(0.0, 1.0)) = 0.0
		_ColorMapStrength("Colormap Strength", Range(0.0, 1.0)) = 0.0
		_ColorMapHeight("Colormap Height", Range(0.0, 1.0)) = 1.0
		_ScalemapInfluence("Scale influence", vector) = (0,1,0,0)
		_OcclusionStrength("Ambient Occlusion", Range(0.0, 1.0)) = 0.25
		_VertexDarkening("Random Darkening", Range(0, 1)) = 0.1
		_Smoothness("Smoothness", Range(0.0, 1.0)) = 0.0
		_TranslucencyDirect("Translucency (Direct)", Range(0.0, 1.0)) = 1
		_TranslucencyIndirect("Translucency (Indirect)", Range(0.0, 1.0)) = 0.0
		_TranslucencyFalloff("Translucency Falloff", Range(1.0, 8.0)) = 4.0
		_TranslucencyOffset("Translucency Offset", Range(0.0, 1.0)) = 0.0
		[HDR] _EmissionColor("Emission Color", Color) = (0,0,0)
		
		_NormalFlattening("Normal Flattening",Range(0.0, 1.0)) = 1.0
		_NormalSpherify("Normal Spherifying",Range(0.0, 1.0)) = 0.0
		_NormalSpherifyMask("Normal Spherifying (tip mask)",Range(0.0, 1.0)) = 0.0
		_NormalFlattenDepthNormals("Normal Spherifying (DepthNormals pass)",Range(0.0, 1.0)) = 0.0

		_BumpScale("Normal Map Strength",Range(0.0, 1.0)) = 1.0
		_BumpMap("Normal Map", 2D) = "bump" {}
		_BendPushStrength("Push Strength (XZ)", Range(0.0, 1.0)) = 1.0
		[MaterialEnum(Per Vertex,0,Uniform,1)]_BendMode("Bend Mode", Float) = 0.0
		_BendFlattenStrength("Flatten Strength (Y)", Range(0.0, 1.0)) = 1.0
		_BendTint("Bending tint", Color) = (1, 1, 1, 1.0)
		_PerspectiveCorrection("Perspective Correction", Range(0.0, 1.0)) = 1.0
		_BillboardingVerticalRotation("Billboarding, vertical rotation", Range(0.0, 1.0)) = 0.0

		//[Header(Wind)]
		_WindAmbientStrength("Ambient Strength", Range(0.0, 1.0)) = 0.2
		_WindSpeed("Ambient Speed", Float) = 3.0
		_WindDirection("Direction", vector) = (1,0,0,0)
		_WindVertexRand("Vertex randomization", Range(0.0, 1.0)) = 0.6
		_WindObjectRand("Object randomization", Range(0.0, 1.0)) = 0.5
		_WindRandStrength("Random per-object strength", Range(0.0, 1.0)) = 0.5
		_WindSwinging("Swinging", Range(0.0, 1.0)) = 0.15
		_WindGustStrength("Gusting strength", Range(0.0, 1.0)) = 0.2
		_WindGustFreq("Gusting frequency", Range(0.0, 10.0)) = 4
		_WindGustSpeed("Gusting Speed", Float) = 4
		[NoScaleOffset] _WindMap("Wind map", 2D) = "black" {}
		_WindGustTint("Max Gusting tint", Range(0.0, 3.0)) = 0.1

		//[Header(Rendering)]
		[MinMaxSlider(0, 25)] _FadeNear("Near", vector) = (0.25, 0.5, 0, 0)
		[MinMaxSlider(0, 500)] _FadeFar("Far", vector) = (50, 100, 0, 0)
		_FadeAngleThreshold("Angle fading threshold", Range(0.0, 90.0)) = 15
		
		//Keyword states
		[MaterialEnum(Unlit,0,Simple,1,Advanced,2)]_LightingMode("Lighting Mode", Float) = 2.0
		[Toggle] _Scalemap("Scale grass by scalemap", Float) = 0.0
		[Toggle] _Billboard("Billboard", Float) = 0.0
		[ToggleOff] _ReceiveShadows("Receive Shadows", Float) = 1.0
		[ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
		[Toggle] _EnvironmentReflections("Environment Reflections", Float) = 1.0
		[Toggle] _FadingOn("Distance/Angle Fading", Float) = 0.0
		
		// Editmode props
		[HideInInspector] _QueueOffset("Queue offset", Float) = 0.0

		/* start CurvedWorld */
		//[CurvedWorldBendSettings] _CurvedWorldBendSettings("0|1|1", Vector) = (0, 0, 0, 0)
		/* end CurvedWorld */
		
		//Vegetation Studio Pro v1.4.0+
		_LODDebugColor ("LOD Debug color", Color) = (1,1,1,1)
		
		[HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
		[HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
		[HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
		
		/* start FoliageRenderer */
//		[HideInInspector] _TerrainAlbedoProvided("Blend with Albedo Shader", Float) = 0
//		
//		[HideInInspector]_TerrainSize("Terrain Size", Vector) = (0,0,0,0)
//		[HideInInspector]_TerrainPosition("Terrain Position", Vector) = (0,0,0,0)
//		_TerrainYOffset("Y Offset", Float) = 0
//		
//		[HideInInspector]_TerrainAlbedoC("Terrain", 2D) = "black" {}
//	    [HideInInspector]_TerrainAlbedoL("TerrainL", 2D) = "black" {}
//	    [HideInInspector]_TerrainAlbedoR("TerrainR", 2D) = "black" {}
//	    [HideInInspector]_TerrainAlbedoU("TerrainU", 2D) = "black" {}
//	    [HideInInspector]_TerrainAlbedoUL("TerrainUL", 2D) = "black" {}
//	    [HideInInspector]_TerrainAlbedoUR("TerrainUR", 2D) = "black" {}
//	    [HideInInspector]_TerrainAlbedoB("TerrainB", 2D) = "black" {}
//	    [HideInInspector]_TerrainAlbedoBL("TerrainBL", 2D) = "black" {}
//	    [HideInInspector]_TerrainAlbedoBR("TerrainBR", 2D) = "black" {}
		/* end FoliageRenderer */

	}

	SubShader
	{
		Tags{
			"RenderType" = "Opaque"
			"Queue" = "AlphaTest"
			"RenderPipeline" = "UniversalPipeline"
			"IgnoreProjector" = "True"
			"NatureRendererInstancing" = "True"
			"DisableBatching" = "True"
		}
		
		HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
		#if UNITY_VERSION >= 202220
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
		#endif
		#define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR

		//Hard coded features
		#define _ALPHATEST_ON

		//Uncomment to compile out these calculations
		//#define DISABLE_WIND
		//#define DISABLE_BENDING

		//Uncomment to enable
		//#define MASKING_SPHERE_DISPLACEMENT
		
		#pragma target 3.5

	    #if UNITY_VERSION >= 202220 
        #endif

		#if UNITY_VERSION >= 202230
		#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
		#endif

		// GPU Instancing
		#pragma shader_feature_local _GPU_INSTANCER_BATCHER

		/* start CurvedWorld */
		//#define CURVEDWORLD_BEND_TYPE_CLASSICRUNNER_X_POSITIVE
		//#define CURVEDWORLD_BEND_ID_1
		//#pragma shader_feature_local CURVEDWORLD_DISABLED_ON
		//#pragma shader_feature_local CURVEDWORLD_NORMAL_TRANSFORMATION_ON
		//#include "Assets/Amazing Assets/Curved World/Shaders/Core/CurvedWorldTransform.cginc"
		/* end CurvedWorld */

		//* start VegetationStudio */
//		#define VegetationStudio
		/* end VegetationStudio */

		#ifdef VegetationStudio
		/* include VegetationStudio */
		#include "Assets/AwesomeTechnologies/VegetationStudioPro/Runtime/Shaders/Resources/VSPro_HDIndirect.cginc"
		#endif

		/* start GPUInstancer */
//		#define GPUInstancer
		/* end GPUInstancer */

		#ifdef GPUInstancer
		/* include GPUInstancer */
		#endif
		
		/* start NatureRendererLegacy */
//		#define NatureRendererLegacy
		/* end NatureRendererLegacy */

		#ifdef NatureRendererLegacy
		/* include NatureRendererLegacy */
		#include "Assets/Visual Design Cafe/Nature Shaders/Common/Nodes/Integrations/Nature Renderer.cginc"
		#endif
		
		/* start NatureRenderer */
//		#define NatureRenderer
		/* end NatureRenderer */

		#ifdef NatureRenderer
		/* include NatureRenderer */
		#include "Assets/Visual Design Cafe/Nature Renderer/Shader Includes/Nature Renderer.templatex"
		#endif

		/* start FoliageRenderer */
//		#define FoliageRenderer
		/* end FoliageRenderer */

		#ifdef FoliageRenderer
		/* include FoliageRenderer */
#include "Assets/FoliageRenderer/Shaders/FoliageRendererInstancing.cginc"
		#endif
		
		ENDHLSL

		// ------------------------------------------------------------------
		//  Forward pass. Shades all light in a single pass. GI + emission + Fog
		Pass
		{
			Name "ForwardLit"
			Tags{ "LightMode" = "UniversalForward" }

			AlphaToMask [_AlphaToCoverage]
			Blend One Zero, One Zero
			Cull [_Cull]
			ZWrite On

			HLSLPROGRAM


			//In place for projects that use a custom RP or modified URP and require specific behaviour for vegetation
			#define VEGETATION_SHADER

			// -------------------------------------
			// Material Keywords
			#pragma multi_compile _ LOD_FADE_CROSSFADE //Note: Cannot use _fragment suffix, unity_LODFade conflicts with unity_RenderingLayer in cBuffer somehow
			#pragma shader_feature_local_vertex _SCALEMAP
			#pragma shader_feature_local_vertex _BILLBOARD
			#pragma shader_feature_local_fragment _FADING
			#pragma shader_feature_local _NORMALMAP
			#pragma shader_feature_local _ _SIMPLE_LIGHTING _ADVANCED_LIGHTING
			#pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
			#pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
			#pragma shader_feature_local _RECEIVE_SHADOWS_OFF
			
			// -------------------------------------
            // Universal Pipeline keywords
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE  
			//#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS	
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

			#if !_SIMPLE_LIGHTING && !_ADVANCED_LIGHTING
			#define _UNLIT
			#undef _NORMALMAP
			#endif
			
			// -------------------------------------
			// Unity defined keywords
			//#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ DYNAMICLIGHTMAP_ON
			#pragma multi_compile_fog
			#if UNITY_VERSION >= 202310            
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #endif

			//VVV Stripped on older URP versions VVV

			//URP 10+
			#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK 
			#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION 
			
			//URP 12+
			#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3 
            #pragma multi_compile_fragment _ _LIGHT_LAYERS 
			#pragma multi_compile_fragment _ _LIGHT_COOKIES 
            //#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE //Deprecated?
			#pragma multi_compile _ _CLUSTERED_RENDERING 
			#pragma multi_compile_fragment _ DEBUG_DISPLAY

			//URP 14+
			#pragma multi_compile_fragment _ _FORWARD_PLUS
			
			//Constants
			#define SHADERPASS_FORWARD
			#pragma multi_compile_local_vertex _ _GPU_INSTANCER_BATCHER
			
			#pragma vertex LitPassVertex
			#pragma fragment LightingPassFragment

			//Half-fix for shadow cascades breaking in 2020.3, due to keywords following a set up needed to support newer versions
			#if UNITY_VERSION < 202110 && _MAIN_LIGHT_SHADOWS
			#define _MAIN_LIGHT_SHADOWS_CASCADE 0
			#endif
			
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

			#define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "UnityIndirect.cginc"

			#include "Libraries/Input.hlsl"
			#include "Libraries/Common.hlsl"
			#include "Libraries/Color.hlsl"
			#include "Libraries/Lighting.hlsl"

			#include "LightingPass.hlsl"
			
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.gurbu.gpui-pro/Runtime/Shaders/Include/GPUInstancerSetup.hlsl"
#pragma instancing_options procedural:setupGPUI
#pragma multi_compile_instancing

ENDHLSL
		}

	
		
		Pass
		{
			Name "DepthOnly"
			Tags{"LightMode" = "DepthOnly"}

			ZWrite On
			ColorMask 0
			Cull[_Cull]

			HLSLPROGRAM


			#define SHADERPASS_DEPTHONLY

			// -------------------------------------
			// Material Keywords
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#pragma shader_feature_local_vertex _SCALEMAP
			#pragma shader_feature_local_vertex _BILLBOARD
			#pragma shader_feature_local_fragment _FADING
			#pragma multi_compile_local_vertex _ _GPU_INSTANCER_BATCHER

			#define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "UnityIndirect.cginc"

			#include "Libraries/Input.hlsl"
			#include "Libraries/Common.hlsl"

			#pragma vertex DepthOnlyVertex
			#pragma fragment DepthOnlyFragment
			
			#include "DepthPass.hlsl"
			
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.gurbu.gpui-pro/Runtime/Shaders/Include/GPUInstancerSetup.hlsl"
#pragma instancing_options procedural:setupGPUI
#pragma multi_compile_instancing

ENDHLSL
		}

	

		// Used for Baking GI. This pass is stripped from build.
		//Disabled, breaks SRP batcher, shader doesn't have the exact same properties as the Lit shader
		//UsePass "Universal Render Pipeline/Lit/Meta"

	}//Subshader

	FallBack "Hidden/Universal Render Pipeline/FallbackError"
	CustomEditor "StylizedGrass.MaterialUI"

}//Shader