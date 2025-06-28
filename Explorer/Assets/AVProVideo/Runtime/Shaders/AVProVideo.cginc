//-----------------------------------------------------------------------------
// Copyright 2015-2021 RenderHeads Ltd.  All rights reserverd.
//-----------------------------------------------------------------------------


//#define AVPRO_CHEAP_GAMMA_CONVERSION

#if defined (SHADERLAB_GLSL)
	#define AVPRO_CHEAP_GAMMA_CONVERSION
	#define INLINE
	#define FIXED float
	#define HALF float
	#define HALF2 vec2
	#define HALF3 vec3
	#define HALF4 vec4
	#define FLOAT2 vec2
	#define FLOAT3 vec3
	#define FLOAT4 vec4
	#define FIXED4 vec4
	#define FLOAT3X3 mat3
	#define FLOAT4X4 mat4
	#define LERP mix
#else
	#define INLINE inline
	#define FIXED fixed
	#define HALF half
	#define HALF2 half2
	#define HALF3 half3
	#define HALF4 half4
	#define FLOAT2 float2
	#define FLOAT3 float3
	#define FLOAT4 float4
	#define FIXED4 fixed4
	#define FLOAT3X3 float3x3
	#define FLOAT4X4 float4x4
	#define LERP lerp
#endif

// Specify this so Unity doesn't automatically update our shaders.
#define UNITY_SHADER_NO_UPGRADE 1

//#pragma multi_compile __ XR_USE_BUILT_IN_EYE_VARIABLE

// We use this method so that when Unity automatically updates the shader from the old
// mul(UNITY_MATRIX_MVP.. to UnityObjectToClipPos that it only changes in one place.
INLINE FLOAT4 XFormObjectToClip(FLOAT4 vertex)
{
#if defined(SHADERLAB_GLSL)
	return gl_ModelViewProjectionMatrix * vertex;
#else
	return UnityObjectToClipPos(vertex);
#endif
}

uniform FLOAT3 _WorldCameraPosition;
uniform FLOAT3 _WorldCameraRight;

INLINE bool IsStereoEyeLeft()
{
#if defined(FORCEEYE_LEFT)
	return true;
#elif defined(FORCEEYE_RIGHT)
	return false;
#elif defined(STEREO_TWO_TEXTURES)
	return unity_StereoEyeIndex == 0;
#elif defined(USING_STEREO_MATRICES)
	// Unity 5.4 has this new variable
	return (unity_StereoEyeIndex == 0);
#elif defined (UNITY_DECLARE_MULTIVIEW)
	// OVR_multiview extension
	return (UNITY_VIEWID == 0);
#else
	#if defined(SHADERLAB_GLSL) && defined(USING_URP)
		// NOTE: Bug #1416: URP + OES
		FLOAT3 renderCameraPos = FLOAT3( gl_ModelViewMatrixInverseTranspose[0][3], gl_ModelViewMatrixInverseTranspose[1][3], gl_ModelViewMatrixInverseTranspose[2][3] );
	#elif defined(UNITY_MATRIX_I_V)
		// NOTE: Bug #1165: _WorldSpaceCameraPos is not correct in multipass VR (when skybox is used) but UNITY_MATRIX_I_V seems to be
		FLOAT3 renderCameraPos = UNITY_MATRIX_I_V._m03_m13_m23;
	#else
		FLOAT3 renderCameraPos = _WorldSpaceCameraPos.xyz;
	#endif
	
	float fL = distance(_WorldCameraPosition - _WorldCameraRight, renderCameraPos);
	float fR = distance(_WorldCameraPosition + _WorldCameraRight, renderCameraPos);
	return (fL < fR);
#endif
}

INLINE bool IsStereoEyeRight()
{
	return !IsStereoEyeLeft();
}

#if defined(STEREO_TOP_BOTTOM)
FLOAT4 GetStereoScaleOffset(bool isLeftEye, bool isYFlipped)
{
	float oy = isLeftEye ? 0.5 : 0.0;
	if (isYFlipped)
	{
		oy = 0.5 - oy;
	}
	return FLOAT4(1.0, 0.5, 0.0, oy);
}
#elif defined(STEREO_LEFT_RIGHT)
FLOAT4 GetStereoScaleOffset(bool isLeftEye, bool isYFlipped)
{
	return FLOAT4(0.5, 1.0, isLeftEye ? 0.0 : 0.5, 0.0);
}
#endif

#if defined(STEREO_DEBUG)
INLINE HALF4 GetStereoDebugTint(bool isLeftEye)
{
	#if defined(STEREO_TOP_BOTTOM) || defined(STEREO_LEFT_RIGHT) || defined(STEREO_CUSTOM_UV) || defined(STEREO_TWO_TEXTURES)
		if (isLeftEye)
		{
			return HALF4(0.0, 1.0, 0.0, 1.0);	// Left
		}
		else
		{
			return HALF4(1.0, 0.0, 0.0, 1.0);	// Right
		}
	#else
		return HALF4(1.0, 1.0, 1.0, 1.0);		// White
	#endif
}
#endif

