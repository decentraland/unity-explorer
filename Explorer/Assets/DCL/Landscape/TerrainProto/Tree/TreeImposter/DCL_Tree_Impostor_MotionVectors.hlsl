#ifndef DCL_TREE_IMPOSTOR_MOTIONVECTORS
#define DCL_TREE_IMPOSTOR_MOTIONVECTORS

#if defined(ASE_EARLY_Z_DEPTH_OPTIMIZE) && (SHADER_TARGET >= 45)
	#define ASE_SV_DEPTH SV_DepthLessEqual
	#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
#else
	#define ASE_SV_DEPTH SV_Depth
	#define ASE_SV_POSITION_QUALIFIERS
#endif

struct Attributes
{
	float4 positionOS : POSITION;
	float3 positionOld : TEXCOORD4;
	#if _ADD_PRECOMPUTED_VELOCITY
		float3 alembicMotionVector : TEXCOORD5;
	#endif
	half3 normalOS : NORMAL;
	half4 tangentOS : TANGENT;
	
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct PackedVaryings
{
	float4 positionCS : SV_POSITION;
	float4 positionCSNoJitter : TEXCOORD0;
	float4 previousPositionCSNoJitter : TEXCOORD1;
	float3 positionWS : TEXCOORD2;
	float4 UVsFrame1116 : TEXCOORD3;
	float4 UVsFrame2116 : TEXCOORD4;
	float4 UVsFrame3116 : TEXCOORD5;
	float4 octaframe116 : TEXCOORD6;
	float4 viewPos116 : TEXCOORD7;
	UNITY_VERTEX_INPUT_INSTANCE_ID
	UNITY_VERTEX_OUTPUT_STEREO
};

CBUFFER_START(UnityPerMaterial)
float4 _HueVariation;
float4 _AI_SizeOffset;
float3 _Offset;
float3 _AI_BoundsMin;
float3 _AI_BoundsSize;
float _AI_AlphaToCoverage;
float _ClipMask;
float _TextureBias;
float _Parallax;
float _AI_ShadowBias;
float _AI_ShadowView;
float _AI_ForwardBias;
float _Frames;
float _DepthSize;
float _ImpostorSize;
#ifdef ASE_TRANSMISSION
	float _TransmissionShadow;
#endif
#ifdef ASE_TRANSLUCENCY
	float _TransStrength;
	float _TransNormal;
	float _TransScattering;
	float _TransDirect;
	float _TransAmbient;
	float _TransShadow;
#endif
#ifdef ASE_TESSELLATION
	float _TessPhongStrength;
	float _TessValue;
	float _TessMin;
	float _TessMax;
	float _TessEdgeLength;
	float _TessMaxDisp;
#endif
CBUFFER_END

#ifdef SCENEPICKINGPASS
	float4 _SelectionID;
#endif

#ifdef SCENESELECTIONPASS
	int _ObjectId;
	int _PassValue;
#endif

sampler2D _Albedo;
sampler2D _Normals;
sampler2D _Specular;
sampler2D _Occlusion;
sampler2D _Emission;
sampler2D _Position;


struct ImpostorOutput
{
	half3 Albedo;
	half3 Specular;
	half Metallic;
	half3 WorldNormal;
	half Smoothness;
	half Occlusion;
	half3 Emission;
	half Alpha;
};

float2 VectorToOctahedron( float3 N )
{
	N /= dot( 1.0, abs( N ) );
	if( N.z <= 0 )
	{
	N.xy = ( 1 - abs( N.yx ) ) * ( N.xy >= 0 ? 1.0 : -1.0 );
	}
	return N.xy;
}

float3 OctahedronToVector( float2 Oct )
{
	float3 N = float3( Oct, 1.0 - dot( 1.0, abs( Oct ) ) );
	if(N.z< 0 )
	{
	N.xy = ( 1 - abs( N.yx) ) * (N.xy >= 0 ? 1.0 : -1.0 );
	}
	return normalize( N);
}

inline void RayPlaneIntersectionUV( float3 normalOS, float3 rayPosition, float3 rayDirection, out float2 uvs, out float3 localNormal )
{
	float lDotN = dot( rayDirection, normalOS ); 
	float p0l0DotN = dot( -rayPosition, normalOS );
	float t = p0l0DotN / lDotN;
	float3 p = rayDirection * t + rayPosition;
	float3 upVector = float3( 0, 1, 0 );
	float3 tangent = normalize( cross( upVector, normalOS ) + float3( -0.001, 0, 0 ) );
	float3 bitangent = cross( tangent, normalOS );
	float frameX = dot( p, tangent );
	float frameZ = dot( p, bitangent );
	uvs = -float2( frameX, frameZ );
	if( t <= 0.0 )
	uvs = 0;
	float3x3 worldToLocal = float3x3( tangent, bitangent, normalOS );
	localNormal = normalize( mul( worldToLocal, rayDirection ) );
}

float2 VectorToHemiOctahedron( float3 N )
{
	N.xy /= dot( 1.0, abs( N ) );
	return float2( N.x + N.y, N.x - N.y );
}

float3 HemiOctahedronToVector( float2 Oct )
{
	Oct = float2( Oct.x + Oct.y, Oct.x - Oct.y ) * 0.5;
	float3 N = float3( Oct, 1 - dot( 1.0, abs( Oct ) ) );
	return normalize( N );
}

inline void OctaImpostorVertex( inout float3 positionOS, out float3 normalOS, out float4 tangentOS, out float4 uvsFrame1, out float4 uvsFrame2, out float4 uvsFrame3, out float4 octaFrame, out float4 viewPos )
{
	float2 uvOffset = _AI_SizeOffset.zw;
	float parallax = -_Parallax; 
	float UVscale = _ImpostorSize;
	float framesXY = _Frames;
	float prevFrame = framesXY - 1;
	float3 fractions = 1.0 / float3( framesXY, prevFrame, UVscale );
	float fractionsFrame = fractions.x;
	float fractionsPrevFrame = fractions.y;
	float fractionsUVscale = fractions.z;
	float3 worldCameraPos;
	#if defined(UNITY_PASS_SHADOWCASTER)
	float3 worldOrigin = 0;
	float4 perspective = float4( 0, 0, 0, 1 );
	if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )
	{
	perspective = float4( 0, 0, 5000, 0 );
	worldOrigin = AI_ObjectToWorld._m03_m13_m23;
	}
	worldCameraPos = worldOrigin + mul( UNITY_MATRIX_I_V, perspective ).xyz;
	#else
	if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )
	{
	worldCameraPos = AI_ObjectToWorld._m03_m13_m23 + UNITY_MATRIX_I_V._m02_m12_m22 * 5000;
	}
	else
	{
	worldCameraPos = GetCameraRelativePositionWS( _WorldSpaceCameraPos );
	}
	#endif
	float3 objectCameraPosition = mul( AI_WorldToObject, float4( worldCameraPos, 1 ) ).xyz - _Offset.xyz; 
	float3 objectCameraDirection = normalize( objectCameraPosition );
	float3 upVector = float3( 0,1,0 );
	float3 objectHorizontalVector = normalize( cross( objectCameraDirection, upVector ) );
	float3 objectVerticalVector = cross( objectHorizontalVector, objectCameraDirection );
	float2 uvExpansion = positionOS.xy;
	float3 billboard = objectHorizontalVector * uvExpansion.x + objectVerticalVector * uvExpansion.y;
	float3 localDir = billboard - objectCameraPosition; 
	objectCameraDirection = trunc( objectCameraDirection * 65536.0 ) / 65536.0;
	#if defined( _HEMI_ON )
	objectCameraDirection.y = max( 0.001, objectCameraDirection.y );
	float2 frameOcta = VectorToHemiOctahedron( objectCameraDirection.xzy ) * 0.5 + 0.5;
	#else
	float2 frameOcta = VectorToOctahedron( objectCameraDirection.xzy ) * 0.5 + 0.5;
	#endif
	float2 prevOctaFrame = frameOcta * prevFrame;
	float2 baseOctaFrame = floor( prevOctaFrame );
	float2 fractionOctaFrame = ( baseOctaFrame * fractionsFrame );
	float2 octaFrame1 = ( baseOctaFrame * fractionsPrevFrame ) * 2.0 - 1.0;
	#if defined( _HEMI_ON )
	float3 octa1WorldY = HemiOctahedronToVector( octaFrame1 ).xzy;
	#else
	float3 octa1WorldY = OctahedronToVector( octaFrame1 ).xzy;
	#endif
	float3 octa1LocalY;
	float2 uvFrame1;
	RayPlaneIntersectionUV( octa1WorldY, objectCameraPosition, localDir, /*out*/ uvFrame1, /*out*/ octa1LocalY );
	float2 uvParallax1 = octa1LocalY.xy * fractionsFrame * parallax;
	uvFrame1 = ( uvFrame1 * fractionsUVscale + 0.5 ) * fractionsFrame + fractionOctaFrame;
	uvsFrame1 = float4( uvParallax1, uvFrame1 ) - float4( 0, 0, uvOffset );
	float2 fractPrevOctaFrame = frac( prevOctaFrame );
	float2 cornerDifference = lerp( float2( 0,1 ) , float2( 1,0 ) , saturate( ceil( ( fractPrevOctaFrame.x - fractPrevOctaFrame.y ) ) ));
	float2 octaFrame2 = ( ( baseOctaFrame + cornerDifference ) * fractionsPrevFrame ) * 2.0 - 1.0;
	#if defined( _HEMI_ON )
	float3 octa2WorldY = HemiOctahedronToVector( octaFrame2 ).xzy;
	#else
	float3 octa2WorldY = OctahedronToVector( octaFrame2 ).xzy;
	#endif
	float3 octa2LocalY;
	float2 uvFrame2;
	RayPlaneIntersectionUV( octa2WorldY, objectCameraPosition, localDir, /*out*/ uvFrame2, /*out*/ octa2LocalY );
	float2 uvParallax2 = octa2LocalY.xy * fractionsFrame * parallax;
	uvFrame2 = ( uvFrame2 * fractionsUVscale + 0.5 ) * fractionsFrame + ( ( cornerDifference * fractionsFrame ) + fractionOctaFrame );
	uvsFrame2 = float4( uvParallax2, uvFrame2 ) - float4( 0, 0, uvOffset );
	float2 octaFrame3 = ( ( baseOctaFrame + 1 ) * fractionsPrevFrame  ) * 2.0 - 1.0;
	#if defined( _HEMI_ON )
	float3 octa3WorldY = HemiOctahedronToVector( octaFrame3 ).xzy;
	#else
	float3 octa3WorldY = OctahedronToVector( octaFrame3 ).xzy;
	#endif
	float3 octa3LocalY;
	float2 uvFrame3;
	RayPlaneIntersectionUV( octa3WorldY, objectCameraPosition, localDir, /*out*/ uvFrame3, /*out*/ octa3LocalY );
	float2 uvParallax3 = octa3LocalY.xy * fractionsFrame * parallax;
	uvFrame3 = ( uvFrame3 * fractionsUVscale + 0.5 ) * fractionsFrame + ( fractionOctaFrame + fractionsFrame );
	uvsFrame3 = float4( uvParallax3, uvFrame3 ) - float4( 0, 0, uvOffset );
	octaFrame = 0;
	octaFrame.xy = prevOctaFrame;
	#if defined( AI_CLIP_NEIGHBOURS_FRAMES )
	octaFrame.zw = fractionOctaFrame;
	#endif
	positionOS = billboard + _Offset.xyz;
	normalOS = objectCameraDirection;
	tangentOS = float4( objectHorizontalVector, 1 );
	viewPos = 0;
	viewPos.xyz = TransformWorldToView( TransformObjectToWorld( positionOS.xyz ) );
	#ifdef EFFECT_HUE_VARIATION
	float hueVariationAmount = frac( AI_ObjectToWorld[ 0 ].w + AI_ObjectToWorld[ 1 ].w + AI_ObjectToWorld[ 2 ].w );
	viewPos.w = saturate( hueVariationAmount * _HueVariation.a );
	#endif
}

