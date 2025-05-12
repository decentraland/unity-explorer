Shader "Hidden/TerrainSplatmask"
{
	Properties
	{
		[HideInInspector] [ToggleUI] _EnableHeightBlend("EnableHeightBlend", Float) = 0.0
		_HeightTransition("Height Transition", Range(0, 1.0)) = 0.0
		// Layer count is passed down to guide height-blend enable/disable, due
		// to the fact that height-based blend will be broken with multipass.
		[HideInInspector][PerRendererData] _NumLayersCount("Total Layer Count", Float) = 1.0

		//Render textures
		[HideInInspector] _SplatBuffer("", 2D) = "black" {}
		[HideInInspector] _Heightmap("", 2D) = "black" {}
		//Leave all default properties so default CBUFFER remains valid

		// set by terrain engine
		[HideInInspector] _Control("Control (RGBA)", 2D) = "red" {}
		[HideInInspector] _Splat3("Layer 3 (A)", 2D) = "grey" {}
		[HideInInspector] _Splat2("Layer 2 (B)", 2D) = "grey" {}
		[HideInInspector] _Splat1("Layer 1 (G)", 2D) = "grey" {}
		[HideInInspector] _Splat0("Layer 0 (R)", 2D) = "grey" {}
		[HideInInspector] _Normal3("Normal 3 (A)", 2D) = "bump" {}
		[HideInInspector] _Normal2("Normal 2 (B)", 2D) = "bump" {}
		[HideInInspector] _Normal1("Normal 1 (G)", 2D) = "bump" {}
		[HideInInspector] _Normal0("Normal 0 (R)", 2D) = "bump" {}
		[HideInInspector] _Mask3("Mask 3 (A)", 2D) = "grey" {}
		[HideInInspector] _Mask2("Mask 2 (B)", 2D) = "grey" {}
		[HideInInspector] _Mask1("Mask 1 (G)", 2D) = "grey" {}
		[HideInInspector] _Mask0("Mask 0 (R)", 2D) = "grey" {}
		[HideInInspector][Gamma] _Metallic0("Metallic 0", Range(0.0, 1.0)) = 0.0
		[HideInInspector][Gamma] _Metallic1("Metallic 1", Range(0.0, 1.0)) = 0.0
		[HideInInspector][Gamma] _Metallic2("Metallic 2", Range(0.0, 1.0)) = 0.0
		[HideInInspector][Gamma] _Metallic3("Metallic 3", Range(0.0, 1.0)) = 0.0
		[HideInInspector] _Smoothness0("Smoothness 0", Range(0.0, 1.0)) = 0.5
		[HideInInspector] _Smoothness1("Smoothness 1", Range(0.0, 1.0)) = 0.5
		[HideInInspector] _Smoothness2("Smoothness 2", Range(0.0, 1.0)) = 0.5
		[HideInInspector] _Smoothness3("Smoothness 3", Range(0.0, 1.0)) = 0.5

		// used in fallback on old cards & base map
		[HideInInspector] _MainTex("BaseMap (RGB)", 2D) = "grey" {}
		[HideInInspector] _BaseColor("Main Color", Color) = (1,1,1,1)

		[HideInInspector] _TerrainHolesTexture("Holes Map (RGB)", 2D) = "white" {}

		[ToggleUI] _EnableInstancedPerPixelNormal("Enable Instanced per-pixel normal", Float) = 1.0
	}

	SubShader
	{
		Tags { "Queue" = "Geometry-100" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "False"}
		Pass
		{
			Name "ForwardLit"
			Tags { "LightMode" = "UniversalForward" }
			HLSLPROGRAM

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			// Required to compile gles 2.0 with standard srp library
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma target 3.0

			#pragma vertex SplatmapVert
			//Override fragment shader
			#pragma fragment SplatmapMaskFragment

			// -------------------------------------
			// Unity defined keywords
			#pragma multi_compile_instancing
			#pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap

			//Disabled features
			#define _METALLICSPECGLOSSMAP 0
			#define _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A 0
			#define _ALPHATEST_ON 0
			#define _TERRAIN_BLEND_HEIGHT 0
			#define _MASKMAP 0
			#define _TERRAIN_INSTANCED_PERPIXEL_NORMAL 0

			#include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLitInput.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLitPasses.hlsl"
			#include "TerrainSplatmaskPass.hlsl"
			ENDHLSL
		}

		//Max blend between alpha and heightmap
		Pass
		{
			Name "Max blend between alpha- and heightmap"
			Tags { "LightMode" = "UniversalForward" }
			ZTest Always
			ZWrite Off
			Cull Off

			HLSLPROGRAM

			// Required to compile gles 2.0 with standard srp library
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma target 3.0

			#pragma vertex FullscreenVert
			#pragma fragment FragMaxBlend

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLitInput.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLitPasses.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "TerrainSplatmaskPass.hlsl"

			ENDHLSL
		}

		//Fill black pixel with white
		Pass
		{
			Name "Fill black pixel with white"
			Tags { "LightMode" = "UniversalForward" }
			ZTest Always
			ZWrite Off
			Cull Off

			HLSLPROGRAM

			// Required to compile gles 2.0 with standard srp library
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma target 3.0

			#pragma vertex FullscreenVert
			#pragma fragment FragFillBlack

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLitInput.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLitPasses.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "TerrainSplatmaskPass.hlsl"		

			ENDHLSL
		}
		
		//Copy heightmap into alpha channel of color map
		Pass
		{
			Name "Copy heightmap into alpha"
			Tags { "LightMode" = "UniversalForward" }
			ZTest Always
			ZWrite Off
			Cull Off

			HLSLPROGRAM

			// Required to compile gles 2.0 with standard srp library
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma target 3.0

			#pragma vertex FullscreenVert
			#pragma fragment FragMergeAlpha

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLitInput.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLitPasses.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "TerrainSplatmaskPass.hlsl"

			ENDHLSL
		}
		
	}
	Fallback "Hidden/Universal Render Pipeline/FallbackError"
}