//Stylized Grass Shader
//Staggart Creations (http://staggart.xyz)
//Copyright protected under Unity Asset Store EULA

#ifndef GRASS_COMMON_INCLUDED
#define GRASS_COMMON_INCLUDED

#ifndef UNITY_CORE_SAMPLERS_INCLUDED
SamplerState sampler_PointRepeat;
#endif

float4 _ColorMapUV;
float4 _ColorMapParams;
//X: Color map available
//Y: Color map has scale data
TEXTURE2D(_ColorMap); SAMPLER(sampler_ColorMap);
float4 _ColorMap_TexelSize;

float4 _PlayerSphere;
//XYZ: Position
//W: Radius

#if !defined(SHADERPASS_SHADOWCASTER ) || !defined(SHADERPASS_DEPTHONLY)
#define LIGHTING_PASS
#else
//Never any normal maps in depth/shadow passes
#undef _NORMALMAP
#endif

#if UNITY_VERSION >= 202120
#else
#define staticLightmapUV lightmapUV
#endif

//Attributes shared per pass, varyings declared separately per pass
struct Attributes
{
	float4 positionOS   : POSITION;
	float4 color		: COLOR0;
#ifdef LIGHTING_PASS
	float3 normalOS     : NORMAL;
#endif 
#if defined(_NORMALMAP) || defined(CURVEDWORLD_NORMAL_TRANSFORMATION_ON)
	float4 tangentOS    : TANGENT;
	float4 uv           : TEXCOORD0;
	//XY: Basemap UV
	//ZW: Bumpmap UV
#else
	float2 uv           : TEXCOORD0;
#endif
	
	float2 staticLightmapUV   : TEXCOORD1;
	float2 dynamicLightmapUV  : TEXCOORD2;

	UNITY_VERTEX_INPUT_INSTANCE_ID
};

#include "Bending.hlsl"
#include "Wind.hlsl"

//---------------------------------------------------------------//

float ObjectPosRand01()
{
	#if defined(UNITY_DOTS_INSTANCING_ENABLED)
	return saturate(frac(UNITY_MATRIX_M[0][2] + UNITY_MATRIX_M[1][2] + UNITY_MATRIX_M[2][2]));
	#else
	return frac(UNITY_MATRIX_M[0][3] + UNITY_MATRIX_M[1][3] + UNITY_MATRIX_M[2][3]);
	#endif
}

float3 GetPivotPos() {
	return float3(UNITY_MATRIX_M[0][3], UNITY_MATRIX_M[1][3] + 0.25, UNITY_MATRIX_M[2][3]);
}

float DistanceFadeFactor(float3 wPos, float4 near, float4 far)
{
	float pixelDist = length(GetCameraPositionWS().xyz - wPos.xyz);

	//Distance based scalar
	float nearFactor = saturate((pixelDist - near.x) / near.y);
	float farFactor = saturate((pixelDist - far.x) / far.y);

	return 1-saturate(nearFactor - farFactor);
}

float PlayerFadeFactor(float3 wPos)
{
	if(_PlayerSphere.w > 0)
	{
		const float pixelDist = length(_PlayerSphere.xyz - wPos.xyz);

		const float nearFactor = saturate((pixelDist - (_PlayerSphere.w * 0.5)) / _PlayerSphere.w);

		return 1-nearFactor;
	}
	else
	{
		return 0;
	}
}

float3 DeriveNormal(float3 positionWS)
{
	float3 dpx = ddx(positionWS);
	float3 dpy = ddy(positionWS);
	return normalize(cross(dpx, dpy));
}

float AngleFadeFactor(float3 positionWS, float angleThreshold)
{
	float viewAngle = (dot(DeriveNormal(positionWS), -normalize(GetCameraPositionWS() - positionWS))) * 90;

	float factor = smoothstep(0.25, 1, saturate(viewAngle / (angleThreshold)));
	return factor;
}

void ApplyLODCrossfade(float2 clipPos)
{
#if LOD_FADE_CROSSFADE

	//Unity 2022.2+
	#ifdef UNIVERSAL_PIPELINE_LODCROSSFADE_INCLUDED
	LODFadeCrossFade(clipPos.xyxy);
	#else
	float hash = GenerateHashedRandomFloat(clipPos.xy * 4.0);

	float sign = CopySign(hash, unity_LODFade.x);
	
	float f = unity_LODFade.x - sign;

	clip(f);
	#endif
#endif
}

TEXTURE2D(_DitheringNoise);
float4 _DitheringScaleOffset;

