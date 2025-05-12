Shader "GPUInstancerPro/AsteroidHaze"
{
	Properties
	{
		_MainTex("Texture Sample 0", 2D) = "white" {}
		_Color("Color", Color) = (0.772549,0.7176471,0.8509804,1)
		_FadeDistance("FadeDistance", Range( 0.1 , 100)) = 50
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Transparent"  "Queue" = "Transparent+0" "IgnoreProjector" = "True" "IsEmissive" = "true"  }
		Cull Back
		CGPROGRAM
		#include "UnityCG.cginc"
		#include_with_pragmas "Packages/com.gurbu.gpui-pro/Runtime/Shaders/Include/GPUInstancerSetup.hlsl"
		#pragma instancing_options procedural:setupGPUI
		#pragma multi_compile_instancing
		#include "UnityShaderVariables.cginc"
		#pragma target 3.0
		#pragma surface surf Standard alpha:fade keepalpha noshadow nofog nometa noforwardadd vertex:vertexDataFunc 
		struct Input
		{
			float2 uv_texcoord;
			float3 worldPos;
		};

		uniform float4 _Color;
		uniform sampler2D _MainTex;
		uniform float _FadeDistance;

		void vertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			float3 appendResult108 = (float3(unity_CameraToWorld[ 2 ][ 0 ] , unity_CameraToWorld[ 2 ][ 1 ] , unity_CameraToWorld[ 2 ][ 2 ]));
			float3 normalizeResult110 = normalize( appendResult108 );
			float3 ReverseCameraViewVector117 = normalizeResult110;
			v.normal = ReverseCameraViewVector117;
			//Calculate new billboard vertex position and normal;
			float3 upCamVec = normalize ( UNITY_MATRIX_V._m10_m11_m12 );
			float3 forwardCamVec = -normalize ( UNITY_MATRIX_V._m20_m21_m22 );
			float3 rightCamVec = normalize( UNITY_MATRIX_V._m00_m01_m02 );
			float4x4 rotationCamMatrix = float4x4( rightCamVec, 0, upCamVec, 0, forwardCamVec, 0, 0, 0, 0, 1 );
			v.normal = normalize( mul( float4( v.normal , 0 ), rotationCamMatrix ));
			v.vertex.x *= length( unity_ObjectToWorld._m00_m10_m20 );
			v.vertex.y *= length( unity_ObjectToWorld._m01_m11_m21 );
			v.vertex.z *= length( unity_ObjectToWorld._m02_m12_m22 );
			v.vertex = mul( v.vertex, rotationCamMatrix );
			v.vertex.xyz += unity_ObjectToWorld._m03_m13_m23;
			//Need to nullify rotation inserted by generated surface shader;
			v.vertex = mul( unity_WorldToObject, v.vertex );
		}

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 uv_TexCoord9 = i.uv_texcoord * float2( 0.8,0.8 ) + float2( 0.1,0.1 );
			float4 transform95 = mul(unity_ObjectToWorld,float4( 0,0,0,1 ));
			float cos6 = cos( ( ( _Time.y * 0.05 ) + radians( ( transform95.x * transform95.y * transform95.z ) ) ) );
			float sin6 = sin( ( ( _Time.y * 0.05 ) + radians( ( transform95.x * transform95.y * transform95.z ) ) ) );
			float2 rotator6 = mul( uv_TexCoord9 - float2( 0.5,0.5 ) , float2x2( cos6 , -sin6 , sin6 , cos6 )) + float2( 0.5,0.5 );
			float2 RotationOverTime53 = rotator6;
			float4 tex2DNode2 = tex2D( _MainTex, RotationOverTime53 );
			float4 temp_output_4_0 = ( _Color * tex2DNode2 );
			o.Albedo = temp_output_4_0.rgb;
			o.Emission = temp_output_4_0.rgb;
			float3 ase_worldPos = i.worldPos;
			_FadeDistance *= 5;
			float clampResult35 = clamp( distance( ase_worldPos , _WorldSpaceCameraPos ) , 0 , _FadeDistance );
			float DistanceFade41 = clampResult35 / _FadeDistance;
			o.Alpha = ( temp_output_4_0.a * DistanceFade41 );
		}

		ENDCG
	}
}