inline void OctaImpostorFragment( inout ImpostorOutput o, out float4 positionCS, out float3 positionWS, float4 uvsFrame1, float4 uvsFrame2, float4 uvsFrame3, float4 octaFrame, float4 viewPos )
{
	float2 fraction = frac( octaFrame.xy );
	float2 invFraction = 1 - fraction;
	float3 weights;
	weights.x = min( invFraction.x, invFraction.y );
	weights.y = abs( fraction.x - fraction.y );
	weights.z = min( fraction.x, fraction.y );
	float4 parallaxSample1 = tex2Dbias( _Normals, float4( uvsFrame1.zw, 0, -1 ) );
	float4 parallaxSample2 = tex2Dbias( _Normals, float4( uvsFrame2.zw, 0, -1 ) );
	float4 parallaxSample3 = tex2Dbias( _Normals, float4( uvsFrame3.zw, 0, -1 ) );
	float2 parallax1_uv = ( ( 0.5 - parallaxSample1.a ) * uvsFrame1.xy ) + uvsFrame1.zw;
	float2 parallax2_uv = ( ( 0.5 - parallaxSample2.a ) * uvsFrame2.xy ) + uvsFrame2.zw;
	float2 parallax3_uv = ( ( 0.5 - parallaxSample3.a ) * uvsFrame3.xy ) + uvsFrame3.zw;
	float4 albedo1 = tex2Dbias( _Albedo, float4( parallax1_uv, 0, _TextureBias ) );
	float4 albedo2 = tex2Dbias( _Albedo, float4( parallax2_uv, 0, _TextureBias ) );
	float4 albedo3 = tex2Dbias( _Albedo, float4( parallax3_uv, 0, _TextureBias ) );
	float4 blendedAlbedo = albedo1 * weights.x + albedo2 * weights.y + albedo3 * weights.z;
	o.Alpha = ( blendedAlbedo.a - _ClipMask );
	clip( o.Alpha );
	#if defined( AI_CLIP_NEIGHBOURS_FRAMES )
	float t = ceil( fraction.x - fraction.y );
	float4 cornerDifference = float4( t, 1 - t, 1, 1 );
	float2 step_1 = ( parallax1_uv - octaFrame.zw ) * _Frames;
	float4 step23 = ( float4( parallax2_uv, parallax3_uv ) -  octaFrame.zwzw ) * _Frames - cornerDifference;
	step_1 = step_1 * ( 1 - step_1 );
	step23 = step23 * ( 1 - step23 );
	float3 steps;
	steps.x = step_1.x * step_1.y;
	steps.y = step23.x * step23.y;
	steps.z = step23.z * step23.w;
	steps = step(-steps, 0);
	float final = dot( steps, weights );
	clip( final - 0.5 );
	#endif
	#ifdef EFFECT_HUE_VARIATION
	half3 shiftedColor = lerp( blendedAlbedo.rgb, _HueVariation.rgb, viewPos.w );
	half maxBase = max( blendedAlbedo.r, max(blendedAlbedo.g, blendedAlbedo.b ) );
	half newMaxBase = max( shiftedColor.r, max(shiftedColor.g, shiftedColor.b ) );
	maxBase /= newMaxBase;
	maxBase = maxBase * 0.5f + 0.5f;
	shiftedColor.rgb *= maxBase;
	blendedAlbedo.rgb = saturate( shiftedColor );
	#endif
	o.Albedo = blendedAlbedo.rgb;
	float4 normals1 = tex2Dbias( _Normals, float4( parallax1_uv, 0, _TextureBias ) );
	float4 normals2 = tex2Dbias( _Normals, float4( parallax2_uv, 0, _TextureBias ) );
	float4 normals3 = tex2Dbias( _Normals, float4( parallax3_uv, 0, _TextureBias ) );
	float4 blendedNormal = normals1 * weights.x  + normals2 * weights.y + normals3 * weights.z;
	float3 localNormal = blendedNormal.rgb * 2.0 - 1.0;
	o.WorldNormal = normalize( mul( (float3x3)AI_ObjectToWorld, localNormal ) );
	float depth = ( ( parallaxSample1.a * weights.x + parallaxSample2.a * weights.y + parallaxSample3.a * weights.z ) - 0.5 ) * _DepthSize * length( AI_ObjectToWorld[ 2 ].xyz );
	#if defined( _SPECULARMAP )
	float4 spec1 = tex2Dbias( _Specular, float4( parallax1_uv, 0, _TextureBias ) );
	float4 spec2 = tex2Dbias( _Specular, float4( parallax2_uv, 0, _TextureBias ) );
	float4 spec3 = tex2Dbias( _Specular, float4( parallax3_uv, 0, _TextureBias ) );
	float4 blendedSpec = spec1 * weights.x  + spec2 * weights.y + spec3 * weights.z;
	o.Specular = blendedSpec.rgb;
	o.Smoothness = blendedSpec.a;
	#else
	o.Specular = 0;
	o.Smoothness = 0;
	#endif
	#if defined( _OCCLUSIONMAP )
	float4 occlusion1 = tex2Dbias( _Occlusion, float4( parallax1_uv, 0, _TextureBias ) );
	float4 occlusion2 = tex2Dbias( _Occlusion, float4( parallax2_uv, 0, _TextureBias ) );
	float4 occlusion3 = tex2Dbias( _Occlusion, float4( parallax3_uv, 0, _TextureBias ) );
	o.Occlusion = occlusion1.g * weights.x  + occlusion2.g * weights.y + occlusion3.g * weights.z;
	#else
	o.Occlusion = 1;
	#endif
	#if defined( _EMISSIONMAP )
	float4 emission1 = tex2Dbias( _Emission, float4( parallax1_uv, 0, _TextureBias ) );
	float4 emission2 = tex2Dbias( _Emission, float4( parallax2_uv, 0, _TextureBias ) );
	float4 emission3 = tex2Dbias( _Emission, float4( parallax3_uv, 0, _TextureBias ) );
	o.Emission = emission1.rgb * weights.x  + emission2.rgb * weights.y + emission3.rgb * weights.z;
	#else
	o.Emission = 0;
	#endif
	#if defined( _POSITIONMAP )
	float4 position1 = tex2Dbias( _Position, float4( parallax1_uv, 0, _TextureBias ) );
	float4 position2 = tex2Dbias( _Position, float4( parallax2_uv, 0, _TextureBias ) );
	float4 position3 = tex2Dbias( _Position, float4( parallax3_uv, 0, _TextureBias ) );
	float4 blendedPosition = position1 * weights.x  + position2 * weights.y + position3 * weights.z;
	float3 objectPosition = blendedPosition.xyz * _AI_BoundsSize + _AI_BoundsMin;
	float3 worldPosition = mul( AI_ObjectToWorld, float4( objectPosition, 1 ) ).xyz;
	if ( blendedPosition.a > 0 )
	{
	viewPos.xyz = mul( UNITY_MATRIX_V, float4( worldPosition.xyz, 1 ) ).xyz;
	depth = 0;
	}
	#endif
	#if ( defined(SHADERPASS) && ((defined(SHADERPASS_SHADOWS) && SHADERPASS == SHADERPASS_SHADOWS) || (defined(SHADERPASS_SHADOWCASTER) && SHADERPASS == SHADERPASS_SHADOWCASTER)) ) || defined(UNITY_PASS_SHADOWCASTER)
	viewPos.z += depth * _AI_ShadowView - _AI_ShadowBias;
	#else 
	viewPos.z += depth + _AI_ForwardBias;
	#endif
	positionWS = mul( UNITY_MATRIX_I_V, float4( viewPos.xyz, 1 ) ).xyz;
	#if defined(SHADERPASS) && defined(UNITY_PASS_SHADOWCASTER)
	#if _CASTING_PUNCTUAL_LIGHT_SHADOW
	float3 lightDirectionWS = normalize( _LightPosition - positionWS );
	#else
	float3 lightDirectionWS = _LightDirection;
	#endif
	positionCS = TransformWorldToHClip( ApplyShadowBias( positionWS, float3( 0, 0, 0 ), lightDirectionWS ) );
	#if UNITY_REVERSED_Z
	positionCS.z = min( positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE );
	#else
	positionCS.z = max( positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE );
	#endif
	#else
	positionCS = mul( UNITY_MATRIX_P, float4( viewPos.xyz, 1 ) );
	#endif
	positionCS.xyz /= positionCS.w;
	if( UNITY_NEAR_CLIP_VALUE < 0 )
	positionCS = positionCS * 0.5 + 0.5;
}


