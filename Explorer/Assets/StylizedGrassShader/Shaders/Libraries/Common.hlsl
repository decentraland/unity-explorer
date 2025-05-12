//Stylized Grass Shader
//Staggart Creations (http://staggart.xyz)
//Copyright protected under Unity Asset Store EULA

#ifndef GRASS_COMMON_INCLUDED
#define GRASS_COMMON_INCLUDED

#include "Dither.hlsl"

#ifdef _GPU_INSTANCER_BATCHER
    struct PerInstanceBuffer
    {
        float4x4 instMatrix;
        float4 instColourTint;
    };
    StructuredBuffer<PerInstanceBuffer> _PerInstanceBuffer;

	struct PerInstanceLookUpAndDither
	{
		uint instanceID;
		uint ditherLevel;
		uint padding0;
    	uint padding1;
	};
	StructuredBuffer<PerInstanceLookUpAndDither> _PerInstanceLookUpAndDitherBuffer;
#endif

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

    float4 tintColour;
    uint nDither;
};

#ifdef _GPU_INSTANCER_BATCHER
float4x4 inverse(float4x4 m) {
    float n11 = m[0][0], n12 = m[1][0], n13 = m[2][0], n14 = m[3][0];
    float n21 = m[0][1], n22 = m[1][1], n23 = m[2][1], n24 = m[3][1];
    float n31 = m[0][2], n32 = m[1][2], n33 = m[2][2], n34 = m[3][2];
    float n41 = m[0][3], n42 = m[1][3], n43 = m[2][3], n44 = m[3][3];

    float t11 = n23 * n34 * n42 - n24 * n33 * n42 + n24 * n32 * n43 - n22 * n34 * n43 - n23 * n32 * n44 + n22 * n33 * n44;
    float t12 = n14 * n33 * n42 - n13 * n34 * n42 - n14 * n32 * n43 + n12 * n34 * n43 + n13 * n32 * n44 - n12 * n33 * n44;
    float t13 = n13 * n24 * n42 - n14 * n23 * n42 + n14 * n22 * n43 - n12 * n24 * n43 - n13 * n22 * n44 + n12 * n23 * n44;
    float t14 = n14 * n23 * n32 - n13 * n24 * n32 - n14 * n22 * n33 + n12 * n24 * n33 + n13 * n22 * n34 - n12 * n23 * n34;

    float det = n11 * t11 + n21 * t12 + n31 * t13 + n41 * t14;
    float idet = 1.0f / det;

    float4x4 ret;

    ret[0][0] = t11 * idet;
    ret[0][1] = (n24 * n33 * n41 - n23 * n34 * n41 - n24 * n31 * n43 + n21 * n34 * n43 + n23 * n31 * n44 - n21 * n33 * n44) * idet;
    ret[0][2] = (n22 * n34 * n41 - n24 * n32 * n41 + n24 * n31 * n42 - n21 * n34 * n42 - n22 * n31 * n44 + n21 * n32 * n44) * idet;
    ret[0][3] = (n23 * n32 * n41 - n22 * n33 * n41 - n23 * n31 * n42 + n21 * n33 * n42 + n22 * n31 * n43 - n21 * n32 * n43) * idet;

    ret[1][0] = t12 * idet;
    ret[1][1] = (n13 * n34 * n41 - n14 * n33 * n41 + n14 * n31 * n43 - n11 * n34 * n43 - n13 * n31 * n44 + n11 * n33 * n44) * idet;
    ret[1][2] = (n14 * n32 * n41 - n12 * n34 * n41 - n14 * n31 * n42 + n11 * n34 * n42 + n12 * n31 * n44 - n11 * n32 * n44) * idet;
    ret[1][3] = (n12 * n33 * n41 - n13 * n32 * n41 + n13 * n31 * n42 - n11 * n33 * n42 - n12 * n31 * n43 + n11 * n32 * n43) * idet;

    ret[2][0] = t13 * idet;
    ret[2][1] = (n14 * n23 * n41 - n13 * n24 * n41 - n14 * n21 * n43 + n11 * n24 * n43 + n13 * n21 * n44 - n11 * n23 * n44) * idet;
    ret[2][2] = (n12 * n24 * n41 - n14 * n22 * n41 + n14 * n21 * n42 - n11 * n24 * n42 - n12 * n21 * n44 + n11 * n22 * n44) * idet;
    ret[2][3] = (n13 * n22 * n41 - n12 * n23 * n41 - n13 * n21 * n42 + n11 * n23 * n42 + n12 * n21 * n43 - n11 * n22 * n43) * idet;

    ret[3][0] = t14 * idet;
    ret[3][1] = (n13 * n24 * n31 - n14 * n23 * n31 + n14 * n21 * n33 - n11 * n24 * n33 - n13 * n21 * n34 + n11 * n23 * n34) * idet;
    ret[3][2] = (n14 * n22 * n31 - n12 * n24 * n31 - n14 * n21 * n32 + n11 * n24 * n32 + n12 * n21 * n34 - n11 * n22 * n34) * idet;
    ret[3][3] = (n12 * n23 * n31 - n13 * n22 * n31 + n13 * n21 * n32 - n11 * n23 * n32 - n12 * n21 * n33 + n11 * n22 * n33) * idet;

    return ret;
}

