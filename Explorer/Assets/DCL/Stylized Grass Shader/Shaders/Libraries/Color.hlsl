//Stylized Grass Shader
//Staggart Creations (http://staggart.xyz)
//Copyright protected under Unity Asset Store EULA

//Single channel overlay
float BlendOverlay(float a, float b)
{
	return (b < 0.5) ? 2.0 * a * b : 1.0 - 2.0 * (1.0 - a) * (1.0 - b);
}

//RGB overlay
float3 BlendOverlay(float3 a, float3 b)
{
	float3 color;
	color.r = BlendOverlay(a.r, b.r);
	color.g = BlendOverlay(a.g, b.g);
	color.b = BlendOverlay(a.b, b.b);
	return color;
}


float4 SampleColorMapTexture(in float3 positionWS) 
{
	#ifndef GRASS_COMMON_INCLUDED
	return 0;
	#else
	float2 uv = GetColorMapUV(positionWS);

	return SAMPLE_TEXTURE2D(_ColorMap, sampler_ColorMap, uv).rgba;
	#endif
}

//---------------------------------------------------------------//

//Shading (RGB=hue - A=brightness)
float3 ApplyVertexColor(in float3 vertexPos, in float3 positionWS, in float3 baseColor, in float mask, in float aoAmount, in float darkening, in float posOffset)
{
	float3 col = baseColor;

	//Apply darkening
	float rand = frac(posOffset + vertexPos.x + vertexPos.z * 4.0);
	float vertexDarkening = lerp(1.0, rand, darkening * mask); //Only apply to top vertices
	
	//Apply ambient occlusion
	float ambientOcclusion = lerp(1.0, mask, aoAmount);

	col.rgb *= vertexDarkening * ambientOcclusion;

	return col;
}

float3 ApplyColorMap(float3 positionWS, float3 iColor, float s) 
{
	return lerp(iColor, SampleColorMapTexture(positionWS).rgb, s);
}

//Shader Graph and Amplify Shader Editor

//Common.hlsl cannot be included, as this redefines vert/frag structs
#ifndef GRASS_COMMON_INCLUDED
float4 _ColorMapUV;
TEXTURE2D(_ColorMap); SAMPLER(sampler_ColorMap);
#endif

//SG & ASE
void SampleColorMapTexture_float(in float3 positionWS, out float4 color) 
{
	//Note: Unrolled from the GetColorMapUV, check that these are always in sync!
	float2 uv = (positionWS.xz * _ColorMapUV.z) - (_ColorMapUV.xy * _ColorMapUV.z);
	
	color = SAMPLE_TEXTURE2D(_ColorMap, sampler_ColorMap, uv).rgba;
}