PackedVaryings VertexFunction( Attributes input  )
{
	PackedVaryings output = (PackedVaryings)0;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

	OctaImpostorVertex( input.positionOS.xyz, input.normalOS.xyz, input.tangentOS, output.UVsFrame1116, output.UVsFrame2116, output.UVsFrame3116, output.octaframe116, output.viewPos116 );
	

	#ifdef ASE_ABSOLUTE_VERTEX_POS
		float3 defaultVertexValue = input.positionOS.xyz;
	#else
		float3 defaultVertexValue = float3(0, 0, 0);
	#endif

	float3 vertexValue = defaultVertexValue;

	#ifdef ASE_ABSOLUTE_VERTEX_POS
		input.positionOS.xyz = vertexValue;
	#else
		input.positionOS.xyz += vertexValue;
	#endif

	VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );

	#if defined(APPLICATION_SPACE_WARP_MOTION)
		output.positionCSNoJitter = mul(_NonJitteredViewProjMatrix, mul(UNITY_MATRIX_M, input.positionOS));
		output.positionCS = output.positionCSNoJitter;
	#else
		output.positionCS = vertexInput.positionCS;
		output.positionCSNoJitter = mul(_NonJitteredViewProjMatrix, mul(UNITY_MATRIX_M, input.positionOS));
	#endif

	float4 prevPos = ( unity_MotionVectorsParams.x == 1 ) ? float4( input.positionOld, 1 ) : input.positionOS;

	#if _ADD_PRECOMPUTED_VELOCITY
		prevPos = prevPos - float4(input.alembicMotionVector, 0);
	#endif

	output.previousPositionCSNoJitter = mul( _PrevViewProjMatrix, mul( UNITY_PREV_MATRIX_M, prevPos ) );

	output.positionWS = vertexInput.positionWS;

	// removed in ObjectMotionVectors.hlsl found in unity 6000.0.23 and higher
	//ApplyMotionVectorZBias( output.positionCS );
	return output;
}

