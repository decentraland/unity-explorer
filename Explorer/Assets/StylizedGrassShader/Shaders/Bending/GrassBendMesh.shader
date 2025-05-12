Shader "Hidden/Nature/Grass Bend Mesh"
{
	Properties
	{
		[PerRendererData]
		_Params("Parameters", vector) = (1,0,1,1)
		//X: Flatten strength
		//Y: Height offset
		//Z: Push strength
		//W: Scale multiplier
		
		[PerRendererData]
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcFactor("Src Factor", Float) = 5    // SrcAlpha
		[PerRendererData]
		[Enum(UnityEngine.Rendering.BlendMode)] _DstFactor("Dst Factor", Float) = 10   // OneMinusSrcAlpha
	}

	HLSLINCLUDE
	//Somewhat different code path for trails and lines
	#pragma multi_compile_local _ _TRAIL
	#pragma multi_compile_instancing

	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
	#include "Common.hlsl"
	ENDHLSL

	SubShader
	{
		//Tags{ "LightMode" = "UniversalForward" "RenderPipeline" = "UniversalPipeline"}
		Tags{ "LightMode" = "GrassBender" "RenderPipeline" = "UniversalPipeline" }

		//Rendering from bottom-up, so looking for the front faces
		Cull Back
		ZWrite On
		ZTest LEqual

		Pass
		{
			Tags { "RenderType" = "Opaque" "RenderQueue" = "Geometry" }
			Blend[_SrcFactor][_DstFactor]
			
			Name "Bending Vectors"
			
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			ENDHLSL
		}
	}

	FallBack "Hidden/InternalErrorShader"
}