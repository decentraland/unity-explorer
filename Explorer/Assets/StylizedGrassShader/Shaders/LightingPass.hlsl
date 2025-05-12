//Stylized Grass Shader
//Staggart Creations (http://staggart.xyz)
//Copyright protected under Unity Asset Store EULA

//#define DEBUG_BEND_AREA
//#define DEBUG_BEND_VECTORS

#if UNITY_VERSION >= 202120
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"
#endif

struct Varyings
{
	float4 uv                       : TEXCOORD0;
	DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 1);
	
//#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR) //Always needed
	float3 positionWS               : TEXCOORD2;
//#endif
	
	half3  normalWS                 : TEXCOORD3;

#ifdef _NORMALMAP
	half4 tangentWS                 : TEXCOORD4;  // xyz: tangent, w: sign
#endif

	half4 fogFactorAndVertexLight   : TEXCOORD6; // x: fogFactor, yzw: vertex light

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
	float4 shadowCoord              : TEXCOORD8; // compute shadow coord per-vertex for the main light
#endif

	#ifdef DYNAMICLIGHTMAP_ON
	float2  dynamicLightmapUV : TEXCOORD9; // Dynamic lightmap UVs
	#endif
		
	float4 positionCS               : SV_POSITION;
	float4 color					: COLOR0;

    float4 tintColour               : TEXCOORD10;
    uint nDither        			: TEXCOORD11;
	
	UNITY_VERTEX_INPUT_INSTANCE_ID
	UNITY_VERTEX_OUTPUT_STEREO
};

Varyings LitPassVertex(Attributes input, uint svInstanceID : SV_InstanceID)
{
	#ifdef _GPU_INSTANCER_BATCHER
    InitIndirectDrawArgs(0);
    #endif
	
	Varyings output = (Varyings)0;
	
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

	float posOffset = ObjectPosRand01();

	WindSettings wind = PopulateWindSettings(_WindAmbientStrength, _WindSpeed, _WindDirection, _WindSwinging, input.color[_VertexColorWindChannel], _WindObjectRand, _WindVertexRand, _WindRandStrength, _WindGustStrength, _WindGustFreq, _WindGustSpeed);
	BendSettings bending = PopulateBendSettings(_BendMode, input.color[_VertexColorBendingChannel], _BendPushStrength, _BendFlattenStrength, _PerspectiveCorrection, _BillboardingVerticalRotation);

	//Object space position, normals (and tangents)
	VertexInputs vertexInputs = GetVertexInputs(input, _NormalFlattening);

	//Original vertex normals should be perpendicular to the vertex face!
	//For lighting, force the normals straight up. Later in the LitPassVertex the normals can be modified through parameters

	vertexInputs.normalOS = lerp(vertexInputs.normalOS, normalize(vertexInputs.positionOS.xyz), _NormalSpherify * lerp(1, input.color[_VertexColorShadingChannel], _NormalSpherifyMask));
	//Apply transformations and bending/wind (Can't use GetVertexPositionInputs, because it would amount to double matrix transformations)
	VertexOutput vertexData = GetVertexOutput(svInstanceID, vertexInputs, posOffset, wind, bending);
	
	#ifdef _GPU_INSTANCER_BATCHER
	output.nDither = vertexData.nDither;
	float4 baseColourTinted = _BaseColor * vertexData.tintColour;
	#else
	output.nDither = 0;
	float4 baseColourTinted = _BaseColor;
	#endif

	//#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
	output.positionWS = vertexData.positionWS;
	//#endif
	output.normalWS = vertexData.normalWS;
	
	//Vertex color
	output.color.rgb = ApplyVertexColor(input.positionOS.xyz, vertexData.positionWS.xyz, baseColourTinted.rgb, input.color[_VertexColorShadingChannel], _OcclusionStrength, _VertexDarkening, posOffset);
	//Pass vertex color alpha-channel to fragment stage. Used in some shading functions such as translucency
	output.color.a = input.color[_VertexColorShadingChannel];

	//output.color.a *= vertexInputs.positionOS.y;
	
	half fogFactor = ComputeFogFactor(vertexData.positionCS.z);

#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR) || defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
	real sign = input.tangentOS.w * GetOddNegativeScale();
	half4 tangentWS = half4(normalInput.tangentWS.xyz, sign);
#endif
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
	output.tangentWS = tangentWS;
#endif
	
#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
	half3 viewDirWS = GetWorldSpaceViewDir(vertexData.positionWS);
	half3 viewDirTS = GetViewDirectionTangentSpace(tangentWS, output.normalWS, viewDirWS);
	output.viewDirTS = viewDirTS;
#endif

	OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
	#ifdef DYNAMICLIGHTMAP_ON
	output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
	#endif

	#if UNITY_VERSION >= 600009 //Not supported, but patched to resolve error
	OUTPUT_SH4(vertexData.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexData.positionWS), output.vertexSH, 1);
	#elif UNITY_VERSION >= 202320 //Note: actually available from 2023.1.7+ (URP 15.0.8)
	OUTPUT_SH4(vertexData.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexData.positionWS), output.vertexSH);
	#else
	OUTPUT_SH(output.normalWS.xyz, output.vertexSH);
	#endif
	
