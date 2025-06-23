Shader "AVProVideo/Background/AVProVideo-ApplyToFarPlane"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_ChromaTex ("Chroma", 2D) = "gray" {}
		_Color("Main Color", Color) = (1,1,1,1)
		[Toggle(APPLY_GAMMA)] _ApplyGamma("Apply Gamma", Float) = 0
		[Toggle(USE_YPCBCR)] _UseYpCbCr("Use YpCbCr", Float) = 0
		_Alpha("Alpha", Float) = 1
		_DrawOffset("Draw Offset", Vector) = (0,0,0,0)
		_CustomScale("Custom Scaling", Vector) = (0,0,0,0)
		_Aspect("Aspect Ratio", Float) = 1
		//_TargetCamID("Target Camera", Float) = 0
		//_CurrentCamID("Current Rendering Camera", Float) = 0
	}
	SubShader
	{
		// this is the important part that makes it render behind all of the other object, we set it to be 0 in the queue 
		// Geometry is 2000 and you cant just put a number so Geometry-2000 it is
		Tags { "Queue" = "Geometry-2000" "RenderType"="Opaque" }
		LOD 100
		// then set ZWrite to off so all other items are drawn infront of this one, this is important as the actual object
		// for this is at the near clipping plane of the camera
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// TODO: replace use multi_compile_local instead (Unity 2019.1 feature)
			#pragma multi_compile __ APPLY_GAMMA
			#pragma multi_compile __ USE_YPCBCR

			#pragma multi_compile_fog

			#include "UnityCG.cginc"
			#include "../../../Runtime/Shaders/AVProVideo.cginc"

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			uniform sampler2D _MainTex;
#if USE_YPCBCR
			uniform sampler2D _ChromaTex; 
			uniform float4x4 _YpCbCrTransform;
#endif
			uniform float4 _MainTex_ST;
			uniform float4 _MainTex_TexelSize;
			uniform fixed4 _Color;
			uniform float _Alpha;
			uniform float2 _DrawOffset;
			uniform float _Aspect;
			uniform float2 _CustomScale;
			uniform int _TargetCamID;
			uniform int _CurrentCamID;

			v2f vert(appdata_img v)
			{
				v2f o;
				// if our position is within 2 unitys of the camera position that is being rendered to
				if (_TargetCamID == _CurrentCamID)
				{
					// scaling
					float height = 1;
					float width = 1;
					// only use AspectRatio scaling if a custom scale has not been set
					if (_CustomScale.x == 0 || _CustomScale.y == 0)
					{
						float2 targetSize = float2(_MainTex_TexelSize.z, _MainTex_TexelSize.w);
						float2 currentSize = float2(_ScreenParams.x / 2, _ScreenParams.y / 2);
						float2 targetAreaSize = float2(_ScreenParams.x, _ScreenParams.y);
						float originalAspectRatio = targetSize.x / targetSize.y;
						float baseTextureAspectRatio = currentSize.x / currentSize.y;
						float targetAspectRatio = baseTextureAspectRatio;
						int finalWidth, finalHeight;

						if (_Aspect == 0) // No Scaling
						{
							// no change wanted here so set the final size to be the size
							// of the orignal image
							finalWidth = (int)targetSize.x;
							finalHeight = (int)targetSize.y;
						}
						else if (_Aspect == 1) // Fit Vertically
						{
							// set the height to that of the target area then mutliply
							// the height by the orignal aspect ratio to ensure that the image
							// stays with the correct aspect.
							finalHeight = (int)targetAreaSize.y;
							finalWidth = round(finalHeight * originalAspectRatio);
						}
						else if (_Aspect == 2) // Fit Horizontally
						{
							// do the same as with FitVertically, just replace the width and heights
							finalWidth = (int)targetAreaSize.x;
							finalHeight = round(finalWidth / originalAspectRatio);
						}
						else if (_Aspect == 3) // Fit Inside
						{
							// if the width is larger then expand to be the same as the target area,
							// cropping the height
							if (targetAspectRatio < originalAspectRatio)
							{
								finalWidth = (int)targetAreaSize.x;
								finalHeight = round(finalWidth / originalAspectRatio);
							}
							// if the height is larger then expand to be the same as the target area,
							// cropping the width
							else
							{
								finalHeight = (int)targetAreaSize.y;
								finalWidth = round(finalHeight * originalAspectRatio);
							}
						}
						else if (_Aspect == 4) // Fit Outside 
						{
							// if the width is smaller, then expand the width to be the same 
							// size as the target then expand the height much like above to ensure
							// that the correct aspect ratio is kept
							if (targetAspectRatio > originalAspectRatio)
							{
								finalWidth = (int)targetAreaSize.x;
								finalHeight = round(finalWidth / originalAspectRatio);
							}
							// if the hight is small, expand that first then make the width follow
							else
							{
								finalHeight = (int)targetAreaSize.y;
								finalWidth = round(finalHeight * originalAspectRatio);
							}
						}
						else if (_Aspect == 5) // Stretch
						{
							// set the width and the height to be the same size as the target area
							finalWidth = (int)targetAreaSize.x;
							finalHeight = (int)targetAreaSize.y;
						}
						else // No Scalling
						{
							// make no change keeping them as the orignal texture size (1/4) of the screen
							finalWidth = (int)currentSize.x;
							finalHeight = (int)currentSize.y;
						}

						height = (float)finalHeight / (float)_ScreenParams.y;
						width = (float)finalWidth / (float)_ScreenParams.x;
					}
					else
					{
						// use custom scaling
						width = _CustomScale.x / (float)_ScreenParams.x;
						height = _CustomScale.y / (float)_ScreenParams.y;
					}
					float2 pos = (v.vertex.xy - float2(0.5, 0.5) + _DrawOffset.xy) * 2.0;
					pos.x *= width;
					pos.y *= height;

					// flip if needed then done
					if (_ProjectionParams.x < 0.0)
					{
						pos.y = (1.0 - pos.y) - 1.0;
					}
					o.vertex = float4(pos.xy, UNITY_NEAR_CLIP_VALUE, 1.0);
					o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
					return o;
				}
				else
				{
					o.vertex = UnityObjectToClipPos(float4(0,0,0,0));
					o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
					return o;
				}
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col;
#if USE_YPCBCR
				col = SampleYpCbCr(_MainTex, _ChromaTex, i.uv, _YpCbCrTransform);
#else
				col = SampleRGBA(_MainTex, i.uv);
#endif
				col *= _Color;
				// alpha now avaialbe to be controleld via user
				return fixed4(col.rgb, _Alpha);
			}
			ENDCG
		}
	}
}
