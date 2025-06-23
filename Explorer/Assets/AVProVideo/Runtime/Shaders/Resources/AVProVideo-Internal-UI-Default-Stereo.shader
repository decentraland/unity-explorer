Shader "AVProVideo/Internal/UI/Stereo"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" { }
		[PerRendererData] _ChromaTex ("Sprite Texture", 2D) = "white" { }
		_Color ("Tint", Color) = (1, 1, 1, 1)

		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255

		_ColorMask ("Color Mask", Float) = 15

		[KeywordEnum(None, Top_Bottom, Left_Right, Two_Textures)] Stereo("Stereo Mode", Float) = 0
		[KeywordEnum(None, Left, Right)] ForceEye ("Force Eye Mode", Float) = 0
		[Toggle(STEREO_DEBUG)] _StereoDebug("Stereo Debug Tinting", Float) = 0
		[Toggle(APPLY_GAMMA)] _ApplyGamma("Apply Gamma", Float) = 0
		[Toggle(USE_YPCBCR)] _UseYpCbCr("Use YpCbCr", Float) = 0
	}

	SubShader
	{
		Tags
		{
			"Queue"="Transparent"
			"IgnoreProjector"="True"
			"RenderType"="Transparent"
			"PreviewType"="Plane"
			"CanUseSpriteAtlas"="True"
		}

		Stencil
		{
			Ref [_Stencil]
			Comp [_StencilComp]
			Pass [_StencilOp]
			ReadMask [_StencilReadMask]
			WriteMask [_StencilWriteMask]
		}

		Cull Off
		Lighting Off
		ZWrite Off
		ZTest [unity_GUIZTestMode]
		Fog { Mode Off }
		Blend SrcAlpha OneMinusSrcAlpha
		ColorMask [_ColorMask]

		Pass
		{
		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// TODO: replace use multi_compile_local instead (Unity 2019.1 feature)
			#pragma multi_compile MONOSCOPIC STEREO_TOP_BOTTOM STEREO_LEFT_RIGHT STEREO_TWO_TEXTURES
			#pragma multi_compile FORCEEYE_NONE FORCEEYE_LEFT FORCEEYE_RIGHT
			#pragma multi_compile __ APPLY_GAMMA
			#pragma multi_compile __ STEREO_DEBUG
			#pragma multi_compile __ USE_YPCBCR

			#include "UnityCG.cginc"
			#include "../AVProVideo.cginc"

			struct appdata_t
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			#ifdef UNITY_STEREO_INSTANCING_ENABLED
				UNITY_VERTEX_INPUT_INSTANCE_ID
			#endif
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				half4 color : COLOR;
				half2 texcoord : TEXCOORD0;
			#ifdef UNITY_STEREO_INSTANCING_ENABLED
				UNITY_VERTEX_OUTPUT_STEREO
			#endif
			};

			uniform half4 _Color;

			uniform sampler2D _MainTex;
		#if STEREO_TWO_TEXTURES
			uniform sampler2D _MainTex_R;
		#endif

		#if USE_YPCBCR
			uniform sampler2D _ChromaTex;
			#if STEREO_TWO_TEXTURES
				uniform sampler2D _ChromaTex_R;
			#endif
			uniform float4x4 _YpCbCrTransform;
		#endif

			uniform float4 _MainTex_ST;
			uniform float4 _MainTex_TexelSize;
			uniform float4x4 _MainTex_Xfrm;

			v2f vert(appdata_t i)
			{
				v2f o;

			#ifdef UNITY_STEREO_INSTANCING_ENABLED
				UNITY_SETUP_INSTANCE_ID(i);
				UNITY_INITIALIZE_OUTPUT(v2f, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
			#endif

				o.vertex = XFormObjectToClip(i.vertex);

			#if UNITY_HALF_TEXEL_OFFSET
				o.vertex.xy += (_ScreenParams.zw - 1.0) * float2(-1.0, 1.0);
			#endif

				o.texcoord.xy = mul(_MainTex_Xfrm, float4(i.texcoord.xy, 0.0, 1.0)).xy;
				o.texcoord.xy = TRANSFORM_TEX(o.texcoord.xy, _MainTex);

			#if STEREO_TOP_BOTTOM || STEREO_LEFT_RIGHT
				float4 scaleOffset = GetStereoScaleOffset(IsStereoEyeLeft(), _MainTex_ST.y < 0.0);
				o.texcoord.xy *= scaleOffset.xy;
				o.texcoord.xy += scaleOffset.zw;
			#endif

				o.color = i.color * _Color;

			#if STEREO_DEBUG
				o.color *= GetStereoDebugTint(IsStereoEyeLeft());
			#endif

				return o;
			}

			inline half4 sampleTextureForEye(float2 uv, bool rightEye)
			{
			#if STEREO_TWO_TEXTURES
				if (rightEye)
				{
				#if USE_YPCBCR
					return SampleYpCbCr(_MainTex_R, _ChromaTex_R, uv, _YpCbCrTransform);
				#else
					return SampleRGBA(_MainTex_R, uv);
				#endif
				}
				else
			#endif
				{
				#if USE_YPCBCR
					return SampleYpCbCr(_MainTex, _ChromaTex, uv, _YpCbCrTransform);
				#else
					return SampleRGBA(_MainTex, uv);
				#endif
				}
			}

			half4 frag(v2f i) : SV_Target
			{
			#ifdef UNITY_STEREO_INSTANCING_ENABLED
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
			#endif

				half4 col = sampleTextureForEye(i.texcoord, IsStereoEyeRight());
				col *= i.color;
				return col;
			}

		ENDCG
		}
	}
}