float Dithering(float2 coords, float t)
{
	//return t * (InterleavedGradientNoise(coords, 0) + t);

	#if defined(SHADERPASS_SHADOWCASTER)
	//_Dithering_Offset.xy *= 0;
	#endif
	
	half2 uv = (coords.xy * _DitheringScaleOffset.xy) + _DitheringScaleOffset.zw;

	half d = SAMPLE_TEXTURE2D(_DitheringNoise, sampler_PointRepeat, uv).a;

	return smoothstep(0, d, t);
}

float ComposeAlpha(float alpha, float cutoff, float3 clipPos, float3 wPos, float4 fadeParamsNear, float4 fadeParamsFar, float angleThreshold)
{
	float f = 1.0;

	#if _FADING
	f -= DistanceFadeFactor(wPos, fadeParamsNear, fadeParamsFar);
	f -= PlayerFadeFactor(wPos);

	//Don't perform for cast shadows. Otherwise fading is calculated based on the light direction relative to the surface, not the camera
	#if !defined(SHADERPASS_SHADOWCASTER)
	if(angleThreshold > -1)
	{
		float NdotV = AngleFadeFactor(wPos, angleThreshold);

		f *= NdotV;
	}
	#endif
	
	float dither = Dithering(clipPos.xy, f);
	f = dither;

	alpha = min((alpha - cutoff), (dither - 0.5));
	#else
	alpha -= cutoff;
	#endif

	return alpha;
}

void AlphaClip(float alpha, float3 clipPos, float3 wPos)
{
	#ifdef _ALPHATEST_ON
	clip(alpha);
	#endif

	#if defined(SHADERPASS_SHADOWCASTER)
	//Using clip-space position causes pixel swimming as the camera moves
	ApplyLODCrossfade(wPos.xz * 32);
	#else
	ApplyLODCrossfade(clipPos.xy);
	#endif
}

//UV Utilities
float2 BoundsToWorldUV(in float3 wPos, in float4 b)
{
	return (wPos.xz * b.z) - (b.xy * b.z);
}

//Color map UV
float2 GetColorMapUV(in float3 wPos)
{
	return BoundsToWorldUV(wPos, _ColorMapUV);
}

float4 SampleColorMapTextureLOD(in float3 wPos)
{
	float2 uv = GetColorMapUV(wPos);

	return SAMPLE_TEXTURE2D_LOD(_ColorMap, sampler_ColorMap, uv, 0).rgba;
}

//---------------------------------------------------------------//
//Vertex transformation

struct VertexInputs
{
	float4 positionOS;
	float3 normalOS;
#if defined(_NORMALMAP) || defined(CURVEDWORLD_NORMAL_TRANSFORMATION_ON)
	float4 tangentOS;
#endif
};

VertexInputs GetVertexInputs(Attributes v, float flattenNormals)
{
	VertexInputs i = (VertexInputs)0;
	i.positionOS = v.positionOS;
	i.normalOS = lerp(v.normalOS, float3(0,1,0), flattenNormals);
#if defined(_NORMALMAP) || defined(CURVEDWORLD_NORMAL_TRANSFORMATION_ON)
	i.tangentOS = v.tangentOS;
#endif

	return i;
}

//Struct that holds both VertexPositionInputs and VertexNormalInputs
struct VertexOutput {
	//Positions
	float3 positionWS; // World space position
	float3 positionVS; // View space position
	float4 positionCS; // Homogeneous clip space position
	float4 positionNDC;// Homogeneous normalized device coordinates
	float3 viewDir;// Homogeneous normalized device coordinates

	//Normals
#if defined(_NORMALMAP) || defined(CURVEDWORLD_NORMAL_TRANSFORMATION_ON)
	real4 tangentWS;
#endif
	float3 normalWS;
};

//Physically correct, but doesn't look great
//#define RECALC_NORMALS

