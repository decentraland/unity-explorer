//Stylized Grass Shader
//Staggart Creations (http://staggart.xyz)
//Copyright protected under Unity Asset Store EULA

//Properties
TEXTURE2D(_WindMap); SAMPLER(sampler_WindMap);
float4 _GlobalWindParams;
//X: Delta time between frames
//Y: Wind Zone: Main
//Z: Wind Zone: Turbulence
//W: (int bool) Wind zone present
float3 _GlobalWindDirection;
float3 _GlobalWindOffset;

#define WIND_ZONE_MAIN _GlobalWindParams.y
#define WIND_ZONE_TURBULENCE _GlobalWindParams.z
#define WIND_ZONE_DIRECTION _GlobalWindDirection

struct WindSettings
{
	float mask;
	float ambientStrength;
	float ambientSpeed;
	float time;
	float3 direction;
	float swinging;

	float randObject;
	float randVertex;
	float randObjectStrength;

	float3 gustOffset;
	float gustSpeed;

	float gustStrength;
	float gustFrequency;
};

WindSettings PopulateWindSettings(in float strength, float speed, float4 direction, float swinging, float mask, float randObject, float randVertex, float randObjectStrength, float gustStrength, float gustFrequency, float gustSpeed)
{
	WindSettings windSettings = (WindSettings)0;

	//Apply WindZone
	if (_GlobalWindParams.w > 0.5) 
	{
		strength *= WIND_ZONE_MAIN;
		gustStrength *= WIND_ZONE_TURBULENCE;
		direction.xyz = WIND_ZONE_DIRECTION.xyz;
		
		windSettings.time = _GlobalWindParams.x;
		windSettings.gustOffset = _GlobalWindOffset.xyz;
		windSettings.gustSpeed = gustSpeed;
	}
	else
	{
		windSettings.time = _TimeParameters.x;
		windSettings.gustOffset = direction.xyz;
		windSettings.gustSpeed = gustSpeed * windSettings.time;
	}
	
	windSettings.ambientStrength = strength;
	windSettings.ambientSpeed = speed;
	windSettings.direction.xyz = direction.xyz;
	windSettings.swinging = swinging;
	windSettings.mask = mask;
	windSettings.randObject = randObject;
	windSettings.randVertex = randVertex;
	windSettings.randObjectStrength = randObjectStrength;
	windSettings.gustStrength = gustStrength;
	windSettings.gustFrequency = gustFrequency * 0.01;

	return windSettings;
}

//World-align UV moving in wind direction
float2 GetGustingUV(float3 positionWS, WindSettings windSettings)
{
	return (positionWS.xz - windSettings.gustOffset.xz * windSettings.gustSpeed.xx) * windSettings.gustFrequency;
}

#if defined(SHADER_STAGE_VERTEX) || defined(SHADER_STAGE_DOMAIN)
#define SAMPLE_GUST_MAP(texName, sampler, uv) SAMPLE_TEXTURE2D_LOD(texName, sampler, uv, 0)
#else
#define SAMPLE_GUST_MAP(texName, sampler, uv) SAMPLE_TEXTURE2D(texName, sampler, uv)
#endif

float SampleGustMap(float3 positionWS, WindSettings windSettings)
{
	float2 gustUV = GetGustingUV(positionWS, windSettings);

	float gust = SAMPLE_GUST_MAP(_WindMap, sampler_WindMap, gustUV).r;

	gust *= windSettings.gustStrength;

	return gust;
}

float4 GetWindOffset(in float3 positionOS, in float3 positionWS, float rand, WindSettings windSettings)
{
	float4 offset = 0;

#if !defined(DISABLE_WIND)
	//Random offset per vertex
	float f = length(positionOS.xz) * windSettings.randVertex;
	float strength = windSettings.ambientStrength * 0.5 * lerp(1, rand, windSettings.randObjectStrength);
	
	//Combine
	float2 sine = sin(windSettings.ambientSpeed * (windSettings.time + (rand * windSettings.randObject) + f));
	//Remap from -1/1 to 0/1
	sine = lerp(sine * 0.5 + 0.5, sine, windSettings.swinging);

	//Apply gusting
	float2 gust = SampleGustMap(positionWS, windSettings).xx;

	//Scale sine
	sine = sine * strength;

	//Mask by direction vector + gusting push
	offset.xz = windSettings.direction.xz * (sine + gust) * windSettings.mask;
	offset.y = windSettings.mask;

	//Summed offset strength
	float windWeight = length(offset.xz) + 0.0001;
	//Slightly negate the triangle-shape curve
	windWeight = pow(windWeight, 1.5);
	offset.y *= windWeight;

	//Wind strength in alpha
	offset.a = windWeight;
#endif

	return offset;
}

void GetWindOffset_float(in float3 positionOS, in float3 positionWS, float rand, in float strength, float speed, float3 direction, float swinging, float mask, float randObject, float randVertex, float randObjectStrength, float gustStrength, float gustFrequency, float gustSpeed, out float3 offset)
{
	WindSettings windSettings = PopulateWindSettings(strength, speed, direction.xyzz, swinging, mask, randObject, randVertex, randObjectStrength, gustStrength, gustFrequency, gustSpeed);

	offset = GetWindOffset(positionOS, positionWS, rand, windSettings).xyz;

	//Negate component so the entire vector can just be additively applied
	offset.y = -offset.y;
}