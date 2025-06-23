Shader "AVProVideo/Unlit/Transparent (texture+alpha support) - Android OES ONLY"
{
	Properties
	{
		_MainTex ("Base (RGB)", 2D) = "black" { }
		_ChromaTex("Chroma", 2D) = "gray" { }			// For fallback shader
		_Color("Main Color", Color) = (1,1,1,1)			// For fallback shader

		[KeywordEnum(None, Top_Bottom, Left_Right)] AlphaPack("Alpha Pack", Float) = 0
		[Toggle(APPLY_GAMMA)] _ApplyGamma("Apply Gamma", Float) = 0
		[Toggle(USE_YPCBCR)] _UseYpCbCr("Use YpCbCr", Float) = 0
	}
	SubShader
	{
		Tags { "RenderType"="Transparent" "IgnoreProjector"="True" "Queue"="Transparent" }
		LOD 100
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha
		Lighting Off
		Cull Off

		Pass
		{
			GLSLPROGRAM

			#pragma only_renderers gles3
			// TODO: replace use multi_compile_local instead (Unity 2019.1 feature)
			#pragma multi_compile ALPHAPACK_NONE ALPHAPACK_TOP_BOTTOM ALPHAPACK_LEFT_RIGHT
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
			#include "AVProVideo.cginc"

			#ifdef VERTEX

			#if defined(ALPHAPACK_TOP_BOTTOM) || defined(ALPHAPACK_LEFT_RIGHT)
			out vec4 texVal;
			#else
			out vec2 texVal;
			#endif
			
			uniform vec4 _MainTex_ST;
			uniform vec4 _MainTex_TexelSize;
			uniform mat4 _MainTex_Xfrm;

			void main()
			{
				gl_Position = XFormObjectToClip(gl_Vertex);

				texVal.xy = gl_MultiTexCoord0.xy;

				// Apply texture transformation matrix - adjusts for offset/cropping (when the decoder decodes in blocks that overrun the video frame size, it pads)
				texVal.xy = (_MainTex_Xfrm * vec4(texVal.x, texVal.y, 0.0, 1.0) ).xy;
				texVal.xy = TRANSFORM_TEX_ST(texVal, _MainTex_ST);

				#if defined(ALPHAPACK_TOP_BOTTOM) || defined(ALPHAPACK_LEFT_RIGHT)
					texVal = OffsetAlphaPackingUV(_MainTex_TexelSize.xy, texVal.xy, _MainTex_ST.y < 0.0);
					#if defined(ALPHAPACK_TOP_BOTTOM)
						texVal.yw = texVal.wy;
					#endif
				#endif
			}
			#endif	// VERTEX

			#ifdef FRAGMENT

			#if defined(ALPHAPACK_TOP_BOTTOM) || defined(ALPHAPACK_LEFT_RIGHT)
			in vec4 texVal;
			#else
			in vec2 texVal;
			#endif

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

				#if defined(ALPHAPACK_TOP_BOTTOM) || defined(ALPHAPACK_LEFT_RIGHT)
					vec3 rgb = texture(_MainTex, texVal.zw).rgb;
					col.a = (rgb.r + rgb.g + rgb.b) / 3.0;
				#endif
				
				gl_FragColor = col;
			}
			#endif

			ENDGLSL
		}
	}
	
	Fallback "AVProVideo/Unlit/Transparent (texture+color+fog+stereo+alpha)"
}
