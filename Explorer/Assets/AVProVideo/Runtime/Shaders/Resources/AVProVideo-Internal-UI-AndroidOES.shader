Shader "AVProVideo/Internal/UI/Stereo - AndroidOES"
{
	Properties
	{
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "black" { }
		[PerRendererData] _ChromaTex("Sprite Texture", 2D) = "gray" { }
		_Color("Tint", Color) = (1,1,1,1)

		_StencilComp("Stencil Comparison", Float) = 8
		_Stencil("Stencil ID", Float) = 0
		_StencilOp("Stencil Operation", Float) = 0
		_StencilWriteMask("Stencil Write Mask", Float) = 255
		_StencilReadMask("Stencil Read Mask", Float) = 255

		_ColorMask("Color Mask", Float) = 15

		// TODO: replace use multi_compile_local instead (Unity 2019.1 feature)
		[KeywordEnum(None, Top_Bottom, Left_Right)] Stereo("Stereo Mode", Float) = 0
		[KeywordEnum(None, Left, Right)] ForceEye ("Force Eye Mode", Float) = 0
		[Toggle(STEREO_DEBUG)] _StereoDebug("Stereo Debug Tinting", Float) = 0
		[Toggle(APPLY_GAMMA)] _ApplyGamma("Apply Gamma", Float) = 0
		[Toggle(USE_YPCBCR)] _UseYpCbCr("Use YpCbCr", Float) = 0
	}

	SubShader
	{
		Tags
		{
			"Queue" = "Transparent"
			"IgnoreProjector" = "True"
			"RenderType" = "Transparent"
			"PreviewType" = "Plane"
			"CanUseSpriteAtlas" = "True"
		}

		Stencil
		{
			Ref[_Stencil]
			Comp[_StencilComp]
			Pass[_StencilOp]
			ReadMask[_StencilReadMask]
			WriteMask[_StencilWriteMask]
		}

		Cull Off
		Lighting Off
		ZWrite Off
		ZTest[unity_GUIZTestMode]
		Fog{ Mode Off }
		Blend SrcAlpha OneMinusSrcAlpha
		ColorMask[_ColorMask]

		Pass
		{
			GLSLPROGRAM
			#pragma only_renderers gles3

			#pragma multi_compile MONOSCOPIC STEREO_TOP_BOTTOM STEREO_LEFT_RIGHT STEREO_CUSTOM_UV
			#pragma multi_compile FORCEEYE_NONE FORCEEYE_LEFT FORCEEYE_RIGHT
			#pragma multi_compile __ STEREO_DEBUG
			#pragma multi_compile __ APPLY_GAMMA
			#pragma multi_compile __ USING_DEFAULT_TEXTURE
			#pragma multi_compile __ USING_URP

			#extension GL_OES_EGL_image_external : require
			#extension GL_OES_EGL_image_external_essl3 : enable

			precision mediump float;

			#include "UnityCG.glslinc"
			#if defined(STEREO_MULTIVIEW_ON)
				UNITY_SETUP_STEREO_RENDERING
			#endif
			#define SHADERLAB_GLSL
			#include "../AVProVideo.cginc"

			#ifdef VERTEX

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
	
			out vec2 texVal;
			uniform vec4 _MainTex_ST;
			uniform mat4 _MainTex_Xfrm;

			#if defined(STEREO_DEBUG)
				out vec4 tint;
			#endif

			void main()
			{
				#if defined(STEREO_MULTIVIEW_ON)
					int eyeIndex = SetupStereoEyeIndex();
					mat4 vpMatrix = GetStereoMatrixVP(eyeIndex);
					gl_Position = vpMatrix * unity_ObjectToWorld * gl_Vertex;
				#else
					gl_Position = XFormObjectToClip(gl_Vertex);
				#endif

				// Apply texture transformation matrix - adjusts for offset/cropping (when the decoder decodes in blocks that overrun the video frame size, it pads)
				texVal.xy = (_MainTex_Xfrm * vec4(gl_MultiTexCoord0.x, gl_MultiTexCoord0.y, 0.0, 1.0)).xy;
				texVal.xy = TRANSFORM_TEX_ST(texVal, _MainTex_ST);

				#if defined(STEREO_TOP_BOTTOM) | defined(STEREO_LEFT_RIGHT)
					vec4 scaleOffset = GetStereoScaleOffset(Android_IsStereoEyeLeft(), false);
					texVal.xy *= scaleOffset.xy;
					texVal.xy += scaleOffset.zw;
				#elif defined (STEREO_CUSTOM_UV)
					if (!Android_IsStereoEyeLeft())
					{
						texVal = gl_MultiTexCoord1.xy;
						texVal = vec2(1.0, 1.0) - texVal;
					}
				#endif

				#if defined(STEREO_DEBUG)
					tint = GetStereoDebugTint(Android_IsStereoEyeLeft());
				#endif
			}
			#endif	// VERTEX

			#ifdef FRAGMENT
			in vec2 texVal;

			#if defined(STEREO_DEBUG)
				in vec4 tint;
			#endif

			uniform vec4 _Color;
			#if defined(USING_DEFAULT_TEXTURE)
				uniform sampler2D _MainTex;
			#else
				uniform samplerExternalOES _MainTex;
			#endif

			void main()
			{
				vec4 col = texture(_MainTex, texVal.xy);

				#if defined(APPLY_GAMMA)
					col.rgb = GammaToLinear(col.rgb);
				#endif

				col *= _Color;

				#if defined(STEREO_DEBUG)
					col *= tint;
				#endif

				gl_FragColor = col;
			}
			#endif	// FRAGMENT

			ENDGLSL
		}
	}

	Fallback "AVProVideo/Internal/UI/Stereo"
}