FLOAT2 ScaleZoomToFit(float targetWidth, float targetHeight, float sourceWidth, float sourceHeight)
{
#if defined(ALPHAPACK_TOP_BOTTOM)
	sourceHeight *= 0.5;
#elif defined(ALPHAPACK_LEFT_RIGHT)
	sourceWidth *= 0.5;
#endif
	float targetAspect = targetHeight / targetWidth;
	float sourceAspect = sourceHeight / sourceWidth;
	FLOAT2 scale = FLOAT2(1.0, sourceAspect / targetAspect);
	if (targetAspect < sourceAspect)
	{
		scale = FLOAT2(targetAspect / sourceAspect, 1.0);
	}
	return scale;
}

FLOAT4 OffsetAlphaPackingUV(FLOAT2 texelSize, FLOAT2 uv, bool flipVertical)
{
	if (flipVertical)
	{
		uv.y = 1.0 - uv.y;
	}
	
	FLOAT4 result = uv.xyxy;

	// We don't want bilinear interpolation to cause bleeding when reading the pixels at the edge of the
	// packed areas, so we shift the UV's by a fraction of a pixel so the edges don't get sampled.

	#if defined(ALPHAPACK_TOP_BOTTOM)

		float offset = texelSize.y * 1.5;
		float y = LERP(offset, 0.5 - offset, uv.y);

		// [MOZ] 250218 - UNITY_UV_STARTS_AT_TOP here breaks OpenGLES on Android, need to check to see if it's required
		// on other platforms, good on Android OpenGLES & Vulkan, Metal
		#if 1 //defined(UNITY_UV_STARTS_AT_TOP)
			result.y = 0.5 + y;
			result.w = y;
		#else
			result.y = y;
			result.w = 0.5 + y;
		#endif

		if (flipVertical)
		{
			result.yw = result.wy;
		}
	
	#elif defined(ALPHAPACK_LEFT_RIGHT)

		float offset = texelSize.x * 1.5;
		float x = LERP(offset, 0.5 - offset, uv.x);
		result.x = x;
		result.z = 0.5 + x;

	#endif

	return result;
}

INLINE HALF3 GammaToLinear_ApproxPow(HALF3 col)
{
	#if defined (SHADERLAB_GLSL)
	return pow(col, HALF3(2.2, 2.2, 2.2));
	#else
	return pow(col, HALF3(2.2h, 2.2h, 2.2h));
	#endif
}

INLINE HALF3 LinearToGamma_ApproxPow(HALF3 col)
{
	#if defined (SHADERLAB_GLSL)
	return pow(col, HALF3(1.0/2.2, 1.0/2.2, 1.0/2.2));
	#else
	return pow(col, HALF3(1.0h/2.2h, 1.0h/2.2h, 1.0h/2.2h));
	#endif
}

// Approximate version from http://chilliant.blogspot.com.au/2012/08/srgb-approximations-for-hlsl.html?m=1
// NOTE: This is about 4 instructions vs 10 instructions for the accurate version
INLINE HALF3 GammaToLinear_ApproxFit(HALF3 col)
{
#if defined (SHADERLAB_GLSL)
	HALF a = 0.305306011;
	HALF b = 0.682171111;
	HALF c = 0.012522878;
#else
	HALF a = 0.305306011h;
	HALF b = 0.682171111h;
	HALF c = 0.012522878h;
#endif
	return col * (col * (col * a + b) + c);
}

// Approximate version from http://chilliant.blogspot.com.au/2012/08/srgb-approximations-for-hlsl.html?m=1
INLINE HALF3 LinearToGamma_ApproxFit(HALF3 col)
{
#if defined (SHADERLAB_GLSL)
	HALF a = 0.416666667;
	HALF b = 0.055;
	HALF c = 0.0;
	HALF d = 1.055;
#else
	HALF a = 0.416666667h;
	HALF b = 0.055h;
	HALF c = 0.0h;
	HALF d = 1.055h;
#endif
	return max(d * pow(col, HALF3(a, a, a)) - b, c);
}

INLINE HALF3 GammaToLinear_Accurate(HALF3 col)
{
	if (col.r <= 0.04045)
		col.r = col.r / 12.92;
	else
		col.r = pow((col.r + 0.055) / 1.055, 2.4);

	if (col.g <= 0.04045)
		col.g = col.g / 12.92;
	else
		col.g = pow((col.g + 0.055) / 1.055, 2.4);

	if (col.b <= 0.04045)
		col.b = col.b / 12.92;
	else
		col.b = pow((col.b + 0.055) / 1.055, 2.4);

	// NOTE: We tried to optimise the above, but actually the compiler does a better job..
	/*HALF3 a = col / 12.92;
	HALF3 b = pow((col + 0.055) / 1.055, 2.4);
	HALF3 c = step(col,0.04045);
	col = LERP(b, a, c);*/

	return col;
}

INLINE HALF3 LinearToGamma_Accurate(HALF3 col)
{
	if (col.r <= 0.0031308)
		col.r = col.r * 12.92;
	else
		col.r = 1.055 * pow(col.r, 0.4166667) - 0.055;

	if (col.g <= 0.0031308)
		col.g = col.g * 12.92;
	else
		col.g = 1.055 * pow(col.g, 0.4166667) - 0.055;

	if (col.b <= 0.0031308)
		col.b = col.b * 12.92;
	else
		col.b = 1.055 * pow(col.b, 0.4166667) - 0.055;

	return col;
}

