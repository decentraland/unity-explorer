Shader "AVProVideo/Unlit/Opaque (texture+color+stereo support) - Android OES ONLY"
{
	Properties
	{
		_MainTex ("Base (RGB)", 2D) = "black" { }
		_ChromaTex("Chroma", 2D) = "gray" { }			// For fallback shader
		_Color("Main Color", Color) = (1,1,1,1)			// For fallback shader

		[KeywordEnum(None, Top_Bottom, Left_Right)] Stereo("Stereo Mode", Float) = 0
		[KeywordEnum(None, Left, Right)] ForceEye ("Force Eye Mode", Float) = 0
		[Toggle(STEREO_DEBUG)] _StereoDebug("Stereo Debug Tinting", Float) = 0
		[Toggle(APPLY_GAMMA)] _ApplyGamma("Apply Gamma", Float) = 0
		[Toggle(USE_YPCBCR)] _UseYpCbCr("Use YpCbCr", Float) = 0
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" "IgnoreProjector"="False" "Queue"="Geometry" }
		LOD 100
		Lighting Off
		Cull Off

		Pass
		{
			GLSLPROGRAM

			#pragma only_renderers gles3
			// TODO: replace use multi_compile_local instead (Unity 2019.1 feature)
			#pragma multi_compile MONOSCOPIC STEREO_TOP_BOTTOM STEREO_LEFT_RIGHT STEREO_CUSTOM_UV
			#pragma multi_compile FORCEEYE_NONE FORCEEYE_LEFT FORCEEYE_RIGHT
			#pragma multi_compile __ APPLY_GAMMA
			#pragma multi_compile __ USING_DEFAULT_TEXTURE
			#pragma multi_compile __ STEREO_DEBUG
			#pragma multi_compile __ USING_URP

			#extension GL_OES_EGL_image_external : require
			#extension GL_OES_EGL_image_external_essl3 : enable
			precision mediump float;

			#include "UnityCG.glslinc"
			#if defined(STEREO_MULTIVIEW_ON)
				UNITY_SETUP_STEREO_RENDERING
			#endif
			#define SHADERLAB_GLSL
			#include "AVProVideo.cginc"

			#ifdef VERTEX

			out vec2 texVal;
			uniform vec4 _MainTex_ST;
			uniform mat4 _MainTex_Xfrm;

#if defined(STEREO_DEBUG)
			out vec4 tint;
#endif

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

			void main()
			{
#if defined(STEREO_MULTIVIEW_ON)
				int eyeIndex = SetupStereoEyeIndex();
				mat4 vpMatrix = GetStereoMatrixVP(eyeIndex);
				gl_Position = vpMatrix * unity_ObjectToWorld * gl_Vertex;
#else
				gl_Position = XFormObjectToClip(gl_Vertex);
#endif

				texVal = gl_MultiTexCoord0.xy;

				// Apply texture transformation matrix - adjusts for offset/cropping (when the decoder decodes in blocks that overrun the video frame size, it pads)
				texVal = (_MainTex_Xfrm * vec4(texVal.x, texVal.y, 0.0, 1.0)).xy;
				texVal = TRANSFORM_TEX_ST(texVal, _MainTex_ST);

#if defined(STEREO_TOP_BOTTOM) || defined(STEREO_LEFT_RIGHT)
				vec4 scaleOffset = GetStereoScaleOffset(Android_IsStereoEyeLeft(), false);

				texVal.xy *= scaleOffset.xy;
				texVal.xy += scaleOffset.zw;
#endif

#if defined(STEREO_DEBUG)
				tint = GetStereoDebugTint(Android_IsStereoEyeLeft());
#endif
			}
			#endif

			#ifdef FRAGMENT

			in vec2 texVal;

#if defined(USING_DEFAULT_TEXTURE)
			uniform sampler2D _MainTex;
#else
			uniform samplerExternalOES _MainTex;
#endif

#if defined(STEREO_DEBUG)
			in vec4 tint;
#endif

			void main()
			{
				vec4 col = texture(_MainTex, texVal.xy);

				#if defined(APPLY_GAMMA)
				col.rgb = GammaToLinear(col.rgb);
#endif

#if defined(STEREO_DEBUG)
				col *= tint;
#endif

				gl_FragColor = col;
			}
			#endif

			ENDGLSL
		}
	}
	
	Fallback "AVProVideo/Unlit/Opaque (texture+color+fog+stereo support)"
}