#ifdef _ADDITIONAL_LIGHTS_VERTEX
	//Apply per-vertex light if enabled in pipeline
	//Pass to fragment shader to apply in Lighting function
	half3 vertexLight = VertexLighting(vertexData.positionWS, vertexData.normalWS);
	output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
#else
	output.fogFactorAndVertexLight.x = fogFactor;
	output.fogFactorAndVertexLight.yzw = 0;
#endif
	
#if _NORMALMAP || defined(CURVEDWORLD_NORMAL_TRANSFORMATION_ON)
	output.uv.zw = TRANSFORM_TEX(input.uv, _BumpMap);
	output.tangentWS = vertexData.tangentWS;
#else
	//Initialize with 0
	output.uv.zw = 0;
#endif

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
	//GetShadowCoord function must be used, in order for normalized screen coords to be calculated (Screen-space shadows)
	VertexPositionInputs vertexPositionInputs = (VertexPositionInputs)0;
	vertexPositionInputs.positionWS = vertexData.positionWS;
	vertexPositionInputs.positionCS = vertexData.positionCS; //used to compute screen pos
	output.shadowCoord = GetShadowCoord(vertexPositionInputs);
#endif

	output.uv.xy = TRANSFORM_TEX(input.uv, _BaseMap);
	output.positionCS = vertexData.positionCS;

	return output;
}

void ModifySurfaceData(Varyings input, out SurfaceData surfaceData)
{
	float4 albedoAlpha = SampleAlbedoAlpha(input.uv.xy, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));

	//If MSAA is enabled an issue occurs where any vertex interpolated values are abnormal if the geometry occupies 1px on screen
	//Clamping the value resolves these artefacts for the most part, otherwise causes fireflies
	half mask = saturate(input.color.a);

	half heightGradient = saturate(sqrt(mask));
	//Apply hue variation
	if(_HueVariation.a > 0)
	{
		float posOffset = ObjectPosRand01();
		half hueMask = (1-smoothstep(_HueVariationHeight, 0, mask)) * _HueVariation.a;
		albedoAlpha.rgb = lerp(albedoAlpha.rgb, _HueVariation.rgb, posOffset * hueMask);
	}
	
	//Apply ambient occlusion and random darkening from vertex stage
	albedoAlpha.rgb *= input.color.rgb;

	//Apply color map per-pixel
	if (_ColorMapParams.x == 1) {
		float colorMapMask = smoothstep(_ColorMapHeight, 1.0 + _ColorMapHeight, heightGradient);
		albedoAlpha.rgb = lerp(ApplyColorMap(input.positionWS.xyz, albedoAlpha.rgb, _ColorMapStrength), albedoAlpha.rgb, colorMapMask);
	}

	if(_BendTint.a > 0 && (_BendPushStrength > 0 || _BendFlattenStrength > 0))
	{
		float4 bendVector = GetBendVector(input.positionWS.xyz);
		float bendingMask = saturate(bendVector.a * HeightDistanceWeight(input.positionWS, bendVector.xyz));
		albedoAlpha.rgb = lerp(albedoAlpha.rgb, albedoAlpha.rgb * _BendTint.rgb, saturate(bendingMask * heightGradient * _BendTint.a));
	}

	surfaceData.albedo = saturate(albedoAlpha.rgb * _LODDebugColor.rgb);
	//Not using specular setup, free to use this to pass data
	surfaceData.specular = float3(0, 0, 0);
	surfaceData.metallic = 0.0;
	surfaceData.smoothness = lerp(0.0, _Smoothness, mask);
