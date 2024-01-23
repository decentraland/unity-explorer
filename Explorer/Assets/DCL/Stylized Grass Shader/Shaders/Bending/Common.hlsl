struct Attributes
{
	float3 positionOS : POSITION;
	#if _TRAIL
	float2 uv : TEXCOORD0;
	#endif
	float3 normalOS : NORMAL;
	float4 tangentOS : TANGENT;
	float4 color : COLOR;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
	float4 positionCS : SV_POSITION;
	float3 positionWS : TEXCOORD0;
	float3 normalWS : TEXCOORD1;
	float4 color : TEXCOORD2;
	#if _TRAIL
	float2 uv : TEXCOORD3;
	float4 tangentWS : TEXCOORD4;
	float3 bitangentWS : TEXCOORD5;
	#endif
	UNITY_VERTEX_INPUT_INSTANCE_ID
	UNITY_VERTEX_OUTPUT_STEREO
};

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	UNITY_DEFINE_INSTANCED_PROP(float4, _Params)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

#define Strength UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Params).x
#define HeightOffset UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Params).y
#define PushStrength UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Params).z
#define Scale UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Params).w

float CreateDirMask(float2 uv) {
	float center = pow((uv.y * (1 - uv.y)) * 4, 4);

	return saturate(center);
}

//Creates a tube mask from the trail UV.y. Red vertex color represents lifetime strength
float CreateTrailMask(float2 uv, float lifetime)
{
	float center = saturate((uv.y * (1.0 - uv.y)) * 8.0);

	//Mask out the start of the trail, avoids grass instantly bending (assumes UV mode is set to "Stretch")
	float tip = saturate(uv.x * 16.0);

	return center * lifetime * tip;
}

Varyings vert(Attributes input)
{
	Varyings output = (Varyings)0;

	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

	#if !_TRAIL
	input.positionOS *= Scale;
	#endif
	
	output.color = input.color;
	output.positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS = TransformWorldToHClip(output.positionWS);
	output.normalWS = TransformObjectToWorldNormal(input.normalOS);

	#if _TRAIL
	output.uv.xy = input.uv.xy;
	output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
	output.bitangentWS = -TransformWorldToViewDir(cross(output.normalWS, output.tangentWS.xyz) * input.tangentOS.w);
	#endif
	
	return output;
}

half4 frag(Varyings input) : SV_Target
{
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

	float4 color = 0;
	
	float height = ((input.positionWS.y) + HeightOffset);
	
	#if _TRAIL
	float mask = CreateTrailMask(input.uv.xy, input.color.r) * Strength;

	float2 sideDir = lerp(input.bitangentWS.xy, -input.bitangentWS.xy, input.uv.y);
	float2 forwardDir = -input.tangentWS.xz;

	//Bounce back
	//sideDir = lerp(sideDir, -sideDir, sin(input.uv.x * 16));

	float dirMask = CreateDirMask(input.uv.xy);
	float2 sumDir = lerp(sideDir, forwardDir, dirMask);

	//Remap from -1.1 to 0.1
	sumDir = (sumDir * PushStrength) * 0.5 + 0.5;

	color = float4(sumDir.x, height, sumDir.y, mask);
	#else
	//Bottom-facing normals
	float mask = dot(-1, input.normalWS.y) * Strength * input.color.r;

	float2 dir = (input.normalWS.xz * PushStrength) * 0.5 + 0.5;
		
	color = float4(dir.x, height, dir.y, mask);
	#endif

	//return float4(dot(input.normalWS, float3(0,-1,0)).xxx, 1.0);

	return color;
}