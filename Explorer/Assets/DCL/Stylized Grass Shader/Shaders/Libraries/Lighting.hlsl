//Stylized Grass Shader
//Staggart Creations (http://staggart.xyz)
//Copyright protected under Unity Asset Store EULA

struct TranslucencyData
{
	float strengthDirect;
	float strengthIndirect;
	float exponent;
	float thickness; //Actually comes in reversed and represents "thinness"
	float offset;

	Light light;
};

float GetLightHorizonFalloff(float3 dir)
{
	//Fade the effect out as the sun approaches the horizon (75 to 90 degrees)
	half sunAngle = dot(float3(0, 1, 0), dir);
	
	return saturate(sunAngle * 6.666); /* 1.0 over 0.15 = 6.666 */
}

void ApplyTranslucency(inout SurfaceData surfaceData, InputData inputData, TranslucencyData data)
{
	float VdotL = saturate(dot(-inputData.viewDirectionWS, normalize(data.light.direction + (inputData.normalWS * data.offset))));
	VdotL = saturate(pow(VdotL, data.exponent));

	//For proper sub-surface scattering, this should be blurred to some extent. But this should ideally be incorporated into the pipeline as a whole.
	float shadowMask = data.light.shadowAttenuation * data.light.distanceAttenuation * surfaceData.occlusion;

	//Fake some subsurface scattering by incorporating the effect into occlusion as well.
	shadowMask = saturate(shadowMask + data.strengthIndirect);

	half angleMask = GetLightHorizonFalloff(data.light.direction);

	//In URP light intensity is pre-multiplied with the color, extract via magnitude of color "vector"
	float lightStrength = length(data.light.color);
	
	float3 tColor = surfaceData.albedo + BlendOverlay(data.light.color, surfaceData.albedo);
	float3 direct = tColor * data.strengthDirect;
	float3 indirect = tColor * data.strengthIndirect;

	surfaceData.emission += lerp(indirect, direct, VdotL) * lightStrength * shadowMask * angleMask * data.thickness;
}

float3 UnlitShading(SurfaceData surfaceData, InputData input)
{
	#if UNITY_VERSION >= 202030
	#if defined(_SCREEN_SPACE_OCCLUSION)
	AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(input.normalizedScreenSpaceUV);
	surfaceData.occlusion = min(surfaceData.occlusion, aoFactor.indirectAmbientOcclusion);

	surfaceData.albedo *= min(surfaceData.occlusion, aoFactor.indirectAmbientOcclusion);
	#endif
	#endif

	return surfaceData.albedo + surfaceData.emission + surfaceData.specular;
}

// General function to apply lighting based on the configured mode
half3 ApplyLighting(SurfaceData surfaceData, InputData inputData, TranslucencyData translucency)
{
	half3 color = 0;

	//Modifies emission data
	ApplyTranslucency(surfaceData, inputData, translucency);
	
#ifdef _UNLIT
	color = UnlitShading(surfaceData, inputData);
#endif

#if _SIMPLE_LIGHTING
	#if VERSION_LOWER(12,0)
	color = UniversalFragmentBlinnPhong(inputData, surfaceData.albedo, 0, surfaceData.smoothness, surfaceData.emission, surfaceData.alpha).rgb;
	#else
	color = UniversalFragmentBlinnPhong(inputData, surfaceData).rgb;
	#endif
#endif

#if _ADVANCED_LIGHTING
	#if VERSION_LOWER(10,0)
	color = UniversalFragmentPBR(inputData, surfaceData.albedo, surfaceData.metallic, surfaceData.specular, surfaceData.smoothness, surfaceData.occlusion, surfaceData.emission, surfaceData.alpha).rgb;
	#else
	color = UniversalFragmentPBR(inputData, surfaceData).rgb;
	#endif
#endif

	return color;
}