//Combination of GetVertexPositionInputs and GetVertexNormalInputs with bending
VertexOutput GetVertexOutput(VertexInputs input, float rand, WindSettings s, BendSettings b)
{
	VertexOutput data = (VertexOutput)0;

#if defined(CURVEDWORLD_IS_INSTALLED) && !defined(CURVEDWORLD_DISABLED_ON) && !defined(DEFAULT_VERTEX)
#if defined(CURVEDWORLD_NORMAL_TRANSFORMATION_ON) && defined(LIGHTING_PASS)
	CURVEDWORLD_TRANSFORM_VERTEX_AND_NORMAL(input.positionOS, input.normalOS, input.tangentOS)
#else
	CURVEDWORLD_TRANSFORM_VERTEX(input.positionOS)
#endif
#endif

#if _BILLBOARD	
	//Local vector towards camera
	float3 camDir = normalize(input.positionOS.xyz - TransformWorldToObject(_WorldSpaceCameraPos.xyz));
	camDir.y = 0; //Cylindrical billboarding
	
	float3 forward = camDir;
	float3 right = normalize(cross(float3(0,1,0), forward));
	float3 up = cross(forward, right);

	float4x4 lookatMatrix = {
		right.x,            up.x,            forward.x,       0,
        right.y,            up.y,            forward.y,       0,
        right.z,            up.z,            forward.z,       0,
        0, 0, 0,  1
    };
	
	input.normalOS = normalize(mul(float4(input.normalOS , 0.0), lookatMatrix)).xyz;
	input.positionOS.xyz = mul((float4x4)lookatMatrix, input.positionOS.xyzw).xyz;	
#endif
	
	float3 wPos = TransformObjectToWorld(input.positionOS.xyz);

	float scaleMap = 1.0;
#if _SCALEMAP
	if(_ColorMapParams.y > 0)
	{
		scaleMap = SampleColorMapTextureLOD(wPos).a;

		//Scale axes in object-space
		input.positionOS.x = lerp(input.positionOS.x, input.positionOS.x * scaleMap, _ScalemapInfluence.x);
		input.positionOS.y = lerp(input.positionOS.y, input.positionOS.y * scaleMap, _ScalemapInfluence.y);
		input.positionOS.z = lerp(input.positionOS.z, input.positionOS.z * scaleMap, _ScalemapInfluence.z);
		wPos = TransformObjectToWorld(input.positionOS.xyz);
	}
#else
#endif

	float3 worldPos = lerp(wPos, GetPivotPos(), b.mode);
	float4 windVec = GetWindOffset(input.positionOS.xyz, wPos, rand, s) * scaleMap; //Less wind on shorter grass
	float4 bendVec = GetBendOffset(worldPos, b);

	float3 offsets = lerp(windVec.xyz, bendVec.xyz, bendVec.a);

	//Perspective correction
	data.viewDir = normalize(GetCameraPositionWS().xyz - wPos);

	#if !_BILLBOARD	
	float3 perspUp = float3(0,1,0);

	#if _ADVANCED_LIGHTING
	//Upward normal of object, taking into account its rotation
	//perspUp = TransformWorldToObjectDir(perspUp);
	#endif
	
	ApplyPerspectiveCorrection(offsets, wPos, perspUp, data.viewDir, b.mask, b.perspectiveCorrection);
	#endif
	
	//Apply bend offset
	wPos.xz += offsets.xz;
	wPos.y -= offsets.y;

	#ifdef MASKING_SPHERE_DISPLACEMENT
	//Displace away from GrassMaskingSphere component
	if(_PlayerSphere.w > 0)
	{
		float3 delta = wPos.xyz - _PlayerSphere.xyz;
		float3 pushDir = normalize(delta);
		if(length(delta) < _PlayerSphere.w)
		{
			wPos = _PlayerSphere.xyz + (pushDir * _PlayerSphere.w);
		}
	} 
	#endif

	//Vertex positions in various coordinate spaces
	data.positionWS = wPos;
	data.positionVS = TransformWorldToView(data.positionWS);
	data.positionCS = TransformWorldToHClip(data.positionWS);                       
	
	float4 ndc = data.positionCS * 0.5f;
	data.positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
	data.positionNDC.zw = data.positionCS.zw;

#if !defined(SHADERPASS_SHADOWCASTER) && !defined(SHADERPASS_DEPTHONLY) //Skip normal derivative during shadow and depth passes

#if _ADVANCED_LIGHTING && defined(RECALC_NORMALS)
	float3 oPos = TransformWorldToObject(wPos); //object-space position after displacement in world-space
	float3 bentNormals = lerp(input.normalOS, normalize(oPos - input.positionOS.xyz), abs(offsets.x + offsets.z) * 0.5); //weight is length of wind/bend vector
#else
	float3 bentNormals = input.normalOS;
#endif

	data.normalWS = TransformObjectToWorldNormal(bentNormals);
#ifdef _NORMALMAP
	data.tangentWS.xyz = TransformObjectToWorldDir(input.tangentOS.xyz);
	real sign = input.tangentOS.w * GetOddNegativeScale();
	data.tangentWS.w = sign;
#endif
#endif

	return data;
}
#endif