#ifdef _NORMALMAP
	surfaceData.normalTS = SampleNormal(input.uv.zw, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
#else
	surfaceData.normalTS = float3(0.5, 0.5, 1.0);
#endif
	surfaceData.emission = lerp(half3(0, 0, 0), _EmissionColor.rgb, mask);
	surfaceData.occlusion = 1.0;
	surfaceData.alpha = albedoAlpha.a;
	
	#if UNITY_VERSION >= 202030
	surfaceData.clearCoatMask = 0.0h;
	surfaceData.clearCoatSmoothness = 0.0h;
	#endif

	//Debug
	//surfaceData.albedo = surfaceData.smoothness.xxx;
}

//This function is a testament to how convoluted cross-compatibility between difference URP versions has become
void PopulateLightingInputData(Varyings input, half3 normalTS, out InputData inputData)
{
	inputData = (InputData)0;
	
	//#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
	inputData.positionWS = input.positionWS.xyz;
	//#endif

	//Using GetWorldSpaceViewDir returns a constant vector for orthographic camera's, which isn't useful
	half3 viewDirWS = normalize(_WorldSpaceCameraPos - (input.positionWS.xyz));
	
	half3x3 tangentToWorld = 0;
	#if defined(_NORMALMAP)
	float sgn = input.tangentWS.w; // should be either +1 or -1
	float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
	tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);;
	#endif
	
#if defined(_NORMALMAP)
	inputData.normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
#else
	inputData.normalWS = input.normalWS;
#endif
	inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
	
	inputData.viewDirectionWS = viewDirWS;

	#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) //No shadow cascades
	inputData.shadowCoord = input.shadowCoord;
	#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
	inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
	#else
	inputData.shadowCoord = float4(0, 0, 0, 0);
	#endif

	#if UNITY_VERSION >= 202120
	inputData.positionCS = input.positionCS;
	inputData.tangentToWorld = tangentToWorld; //Not actually using this value of the InputData struct, but URP12+ dictates it
	#endif

	inputData.fogCoord = input.fogFactorAndVertexLight.x;
	inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
	
	#if defined(DYNAMICLIGHTMAP_ON) && UNITY_VERSION >= 202120
	inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV.xy, input.vertexSH, inputData.normalWS);
	#elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))

	#if UNITY_VERSION >= 600009 //Not supported, but patched to avoid compile error
	inputData.bakedGI = SAMPLE_GI(input.vertexSH, GetAbsolutePositionWS(inputData.positionWS), inputData.normalWS, inputData.viewDirectionWS, input.positionCS.xy, 1, 1);
	#else
	inputData.bakedGI = SAMPLE_GI(input.vertexSH, GetAbsolutePositionWS(inputData.positionWS), inputData.normalWS, inputData.viewDirectionWS, input.positionCS.xy);
	#endif

	#else
	inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
	#endif
	
#if UNITY_VERSION >= 202030
	inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
	inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#endif

	#if defined(DEBUG_DISPLAY) && UNITY_VERSION >= 202120//URP 12+
	#if defined(DYNAMICLIGHTMAP_ON)
	inputData.dynamicLightmapUV = input.dynamicLightmapUV;
	#endif
	#if defined(LIGHTMAP_ON)
	inputData.staticLightmapUV = input.staticLightmapUV;
	#else
	inputData.vertexSH = input.vertexSH;
	#endif
	#endif
}

#if defined(SHADERPASS_DEFERRED) && UNITY_VERSION >= 202120
FragmentOutput LightingPassFragment(Varyings input)
#else
void LightingPassFragment(Varyings input, out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
	, out float4 outRenderingLayers : SV_Target1
#endif
	)