// http://entropymine.com/imageworsener/srgbformula/
INLINE HALF3 GammaToLinear(HALF3 col)
{
#if defined(AVPRO_CHEAP_GAMMA_CONVERSION)
	return GammaToLinear_ApproxFit(col);
#else
	return GammaToLinear_Accurate(col);
#endif
}

// http://entropymine.com/imageworsener/srgbformula/
INLINE HALF3 LinearToGamma(HALF3 col)
{
#if defined(AVPRO_CHEAP_GAMMA_CONVERSION)
	return LinearToGamma_ApproxFit(col);
#else
	return LinearToGamma_Accurate(col);
#endif
}

INLINE FLOAT3 ConvertYpCbCrToRGB(FLOAT3 YpCbCr, FLOAT4X4 YpCbCrTransform)
{
#if defined(SHADERLAB_GLSL)
	return clamp(FLOAT3X3(YpCbCrTransform) * (YpCbCr + YpCbCrTransform[3].xyz), 0.0, 1.0);
#else
	return saturate(mul((FLOAT3X3)YpCbCrTransform, YpCbCr + YpCbCrTransform[3].xyz));
#endif
}

#if defined(SHADERLAB_GLSL)
	#if __VERSION__ < 300
		#define TEX_EXTERNAL(sampler, uv) texture2D(sampler, uv.xy);
	#else
		#define TEX_EXTERNAL(sampler, uv) texture(sampler, uv.xy)
	#endif
#endif

INLINE HALF4 SampleRGBA(sampler2D tex, FLOAT2 uv)
{
#if defined(SHADERLAB_GLSL)		// GLSL doesn't support tex2D, and Adreno GPU doesn't support passing sampler as a parameter, so just return if this is called
	return HALF4(1.0, 1.0, 0.0, 1.0);
#else
	HALF4 rgba = tex2D(tex, uv);
#if defined(APPLY_GAMMA)
	rgba.rgb = GammaToLinear(rgba.rgb);
#endif
	return rgba;
#endif
}

INLINE HALF4 SampleYpCbCr(sampler2D luma, sampler2D chroma, FLOAT2 uv, FLOAT4X4 YpCbCrTransform)
{
#if defined(SHADERLAB_GLSL)		// GLSL doesn't support tex2D, and Adreno GPU doesn't support passing sampler as a parameter, so just return if this is called
	return HALF4(1.0, 1.0, 0.0, 1.0);
#else
#if defined(SHADER_API_METAL) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
	FLOAT3 YpCbCr = FLOAT3(tex2D(luma, uv).r, tex2D(chroma, uv).rg);
#else
	FLOAT3 YpCbCr = FLOAT3(tex2D(luma, uv).r, tex2D(chroma, uv).ra);
#endif
	HALF4 rgba = HALF4(ConvertYpCbCrToRGB(YpCbCr, YpCbCrTransform), 1.0);
#if defined(APPLY_GAMMA)
	rgba.rgb = GammaToLinear(rgba.rgb);
#endif
	return rgba;
#endif
}

INLINE HALF SamplePackedAlpha(sampler2D tex, FLOAT2 uv)
{
#if defined(SHADERLAB_GLSL)		// GLSL doesn't support tex2D, and Adreno GPU doesn't support passing sampler as a parameter, so just return if this is called
	return 0.0;
#else
	HALF alpha;
#if defined(USE_YPCBCR)
	alpha = (tex2D(tex, uv).r - 0.0625) * (255.0 / 219.0);
#else
	HALF3 rgb = tex2D(tex, uv).rgb;
#if defined(APPLY_GAMMA)
	rgb = GammaToLinear(rgb);
#endif
	alpha = (rgb.r + rgb.g + rgb.b) / 3.0;
#endif
	return alpha;
#endif
}

#if defined(USE_HSBC)
INLINE HALF3 ApplyHue(HALF3 color, HALF hue)
{
	HALF angle = radians(hue);
	HALF3 k = HALF3(0.57735, 0.57735, 0.57735);
	HALF cosAngle = cos(angle);
	//Rodrigues' rotation formula
	return color * cosAngle + cross(k, color) * sin(angle) + k * dot(k, color) * (1.0 - cosAngle);
}

INLINE HALF3 ApplyHSBEffect(HALF3 color, FIXED4 hsbc)
{
	HALF hue = hsbc.r * 360.0;
	HALF saturation = hsbc.g * 2.0;
	HALF brightness = hsbc.b * 2.0 - 1.0;
	HALF contrast = hsbc.a * 2.0;

	HALF3 result = color;
	result.rgb = ApplyHue(result, hue);
	result.rgb = (result - 0.5) * contrast + 0.5 + brightness;

	#if defined(SHADERLAB_GLSL)
	result.rgb = LERP(vec3(Luminance(result)), result, saturation);
	#else
	result.rgb = LERP(Luminance(result), result, saturation);
	#endif
	
	return result;
}
#endif