//Stylized Grass Shader
//Staggart Creations (http://staggart.xyz)
//Copyright protected under Unity Asset Store EULA

//Global parameters
float4 _GrassBendCoords;
//XY: Bounds min corner
//Z: Bounds size (uniform)
//W: Bool if renderer is enabled
TEXTURE2D(_GrassOffsetVectors); SAMPLER(sampler_GrassOffsetVectors);
float4 _GrassOffsetVectors_TexelSize;

//Cannot rely on the unity_CameraToWorld matrix, since this represent the directional light during the shadow casting pass
//Instead, it is passed on during the execution of GrassOffsetVectorPass
float4 _CameraForwardVector;
//W: (bool) enabled

struct BendSettings
{
	uint mode;
	float mask;
	float pushStrength;
	float flattenStrength;
	float perspectiveCorrection;
};

BendSettings PopulateBendSettings(uint mode, float mask, float pushStrength, float flattenStrength, float perspCorrection)
{
	BendSettings s = (BendSettings)0;

	s.mode = mode;
	s.mask = mask;
	s.pushStrength = pushStrength;
	s.flattenStrength = flattenStrength;
	s.perspectiveCorrection = perspCorrection;

	return s;
}

//Bend map UV
float2 GetBendMapUV(in float3 wPos)
{
	#if defined(SHADERGRAPH_PREVIEW)
	return 0;
	#else
	float2 uv = _GrassBendCoords.xy / _GrassBendCoords.z + (_GrassBendCoords.z / (_GrassBendCoords.z * _GrassBendCoords.z)) * wPos.xz;

#if	UNITY_VERSION >= 202020
	uv.y = 1-uv.y;
#endif

	return uv;
	#endif
}

//https://github.com/Unity-Technologies/Graphics/blob/4641000674e63d10e2f7693e919c78b611f9de27/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl#L170
float BoundsEdgeMask(float2 position)
{
	const float blendDistance = 2;
	//Negate and center
	position = -position + _GrassBendCoords.z;
	
	const float2 boundsMin = _GrassBendCoords.xy;
	const float2 boundsMax = _GrassBendCoords.xy + _GrassBendCoords.z;
	
	float2 weightDir = min(position - boundsMin, boundsMax - position) / blendDistance;
	
	return saturate(min(weightDir.x, weightDir.y));
}

//Texture sampling
float4 GetBendVector(float3 wPos) 
{
	float2 uv = GetBendMapUV(wPos);

	float4 v = SAMPLE_TEXTURE2D_LOD(_GrassOffsetVectors, sampler_GrassOffsetVectors, uv, 0).rgba;

	v.xz = v.xz * 2.0 - 1.0;

	v.a *= BoundsEdgeMask(wPos.xz);
	
	return v;
}

#define BENDING_END_DIST 3.0

float HeightDistanceWeight(float3 obstaclePos, float3 surfacePos)
{
	const float grassHeight = obstaclePos.y;
	const float bendHeight = surfacePos.y;

	const float pixelDist = -(bendHeight - grassHeight);

	//Ensure the weight tapers off once the obstacle start to go lower than 3 units from the grass.
	const float falloff = 1-saturate((pixelDist - BENDING_END_DIST) / (grassHeight));

	return saturate((grassHeight - bendHeight) * falloff);
}

float4 GetBendOffset(float3 wPos, BendSettings b)
{
	float4 offset = 0;
	
#if !defined(DISABLE_BENDING)
	//Render feature not present
	if (_GrassBendCoords.w == 0) return 0;
	
	float4 vec = GetBendVector(wPos);

	const float weight = HeightDistanceWeight(wPos.y, vec.y);

	offset.xz = vec.xz * b.mask * weight * b.pushStrength;
	offset.y = b.mask * (vec.a * 0.75) * weight * b.flattenStrength;
	
	//Pass the mask, so it can be used to lerp between wind and bend offset vectors
	offset.a = vec.a * weight;

	//Apply mask
	offset.xyz *= offset.a;
#endif
	
	return offset;
}

void ApplyPerspectiveCorrection(inout float3 offset, float3 wPos, float3 up, float3 viewDir, float mask, float strength)
{
	float dist = 1;
	
	if(_CameraForwardVector.w > 0)
	{
		viewDir = _CameraForwardVector.xyz;
	}
	else
	{
		//Avoid pushing grass straight underneath the camera in a falloff of 4 units (1.0/4.0)
		dist = saturate(distance(wPos.xz, GetCameraPositionWS().xz) * 0.25);
	}
	
	float NdotV = dot(up, viewDir);

	const float perspMask = mask * strength * dist * NdotV;
	
	offset.xz += -viewDir.xz * perspMask;
}


//Shader Graph and Amplify Shader Editor

void GetBendOffset_float(float3 wPos, float mask, float pushStrength, float flattenStrength, out float4 offset)
{
	//Note: Mode and PerspCorrection parameters aren't used for just the grass bending
	BendSettings b = PopulateBendSettings(0, mask, pushStrength, flattenStrength, 0);

	offset = GetBendOffset(wPos, b);

	//Negate component so the entire vector can just be additively applied
	offset.y = -offset.y;

	//SG and ASE work in Object-space offsets.
	offset.xyz = TransformWorldToObjectDir(offset.xyz, false);
}

//Backwards compatibility with 1.2.2
float CreateTrailMask(float2 uv, float mask)
{
	return 0;
}

float CreateDirMask(float2 uv)
{
	return 0;
}