Shader "AVProVideo/Internal/ResolveOES"
{
	Properties
	{
		_MainTex("Texture", any) = "" {}
		_ChromaTex("Chroma", any) = "" {}
		_Color ("Tint", Color) = (1,1,1,1)
		_VertScale("Vertical Scale", Range(-1, 1)) = 1.0

		[Toggle(USE_HSBC)] _UseHSBC("Use HSBC", Float) = 0
		_Hue("Hue", Range(0, 1.0)) = 0
		_Saturation("Saturation", Range(0, 1.0)) = 0.5
		_Brightness("Brightness", Range(0, 1.0)) = 0.5
		_Contrast("Contrast", Range(0, 1.0)) = 0.5
		_InvGamma("InvGamma", Range(0.0001, 10000.0)) = 1.0

		[KeywordEnum(None, Top_Bottom, Left_Right)] Stereo("Stereo Mode", Float) = 0
		[KeywordEnum(None, Left, Right)] ForceEye ("Force Eye Mode", Float) = 0		
		[KeywordEnum(None, Top_Bottom, Left_Right)] AlphaPack("Alpha Pack", Float) = 0
		[Toggle(APPLY_GAMMA)] _ApplyGamma("Apply Gamma", Float) = 0
		[Toggle(USE_YPCBCR)] _UseYpCbCr("Use YpCbCr", Float) = 0
	}

	SubShader
	{
		Tags
		{
			"IgnoreProjector" = "True"
			"PreviewType" = "Plane"
		}

		Lighting Off
		Cull Off
		ZWrite Off
		ZTest Always

		Pass
		{
			Name "RESOLVE-OES"

			GLSLPROGRAM
			#pragma only_renderers gles gles3

			// TODO: replace use multi_compile_local instead (Unity 2019.1 feature)
			#pragma multi_compile MONOSCOPIC STEREO_TOP_BOTTOM STEREO_LEFT_RIGHT
			#pragma multi_compile ALPHAPACK_NONE ALPHAPACK_TOP_BOTTOM ALPHAPACK_LEFT_RIGHT
			#pragma multi_compile FORCEEYE_NONE FORCEEYE_LEFT FORCEEYE_RIGHT
			#pragma multi_compile __ APPLY_GAMMA
			#pragma multi_compile __ USE_HSBC
			#pragma multi_compile __ USING_URP

			#extension GL_OES_EGL_image_external : require
			#extension GL_OES_EGL_image_external_essl3 : enable

			#include "UnityCG.glslinc"
		#if defined(STEREO_MULTIVIEW_ON)
			UNITY_SETUP_STEREO_RENDERING
		#endif
			#define SHADERLAB_GLSL
			#include "../AVProVideo.cginc"

			#ifdef VERTEX

			varying vec4 varTexCoord;
			varying vec4 varColor;

			uniform vec4 _Color;
			uniform vec4 _MainTex_ST;
			uniform vec4 _MainTex_TexelSize;
			uniform mat4 _MainTex_Xfrm;
			uniform float _VertScale;

			INLINE bool Android_IsStereoEyeLeft()
			{
				#if defined(FORCEEYE_LEFT)
					return true;
				#elif defined(FORCEEYE_RIGHT)
					return false;
				#elif defined(STEREO_MULTIVIEW_ON)
					int eyeIndex = SetupStereoEyeIndex();
					return (eyeIndex == 0);
				#else
					return IsStereoEyeLeft();
				#endif
			}

			vec2 transformTex(vec4 texCoord, vec4 texST)
			{
				return (texCoord.xy * texST.xy + texST.zw);
			}

			void main()
			{
#if defined(STEREO_MULTIVIEW_ON)
				int eyeIndex = SetupStereoEyeIndex();
				mat4 vpMatrix = GetStereoMatrixVP(eyeIndex);
				gl_Position = vpMatrix * unity_ObjectToWorld * gl_Vertex;
#else
				gl_Position = XFormObjectToClip(gl_Vertex);
#endif

				varColor = gl_Color * _Color;

				// Apply texture transformation matrix - adjusts for offset/cropping (when the decoder decodes in blocks that overrun the video frame size, it pads)
				varTexCoord.xy = (_MainTex_Xfrm * vec4(gl_MultiTexCoord0.x, gl_MultiTexCoord0.y, 0.0, 1.0)).xy;
				varTexCoord.zw = vec2(0.0, 0.0);

			#if defined(STEREO_TOP_BOTTOM) || defined(STEREO_LEFT_RIGHT)
				vec4 scaleOffset = GetStereoScaleOffset(Android_IsStereoEyeLeft(), false);
				varTexCoord.xy *= scaleOffset.xy;
				varTexCoord.xy += scaleOffset.zw;
			#endif

			#if defined (ALPHAPACK_TOP_BOTTOM) || defined(ALPHAPACK_LEFT_RIGHT)
				varTexCoord = OffsetAlphaPackingUV(_MainTex_TexelSize.xy, varTexCoord.xy, false);
				#if defined(ALPHAPACK_TOP_BOTTOM)
				varTexCoord.yw = varTexCoord.wy;
				#endif
			#endif
			}

			#endif

			#ifdef FRAGMENT

			varying vec4 varTexCoord;
			varying vec4 varColor;

			uniform samplerExternalOES _MainTex;
		#if defined(USE_HSBC)
			uniform	float _Hue, _Saturation, _Brightness, _Contrast, _InvGamma;
		#endif

			void main()
			{
				vec4 col = TEX_EXTERNAL(_MainTex, varTexCoord.xy);
			#if defined(APPLY_GAMMA)
				col.rgb = GammaToLinear(col.rgb);
			#endif

			#if defined(ALPHAPACK_TOP_BOTTOM) || defined(ALPHAPACK_LEFT_RIGHT)
				vec4 colAlpha = TEX_EXTERNAL(_MainTex, varTexCoord.zw);
				col.a = (colAlpha.r + colAlpha.g + colAlpha.b) / 3.0;
			#endif

			#if defined(USE_HSBC)
				col.rgb = ApplyHSBEffect(col.rgb, vec4(_Hue, _Saturation, _Brightness, _Contrast));
				col.rgb = pow(col.rgb, vec3(_InvGamma));
			#endif

				gl_FragColor = col * varColor;
			}

			#endif

			ENDGLSL
		}
	}

	Fallback off
}