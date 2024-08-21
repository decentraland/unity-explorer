#ifndef AVATAR_LIT_FORWARD_PASS_INCLUDED
#define AVATAR_LIT_FORWARD_PASS_INCLUDED

#include "Avatar_CelShading_Input.hlsl"
#include "Avatar_CelShading_SurfaceData.hlsl"
#include "Avatar_CelShading_Lighting.hlsl"
#include "Assets/git-submodules/unity-shared-dependencies/Runtime/Shaders/URP/FadeDithering.hlsl"

#if (defined(_NORMALMAP) || (defined(_PARALLAXMAP) && !defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR))) || defined(_DETAIL)
#define REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR
#endif

// Skinning structure
struct VertexInfo
{
    float3 position;
    float3 normal;
    float4 tangent;
};
StructuredBuffer<VertexInfo> _GlobalAvatarBuffer;

// keep this file in sync with LitGBufferPass.hlsl
struct Attributes
{
    uint    index       : SV_VertexID;
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    float2 texcoord1   : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 uvAlbedoNormal           : TEXCOORD0; //Albedo, Normal UVs
    //float4 uvMetallicEmissive       : TEXCOORD1; //Metallic, Emissive UVs
    half3 vertexSH                  : TEXCOORD1;

    #if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
        float3 positionWS               : TEXCOORD2;
    #endif

    float3 normalWS                 : TEXCOORD3;
    #if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
        float4 tangentWS                : TEXCOORD4;    // xyz: tangent, w: sign
    #endif
    float3 viewDirWS                : TEXCOORD5;
    half4 fogFactorAndVertexLight   : TEXCOORD6; // x: fogFactor, yzw: vertex light

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        float4 shadowCoord              : TEXCOORD7;
    #endif

	//NOTE(Brian): needed for FadeDithering
	float4 positionSS               : TEXCOORD8;
    float3 normalMS                 : TEXCOORD9;
    float4 positionCS               : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

void InitializeInputData(Varyings input, half3 normalTS, out InputData_Avatar inputData)
{
    inputData = (InputData_Avatar)0;

    #if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
        inputData.positionWS = input.positionWS;
    #endif

    const half3 viewDirWS = SafeNormalize(input.viewDirWS);
    #if defined(_NORMALMAP) || defined(_DETAIL)
        float sgn = input.tangentWS.w;      // should be either +1 or -1
        float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
        inputData.normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz));
    #else
        inputData.normalWS = input.normalWS;
    #endif

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    inputData.viewDirectionWS = viewDirWS;
    
    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        inputData.shadowCoord = input.shadowCoord;
    #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
        inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
    #else
        inputData.shadowCoord = float4(0, 0, 0, 0);
    #endif

    inputData.fogCoord = input.fogFactorAndVertexLight.x;
    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
    inputData.bakedGI = SampleSHPixel(input.vertexSH, inputData.normalWS);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);
    inputData.matCapUV = mul(UNITY_MATRIX_V, input.normalMS).xy * 0.5 + float2(0.5, 0.5);
}

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

// Used in Standard (Physically Based) shader
Varyings LitPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(_GlobalAvatarBuffer[_lastAvatarVertCount + _lastWearableVertCount + input.index].position.xyz);
    
    // normalWS and tangentWS already normalize.
    // this is required to avoid skewing the direction during interpolation
    // also required for per-vertex lighting and SH evaluation
    //TODO: Tangents
    VertexNormalInputs normalInput = GetVertexNormalInputs(_GlobalAvatarBuffer[_lastAvatarVertCount + _lastWearableVertCount + input.index].normal.xyz, _GlobalAvatarBuffer[_lastAvatarVertCount + _lastWearableVertCount + input.index].tangent.xyzw);

    half3 viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
    half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
    half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

    //float2 uvs[] = { TRANSFORM_TEX(input.texcoord, _BaseMap), TRANSFORM_TEX(input.texcoord1, _BaseMap)};
    output.uvAlbedoNormal.xy = input.texcoord;//uvs[saturate(_BaseMapUVs)];
    output.uvAlbedoNormal.zw = input.texcoord;//uvs[saturate(_NormalMapUVs)];
    //output.uvMetallicEmissive.xy = uvs[saturate(_MetallicMapUVs)];
    //output.uvMetallicEmissive.zw = uvs[saturate(_EmissiveMapUVs)];

    // already normalized from normal transform to WS.
    output.normalWS = normalInput.normalWS;
    output.viewDirWS = viewDirWS;
    #if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR) || defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
        float sign = input.tangentOS.w * GetOddNegativeScale();
        half4 tangentWS = half4(normalInput.tangentWS.xyz, sign);
    #endif
    #if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
        output.tangentWS = tangentWS;
    #endif
    
    output.vertexSH.xyz = SampleSHVertex(output.normalWS.xyz);
    output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);

    #if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
        output.positionWS = vertexInput.positionWS;
    #endif

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        output.shadowCoord = GetShadowCoord(vertexInput);
    #endif

    output.positionCS = vertexInput.positionCS;
	output.positionSS = ComputeScreenPos(vertexInput.positionCS); // needed for FadeDithering
    output.normalMS = mul(UNITY_MATRIX_M, input.normalOS);
    return output;
}

// Used in Standard (Physically Based) shader
half4 LitPassFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    
    SurfaceData_Avatar surfaceData;
    InitializeStandardLitSurfaceDataWithUV2(input.uvAlbedoNormal.xy, input.uvAlbedoNormal.zw, input.uvAlbedoNormal.xy, input.uvAlbedoNormal.zw, surfaceData);

    InputData_Avatar inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);

    half4 color = UniversalFragmentPBR_Avatar(inputData, surfaceData);
 
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = OutputAlpha(color.a, _Surface);    
	color = fadeDithering(color, input.positionWS, input.positionSS);
    return color;
}
#endif