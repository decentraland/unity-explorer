Shader "AVProVideo/Skybox/Sphere - Android OES"
{
	Properties
	{
		_Tint ("Tint Color", Color) = (0.5, 0.5, 0.5, 0.5)
		[Gamma] _Exposure ("Exposure", Range(0, 8)) = 1.0
		_Rotation ("Rotation", Range(0, 360)) = 0
		[NoScaleOffset] _MainTex ("MainTex (HDR)", 2D) = "grey" { }
		[NoScaleOffset] _ChromaTex ("Chroma", 2D) = "grey" { }		
		[KeywordEnum(None, Top_Bottom, Left_Right, Custom_UV)] Stereo ("Stereo Mode", Float) = 0
		[Toggle(STEREO_DEBUG)] _StereoDebug ("Stereo Debug Tinting", Float) = 0
		[KeywordEnum(None, EquiRect180)] Layout("Layout", Float) = 0
		[Toggle(APPLY_GAMMA)] _ApplyGamma("Apply Gamma", Float) = 0
		[Toggle(USE_YPCBCR)] _UseYpCbCr("Use YpCbCr", Float) = 0
	}

	SubShader
	{
		Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
		Cull Off
		ZWrite Off

		Pass
		{
			GLSLPROGRAM
			#pragma only_renderers gles3
			#pragma multi_compile MONOSCOPIC STEREO_TOP_BOTTOM STEREO_LEFT_RIGHT STEREO_CUSTOM_UV
			#pragma multi_compile FORCEEYE_NONE FORCEEYE_LEFT FORCEEYE_RIGHT
			#pragma multi_compile __ STEREO_DEBUG
			#pragma multi_compile __ APPLY_GAMMA
			#pragma multi_compile __ USE_YPCBCR
			#pragma multi_compile __ LAYOUT_EQUIRECT180

			#extension GL_OES_EGL_image_external : require
			#extension GL_OES_EGL_image_external_essl3 : enable

			precision mediump float;

			#include "UnityCG.glslinc"

		#if defined(STEREO_MULTIVIEW_ON)
			UNITY_SETUP_STEREO_RENDERING
		#endif
			
			#define SHADERLAB_GLSL
			#include "AVProVideo.cginc"

			#define fmod(x, y) ((x) - (y) * floor((x) / (y)))

			const float CONST_PI = 3.14159265359;

		//--------------------------------------------------------------------------------------------------------------
		// Vertex shader

		#ifdef VERTEX
			uniform vec4 _MainTex_ST;
			uniform float _Rotation;
			out vec3 texcoord;
		#if defined(STEREO_TOP_BOTTOM) || defined(STEREO_LEFT_RIGHT)
			out vec4 scaleOffset;
		#endif
		#if defined(STEREO_DEBUG)
			out vec4 tint;
		#endif

			vec3 RotateAroundYInDegrees(vec3 vertex, float degrees)
			{
				float alpha = degrees * CONST_PI / 180.0;
				float sina = sin(alpha);
				float cosa = cos(alpha);
				mat2 m = mat2(cosa, -sina, sina, cosa);
				return vec3(m * vertex.xz, vertex.y).xzy;
			}

			void main()
			{					
				vec3 rotated = RotateAroundYInDegrees(gl_Vertex.xyz, _Rotation);

			#if defined(STEREO_MULTIVIEW_ON)
				int eyeIndex = SetupStereoEyeIndex();
				mat4 vpMatrix = GetStereoMatrixVP(eyeIndex);
				gl_Position = vpMatrix * unity_ObjectToWorld * vec4(rotated, 0.0);
			#else
				gl_Position = XFormObjectToClip(vec4(rotated, 0.0));
			#endif

			#if defined(FORCEEYE_LEFT)
				bool isLeftEye = true;
			#elif defined(FORCEEYE_RIGHT)
				bool isLeftEye = false;
			#elif defined(STEREO_MULTIVIEW_ON)
				bool isLeftEye = eyeIndex == 0;
			#else
				bool isLeftEye = true;
			#endif

				texcoord = gl_Vertex.xyz;

			#if defined(STEREO_TOP_BOTTOM) || defined(STEREO_LEFT_RIGHT)
				scaleOffset = GetStereoScaleOffset(isLeftEye, true);
			#endif
			#if defined(STEREO_DEBUG)
				tint = GetStereoDebugTint(isLeftEye);
			#endif
			}
		#endif // VERTEX

		//--------------------------------------------------------------------------------------------------------------
		// Fragment shader

		#ifdef FRAGMENT
			in vec3 texcoord;
		#if defined(STEREO_TOP_BOTTOM) || defined(STEREO_LEFT_RIGHT)
			in vec4 scaleOffset;
		#endif
		#if defined(STEREO_DEBUG)
			in vec4 tint;
		#endif

		#if defined(USING_DEFAULT_TEXTURE)
			uniform sampler2D _MainTex;
		#else
			uniform samplerExternalOES _MainTex;
		#endif
			uniform vec4 _MainTex_ST;
			uniform mat4 _MainTex_Xfrm;
			uniform float _Exposure;
			uniform vec4 _Tint;

			vec2 toRadialCoords(vec3 coords)
			{
				vec3 normalizedCoords = normalize(coords);
				float latitude = acos(normalizedCoords.y);
				float longitude = atan(normalizedCoords.z, normalizedCoords.x);
				vec2 sphereCoords = vec2(longitude, latitude) * vec2(0.5 / CONST_PI, 1.0 / CONST_PI);
				vec2 radial = vec2(0.5, 1.0) - sphereCoords;
				radial.x += 0.25;
				radial.x = fmod(radial.x, 1.0);
				return radial;
			}

			vec2 transformTex(vec2 texCoord, vec4 texST) 
			{
				return texCoord * texST.xy + texST.zw;
			}

			void main()
			{
				vec2 tc = toRadialCoords(texcoord);
			#if defined(LAYOUT_EQUIRECT180)
				tc.x = saturate(((tc.x - 0.5) * 2.0) + 0.5);
			#endif
				tc = (_MainTex_Xfrm * vec4(tc.x, tc.y, 0.0, 1.0)).xy;

			#if defined(STEREO_TOP_BOTTOM) || defined(STEREO_LEFT_RIGHT)
				tc.xy *= scaleOffset.xy;
				tc.xy += scaleOffset.zw;
			#endif
	
				tc = transformTex(tc, _MainTex_ST);
	
				vec4 col = texture(_MainTex, tc);
			#if defined(APPLY_GAMMA)
				col.rgb = GammaToLinear(col.rgb);
			#endif
				col.rgb *= _Exposure;
				// col.rgb *= _Tint.rgb;

			#if defined(STEREO_DEBUG)
				col *= tint;
			#endif
				
				gl_FragColor = col;
			}
		
		#endif // FRAGMENT
			ENDGLSL
		}
	}
	
	Fallback "AVProVideo/Skybox/Sphere"
}