PackedVaryings vert ( Attributes input )
{
	return VertexFunction( input );
}

half4 frag(	PackedVaryings input
	#if defined( ASE_DEPTH_WRITE_ON )
	,out float outputDepth : ASE_SV_DEPTH
	#endif
	 ) : SV_Target
{
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( input );

	float3 PositionWS = input.positionWS;
	float3 PositionRWS = GetCameraRelativePositionWS( PositionWS );
	float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
	float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;

	ImpostorOutput io = ( ImpostorOutput )0;
	OctaImpostorFragment( io, ClipPos, PositionWS, input.UVsFrame1116, input.UVsFrame2116, input.UVsFrame3116, input.octaframe116, input.viewPos116 );
	

	float Alpha = io.Alpha;
	float AlphaClipThreshold = 0.5;

	#if defined( ASE_DEPTH_WRITE_ON )
		float DeviceDepth = ClipPos.z;
	#endif

	#ifdef _ALPHATEST_ON
		clip(Alpha - AlphaClipThreshold);
	#endif

	#if defined(ASE_CHANGES_WORLD_POS)
		float3 positionOS = mul( GetWorldToObjectMatrix(),  float4( PositionWS, 1.0 ) ).xyz;
		float3 previousPositionWS = mul( GetPrevObjectToWorldMatrix(),  float4( positionOS, 1.0 ) ).xyz;
		input.positionCSNoJitter = mul( _NonJitteredViewProjMatrix, float4( PositionWS, 1.0 ) );
		input.previousPositionCSNoJitter = mul( _PrevViewProjMatrix, float4( previousPositionWS, 1.0 ) );
	#endif

	#if defined(LOD_FADE_CROSSFADE)
		LODFadeCrossFade( input.positionCS );
	#endif

	#if defined( ASE_DEPTH_WRITE_ON )
		outputDepth = DeviceDepth;
	#endif

	#if defined(APPLICATION_SPACE_WARP_MOTION)
		return float4( CalcAswNdcMotionVectorFromCsPositions( input.positionCSNoJitter, input.previousPositionCSNoJitter ), 1 );
	#else
		return float4( CalcNdcMotionVectorFromCsPositions( input.positionCSNoJitter, input.previousPositionCSNoJitter ), 0, 0 );
	#endif
}

#endif