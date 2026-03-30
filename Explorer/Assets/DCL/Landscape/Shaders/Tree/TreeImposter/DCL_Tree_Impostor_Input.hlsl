#ifndef DCL_TREE_IMPOSTOR_INPUT
#define DCL_TREE_IMPOSTOR_INPUT

#if ( SHADER_TARGET > 35 ) && defined( SHADER_API_GLES3 )
	#error For WebGL2/GLES3, please set your shader target to 3.5 via SubShader options. URP shaders in ASE use target 4.5 by default.
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

#ifndef ASE_TESS_FUNCS
#define ASE_TESS_FUNCS
float4 FixedTess( float tessValue )
{
	return tessValue;
}

float CalcDistanceTessFactor (float4 vertex, float minDist, float maxDist, float tess, float4x4 o2w, float3 cameraPos )
{
	float3 wpos = mul(o2w,vertex).xyz;
	float dist = distance (wpos, cameraPos);
	float f = clamp(1.0 - (dist - minDist) / (maxDist - minDist), 0.01, 1.0) * tess;
	return f;
}

float4 CalcTriEdgeTessFactors (float3 triVertexFactors)
{
	float4 tess;
	tess.x = 0.5 * (triVertexFactors.y + triVertexFactors.z);
	tess.y = 0.5 * (triVertexFactors.x + triVertexFactors.z);
	tess.z = 0.5 * (triVertexFactors.x + triVertexFactors.y);
	tess.w = (triVertexFactors.x + triVertexFactors.y + triVertexFactors.z) / 3.0f;
	return tess;
}

float CalcEdgeTessFactor (float3 wpos0, float3 wpos1, float edgeLen, float3 cameraPos, float4 scParams )
{
	float dist = distance (0.5 * (wpos0+wpos1), cameraPos);
	float len = distance(wpos0, wpos1);
	float f = max(len * scParams.y / (edgeLen * dist), 1.0);
	return f;
}

float DistanceFromPlane (float3 pos, float4 plane)
{
	float d = dot (float4(pos,1.0f), plane);
	return d;
}

bool WorldViewFrustumCull (float3 wpos0, float3 wpos1, float3 wpos2, float cullEps, float4 planes[6] )
{
	float4 planeTest;
	planeTest.x = (( DistanceFromPlane(wpos0, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
					(( DistanceFromPlane(wpos1, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
					(( DistanceFromPlane(wpos2, planes[0]) > -cullEps) ? 1.0f : 0.0f );
	planeTest.y = (( DistanceFromPlane(wpos0, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
					(( DistanceFromPlane(wpos1, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
					(( DistanceFromPlane(wpos2, planes[1]) > -cullEps) ? 1.0f : 0.0f );
	planeTest.z = (( DistanceFromPlane(wpos0, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
					(( DistanceFromPlane(wpos1, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
					(( DistanceFromPlane(wpos2, planes[2]) > -cullEps) ? 1.0f : 0.0f );
	planeTest.w = (( DistanceFromPlane(wpos0, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
					(( DistanceFromPlane(wpos1, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
					(( DistanceFromPlane(wpos2, planes[3]) > -cullEps) ? 1.0f : 0.0f );
	return !all (planeTest);
}

float4 DistanceBasedTess( float4 v0, float4 v1, float4 v2, float tess, float minDist, float maxDist, float4x4 o2w, float3 cameraPos )
{
	float3 f;
	f.x = CalcDistanceTessFactor (v0,minDist,maxDist,tess,o2w,cameraPos);
	f.y = CalcDistanceTessFactor (v1,minDist,maxDist,tess,o2w,cameraPos);
	f.z = CalcDistanceTessFactor (v2,minDist,maxDist,tess,o2w,cameraPos);

	return CalcTriEdgeTessFactors (f);
}

float4 EdgeLengthBasedTess( float4 v0, float4 v1, float4 v2, float edgeLength, float4x4 o2w, float3 cameraPos, float4 scParams )
{
	float3 pos0 = mul(o2w,v0).xyz;
	float3 pos1 = mul(o2w,v1).xyz;
	float3 pos2 = mul(o2w,v2).xyz;
	float4 tess;
	tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
	tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
	tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
	tess.w = (tess.x + tess.y + tess.z) / 3.0f;
	return tess;
}

float4 EdgeLengthBasedTessCull( float4 v0, float4 v1, float4 v2, float edgeLength, float maxDisplacement, float4x4 o2w, float3 cameraPos, float4 scParams, float4 planes[6] )
{
	float3 pos0 = mul(o2w,v0).xyz;
	float3 pos1 = mul(o2w,v1).xyz;
	float3 pos2 = mul(o2w,v2).xyz;
	float4 tess;

	if (WorldViewFrustumCull(pos0, pos1, pos2, maxDisplacement, planes))
	{
		tess = 0.0f;
	}
	else
	{
		tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
		tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
		tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
		tess.w = (tess.x + tess.y + tess.z) / 3.0f;
	}
	return tess;
}
#endif //ASE_TESS_FUNCS

#endif