float3 TransformObjectToWorld_PerInstance(float3 positionOS, uint _instanceID)
{
    #if defined(SHADER_STAGE_RAY_TRACING)
    return mul(ObjectToWorld3x4(), float4(positionOS, 1.0)).xyz;
    #else
	uint instID = _PerInstanceLookUpAndDitherBuffer[_instanceID].instanceID;
    return mul(_PerInstanceBuffer[instID].instMatrix, float4(positionOS, 1.0)).xyz;
    #endif
}

float3 TransformWorldToObject_PerInstance(float3 positionWS, uint _instanceID)
{
	uint instID = _PerInstanceLookUpAndDitherBuffer[_instanceID].instanceID;
	return mul(inverse(_PerInstanceBuffer[instID].instMatrix), float4(positionWS, 1.0)).xyz;
}

float3 TransformObjectToWorldDir_PerInstance(float3 dirOS, uint _instanceID, bool doNormalize = true)
{
    #ifdef _GPU_INSTANCER_BATCHER
		uint instID = _PerInstanceLookUpAndDitherBuffer[_instanceID].instanceID;
        float4x4 ObjToWorldMatrix = _PerInstanceBuffer[instID].instMatrix;
        float3 dirWS = mul((float3x3)ObjToWorldMatrix, dirOS);
    #else
        #ifndef SHADER_STAGE_RAY_TRACING
            float3 dirWS = mul((float3x3)GetObjectToWorldMatrix(), dirOS);
        #else
            float3 dirWS = mul((float3x3)ObjectToWorld3x4(), dirOS);
        #endif
    #endif
    
    if (doNormalize)
        return SafeNormalize(dirWS);
    return dirWS;
}

float3 TransformObjectToWorldNormal_PerInstance(float3 normalOS, uint _instanceID, bool doNormalize = true)
{
    #ifdef UNITY_ASSUME_UNIFORM_SCALING
        return TransformObjectToWorldDir_PerInstance(normalOS, _instanceID, doNormalize);
    #else
        // Normal need to be multiply by inverse transpose
        #ifdef _GPU_INSTANCER_BATCHER
			uint instID = _PerInstanceLookUpAndDitherBuffer[_instanceID].instanceID;
            float4x4 ObjToWorldMatrix = _PerInstanceBuffer[instID].instMatrix;
            float3 normalWS = mul(normalOS, (float3x3)inverse(ObjToWorldMatrix));
        #else
            float3 normalWS = mul(normalOS, (float3x3)GetWorldToObjectMatrix());
        #endif
        if (doNormalize)
            return SafeNormalize(normalWS);

        return normalWS;
    #endif
}

#endif

//Physically correct, but doesn't look great
//#define RECALC_NORMALS

