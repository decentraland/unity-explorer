//Stylized Grass Shader
//Staggart Creations (http://staggart.xyz)
//Copyright protected under Unity Asset Store EULA

#ifndef UNITY_CORE_SAMPLERS_INCLUDED
SamplerState sampler_LinearClamp;
#endif

//Set through script
TEXTURE2D(_SplatmapRGB);
float4 _SplatmapRGB_TexelSize;
float4 _SplatMask;
float _SplatChannelStrength;
uint _ColormapMipLevel;

struct FullscreenAttributes
{
	float4 positionOS : POSITION;
	float2 uv         : TEXCOORD0;
};

struct FullscreenVaryings
{
	float4 positionCS : SV_POSITION;
	float2 uv         : TEXCOORD0;
};

FullscreenVaryings FullscreenVert(FullscreenAttributes input)
{
	FullscreenVaryings output;

	output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
	output.uv = input.uv;

	return output;
}

TEXTURE2D_X(_InputColormap);
TEXTURE2D_X(_InputAlphamap);
TEXTURE2D_X(_InputHeightmap);

half4 SplatmapMaskFragment(Varyings IN) : SV_TARGET
{
	float2 splatUV = (IN.uvMainAndLM.xy * (_Control_TexelSize.zw - 1.0f) + 0.5f) * _Control_TexelSize.xy;

	//_Control tex is set by material property block
	float4 splatmap = SAMPLE_TEXTURE2D(_Control, sampler_LinearClamp, splatUV);

	float output = 0;

	if (_SplatMask.r == 1) output = splatmap.r;
	if (_SplatMask.g == 1) output = splatmap.g;
	if (_SplatMask.b == 1) output = splatmap.b;
	if (_SplatMask.a == 1) output = splatmap.a;

	output *= 1-_SplatChannelStrength;
	
	return half4(output.xxx, 1.0);
}

half4 FragMaxBlend(FullscreenVaryings input) : SV_Target
{
	float alpha = SAMPLE_TEXTURE2D_X(_InputAlphamap, sampler_LinearClamp, input.uv).r;
	float height = SAMPLE_TEXTURE2D_X(_InputHeightmap, sampler_LinearClamp, input.uv).r;

	float result = saturate(alpha + height);

	return float4(result.xxx, 1);
}

half4 FragFillBlack(FullscreenVaryings input) : SV_Target
{
	float height = SAMPLE_TEXTURE2D_X(_InputHeightmap, sampler_LinearClamp, input.uv).r;

	float mask = height >= 0 ? height : 1;
	float result = lerp(height, 1, mask);

	return float4(result.xxx, 1);
}

half4 FragMergeAlpha(FullscreenVaryings input) : SV_Target
{
	half3 color = SAMPLE_TEXTURE2D_X(_InputColormap, sampler_LinearClamp, input.uv).rgb;
	float height = SAMPLE_TEXTURE2D_X(_InputHeightmap, sampler_LinearClamp, input.uv).r;

	return float4(color, 1-height);
}

//No mipmaps version
void SplatmapMixLOD(float4 uvMainAndLM, float4 uvSplat01, float4 uvSplat23, inout half4 splatControl, out half weight, out half4 mixedDiffuse)
{
	half4 diffAlbedo[4];

	//Force sampling with mip level
	diffAlbedo[0] = SAMPLE_TEXTURE2D_LOD(_Splat0, sampler_Splat0, uvSplat01.xy, _ColormapMipLevel);
	diffAlbedo[1] = SAMPLE_TEXTURE2D_LOD(_Splat1, sampler_Splat0, uvSplat01.zw, _ColormapMipLevel);
	diffAlbedo[2] = SAMPLE_TEXTURE2D_LOD(_Splat2, sampler_Splat0, uvSplat23.xy, _ColormapMipLevel);
	diffAlbedo[3] = SAMPLE_TEXTURE2D_LOD(_Splat3, sampler_Splat0, uvSplat23.zw, _ColormapMipLevel);

	// Now that splatControl has changed, we can compute the final weight and normalize
	weight = dot(splatControl, 1.0h);

	#ifdef TERRAIN_SPLAT_ADDPASS
	clip(weight <= 0.005h ? -1.0h : 1.0h);
	#endif

	mixedDiffuse = 0.0h;
	mixedDiffuse += diffAlbedo[0] * half4(_DiffuseRemapScale0.rgb * splatControl.rrr, 1.0h);
	mixedDiffuse += diffAlbedo[1] * half4(_DiffuseRemapScale1.rgb * splatControl.ggg, 1.0h);
	mixedDiffuse += diffAlbedo[2] * half4(_DiffuseRemapScale2.rgb * splatControl.bbb, 1.0h);
	mixedDiffuse += diffAlbedo[3] * half4(_DiffuseRemapScale3.rgb * splatControl.aaa, 1.0h);
}

half4 FragUnlit(Varyings IN) : SV_TARGET
{
	half4 hasMask = half4(_LayerHasMask0, _LayerHasMask1, _LayerHasMask2, _LayerHasMask3);
	half4 masks[4];
	ComputeMasks(masks, hasMask, IN);

	float2 splatUV = (IN.uvMainAndLM.xy * (_Control_TexelSize.zw - 1.0f) + 0.5f) * _Control_TexelSize.xy;
	half4 splatControl = SAMPLE_TEXTURE2D(_Control, sampler_Control, splatUV);

	half weight;
	half4 mixedDiffuse;
	half4 defaultSmoothness;
	half3 mixedNormal = 0;

	SplatmapMixLOD(IN.uvMainAndLM, IN.uvSplat01, IN.uvSplat23, splatControl, weight, mixedDiffuse);
	//SplatmapMix(IN.uvMainAndLM, IN.uvSplat01, IN.uvSplat23, splatControl, weight, mixedDiffuse, defaultSmoothness, mixedNormal);

	half3 albedo = mixedDiffuse.rgb;

	return half4(albedo.rgb, 1.0h);
}