#endif
{
    Dithering( input.positionCS, input.nDither);

	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

	WindSettings wind = (WindSettings)0;
	if(_WindGustTint > 0)
	{
		wind = PopulateWindSettings(_WindAmbientStrength, _WindSpeed, _WindDirection, _WindSwinging, input.color[_VertexColorWindChannel], _WindObjectRand, _WindVertexRand, _WindRandStrength, _WindGustStrength, _WindGustFreq, _WindGustSpeed);
	}
	SurfaceData surfaceData;
	//Can't use standard function, since including LitInput.hlsl breaks the SRP batcher
	ModifySurfaceData(input, surfaceData);
	
	surfaceData.alpha = ComposeAlpha(surfaceData.alpha, _Cutoff, input.positionCS.xyz, input.positionWS.xyz, _FadeNear, _FadeFar, _FadeAngleThreshold);
	//return float4(surfaceData.alpha.xxx, 1.0);
	AlphaClip(surfaceData.alpha, input.positionCS.xyz, input.positionWS.xyz);
	
	InputData inputData;
	//Standard URP function barely changes, but adds things like clear coat and detail normals
	PopulateLightingInputData(input, surfaceData.normalTS, inputData);

	#if UNITY_VERSION >= 202120
	#if defined(DEBUG_DISPLAY)
	SETUP_DEBUG_TEXTURE_DATA(inputData, input.uv.xy, _BaseMap);
	#endif
	#endif

	//Debugging
	//outColor = float4(AngleFadeFactor(input.positionWS, _FadeAngleThreshold).xxx, 1.0); return;
	//outColor = float4(DistanceFadeFactor(input.positionWS, _FadeNear, _FadeFar).xxx, 1.0); return;
	//outColor = float4(HeightDistanceWeight(input.positionWS.y, GetBendVector(input.positionWS).y).xxx * GetBendVector(input.positionWS).a, 1.0); return;
	//outColor = float4(InterleavedNoise(input.positionCS.xy, PlayerFaceFactor(input.positionWS)).xxx, 1.0); return;
	
	#ifdef DEBUG_BEND_AREA
	float2 bendUV = GetBendMapUV(input.positionWS);
	return float4(any(bendUV.xy) ? bendUV.xy : float2(0,0), 0, 1.0);

	float edgeMask = BoundsEdgeMask(input.positionWS.xz);
	return float4(edgeMask.xxx, 1.0);
	return float4(lerp(float3(1,0,0), float3(0,1,0), edgeMask > 0 ? 1 : 0), 1.0);
	#endif
	
	//Get main light first, need attenuation to mask wind gust
	Light mainLight = GetMainLight(inputData.shadowCoord);

	//Tint by wind gust
	if(_WindGustTint > 0)
	{
		float gustStrength = wind.gustStrength;
		wind.gustStrength = 1;
		float gust = SampleGustMap(input.positionWS.xyz, wind);
		surfaceData.albedo += gust * _WindGustTint * gustStrength * (mainLight.shadowAttenuation) * saturate(input.color.a);
		surfaceData.albedo = saturate(surfaceData.albedo);
	}

	#ifdef DEBUG_BEND_VECTORS
	float4 bendVector = GetBendVector(input.positionWS).xyzw;

	float dist = HeightDistanceWeight(input.positionWS, bendVector.xyz);
	//return float4(bendVector.aaa, 1.0);
	//return float4(saturate(bendVector.yyy), 1.0);
	surfaceData.albedo = saturate(bendVector * 0.5 + 0.5);
	#endif
	
	#if UNITY_VERSION >= 202120
	#ifdef _DBUFFER
	ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
	#endif
	#endif

	TranslucencyData tData = (TranslucencyData)0;
	tData.strengthDirect = _TranslucencyDirect;
	tData.strengthIndirect = _TranslucencyIndirect;
	tData.exponent = _TranslucencyFalloff;
	tData.thickness = saturate(input.color.a);
	tData.offset = _TranslucencyOffset;
	tData.light = mainLight;

	surfaceData.alpha = 1.0;
		
	//Deferred
#if defined(SHADERPASS_DEFERRED) && UNITY_VERSION >= 202120
	// in LitForwardPass GlobalIllumination (and temporarily LightingPhysicallyBased) are called inside UniversalFragmentPBR
	// in Deferred rendering we store the sum of these values (and of emission as well) in the GBuffer
	BRDFData brdfData;
	InitializeBRDFData(surfaceData.albedo, surfaceData.metallic, surfaceData.specular, surfaceData.smoothness, surfaceData.alpha, brdfData);
	
	MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI, inputData.shadowMask);
	half3 color = GlobalIllumination(brdfData, inputData.bakedGI, surfaceData.occlusion, inputData.positionWS, inputData.normalWS, inputData.viewDirectionWS);

	ApplyTranslucency(surfaceData, inputData, tData);
	
	return BRDFDataToGbuffer(brdfData, inputData, surfaceData.smoothness, surfaceData.emission + color, surfaceData.occlusion);

	//Forward
#else

	float3 finalColor = ApplyLighting(surfaceData, inputData, tData);
	finalColor = MixFog(finalColor, inputData.fogCoord);

	outColor = half4(finalColor, surfaceData.alpha);

	#ifdef _WRITE_RENDERING_LAYERS
	uint renderingLayers = GetMeshRenderingLayer();
	outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
	#endif
	
	//return half4(finalColor, surfaceData.alpha);
#endif
}