//Combination of GetVertexPositionInputs and GetVertexNormalInputs with bending
VertexOutput GetVertexOutput(uint _svInstanceID, VertexInputs input, float rand, WindSettings s, BendSettings b)
{
	VertexOutput data = (VertexOutput)0;
	#ifdef _GPU_INSTANCER_BATCHER
		uint instanceID = GetIndirectInstanceID_Base(_svInstanceID);
    	data.nDither = _PerInstanceLookUpAndDitherBuffer[instanceID].ditherLevel;
		data.tintColour = _PerInstanceBuffer[_PerInstanceLookUpAndDitherBuffer[instanceID].instanceID].instColourTint;
	#else
		data.nDither = 0;
		data.tintColour = float4(1.0f, 1.0f, 1.0f, 1.0f);
	#endif

	#if defined(CURVEDWORLD_IS_INSTALLED) && !defined(CURVEDWORLD_DISABLED_ON) && !defined(DEFAULT_VERTEX)
		#if defined(CURVEDWORLD_NORMAL_TRANSFORMATION_ON) && defined(LIGHTING_PASS)
			CURVEDWORLD_TRANSFORM_VERTEX_AND_NORMAL(input.positionOS, input.normalOS, input.tangentOS)
		#else
			CURVEDWORLD_TRANSFORM_VERTEX(input.positionOS)
		#endif
	#endif

	#if _BILLBOARD	
		//Local vector towards camera
		#ifdef _GPU_INSTANCER_BATCHER
			float3 camDir = normalize(input.positionOS.xyz - TransformWorldToObject_PerInstance(_WorldSpaceCameraPos.xyz, instanceID));
		#else
			float3 camDir = normalize(input.positionOS.xyz - TransformWorldToObject(_WorldSpaceCameraPos.xyz));
		#endif
		camDir.y = lerp(0, camDir.y, b.billboardingVerticalRotation); //Cylindrical billboarding if 0
		
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
	
	#ifdef _GPU_INSTANCER_BATCHER
		float3 wPos = TransformObjectToWorld_PerInstance(input.positionOS.xyz, instanceID);
	#else
		float3 wPos = TransformObjectToWorld(input.positionOS.xyz);
	#endif

	float scaleMap = 1.0;
	#if _SCALEMAP
		if(_ColorMapParams.y > 0)
		{
			scaleMap = SampleColorMapTextureLOD(wPos).a;

			//Scale axes in object-space
			input.positionOS.x = lerp(input.positionOS.x, input.positionOS.x * scaleMap, _ScalemapInfluence.x);
			input.positionOS.y = lerp(input.positionOS.y, input.positionOS.y * scaleMap, _ScalemapInfluence.y);
			input.positionOS.z = lerp(input.positionOS.z, input.positionOS.z * scaleMap, _ScalemapInfluence.z);
			#ifdef _GPU_INSTANCER_BATCHER
			wPos = TransformObjectToWorld_PerInstance(input.positionOS.xyz, instanceID);
			#else
			wPos = TransformObjectToWorld(input.positionOS.xyz);
			#endif
		}
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
			#ifdef _GPU_INSTANCER_BATCHER
				float3 oPos = TransformWorldToObject_PerInstance(wPos, instanceID); //object-space position after displacement in world-space
			#else
				float3 oPos = TransformWorldToObject(wPos); //object-space position after displacement in world-space
			#endif
			float3 bentNormals = lerp(input.normalOS, normalize(oPos - input.positionOS.xyz), abs(offsets.x + offsets.z) * 0.5); //weight is length of wind/bend vector
		#else
			float3 bentNormals = input.normalOS;
		#endif

		#ifdef _GPU_INSTANCER_BATCHER
			data.normalWS = TransformObjectToWorldNormal_PerInstance(bentNormals, instanceID);
		#else
			data.normalWS = TransformObjectToWorldNormal(bentNormals);
		#endif

		#ifdef _NORMALMAP
			#ifdef _GPU_INSTANCER_BATCHER
				data.tangentWS.xyz = TransformObjectToWorldDir_PerInstance(input.tangentOS.xyz, instanceID);
			#else
				data.tangentWS.xyz = TransformObjectToWorldDir(input.tangentOS.xyz);
			#endif
			
			real sign = input.tangentOS.w * GetOddNegativeScale();
			data.tangentWS.w = sign;
		#endif
	#endif

	return data;
}
#endif