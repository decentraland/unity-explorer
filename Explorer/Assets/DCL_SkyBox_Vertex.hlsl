#ifndef DCL_SKYBOX_VERTEX_INCLUDED
#define DCL_SKYBOX_VERTEX_INCLUDED

// Includes

#include "Assets/DCL_SkyBox_Data.hlsl"
#include "UnityCG.cginc"
#include "UnityStandardConfig.cginc"
//#include "UnityCustomRenderTexture.cginc"
#include "Lighting.cginc"

//#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Sampling/Sampling.hlsl"

// #if defined(UNITY_COLORSPACE_GAMMA)
//     #define GAMMA 2
//     #define COLOR_2_GAMMA(color) color
//     #define COLOR_2_LINEAR(color) color*color
//     #define LINEAR_2_OUTPUT(color) sqrt(color)
// #else
//     #define GAMMA 2.2
//     // HACK: to get gfx-tests in Gamma mode to agree until UNITY_ACTIVE_COLORSPACE_IS_GAMMA is working properly
//     #define COLOR_2_GAMMA(color) ((unity_ColorSpaceDouble.r>2.0) ? pow(color,1.0/GAMMA) : color)
//     #define COLOR_2_LINEAR(color) color
//     #define LINEAR_2_LINEAR(color) color
// #endif

// // RGB wavelengths
// // .35 (.62=158), .43 (.68=174), .525 (.75=190)
// static const float3 kDefaultScatteringWavelength = float3(.65, .57, .475);
// static const float3 kVariableRangeForScatteringWavelength = float3(.15, .15, .15);

// #define OUTER_RADIUS 1.025
// static const float kOuterRadius = OUTER_RADIUS;
// static const float kOuterRadius2 = OUTER_RADIUS*OUTER_RADIUS;
// static const float kInnerRadius = 1.0;
// static const float kInnerRadius2 = 1.0;

// static const float kCameraHeight = 0.0001;

// #define kRAYLEIGH (lerp(0.0, 0.0025, pow(_AtmosphereThickness,2.5)))      // Rayleigh constant
// #define kMIE 0.0010             // Mie constant
// #define kSUN_BRIGHTNESS 20.0    // Sun brightness

// #define kMAX_SCATTER 50.0 // Maximum scattering value, to prevent math overflows on Adrenos

// static const half kHDSundiskIntensityFactor = 15.0;
// static const half kSimpleSundiskIntensityFactor = 27.0;

// static const half kSunScale = 400.0 * kSUN_BRIGHTNESS;
// static const float kKmESun = kMIE * kSUN_BRIGHTNESS;
// static const float kKm4PI = kMIE * 4.0 * 3.14159265;
// static const float kScale = 1.0 / (OUTER_RADIUS - 1.0);
// static const float kScaleDepth = 0.25;
// static const float kScaleOverScaleDepth = (1.0 / (OUTER_RADIUS - 1.0)) / 0.25;
// static const float kSamples = 2.0; // THIS IS UNROLLED MANUALLY, DON'T TOUCH

// #define MIE_G (-0.990)
// #define MIE_G2 0.9801

// #define SKY_GROUND_THRESHOLD 0.02

// #ifndef SKYBOX_COLOR_IN_TARGET_COLOR_SPACE
//     #if defined(SHADER_API_MOBILE)
//         #define SKYBOX_COLOR_IN_TARGET_COLOR_SPACE 1
//     #else
//         #define SKYBOX_COLOR_IN_TARGET_COLOR_SPACE 0
//     #endif
// #endif

// float scale(float inCos)
// {
//     float x = 1.0 - inCos;
//     return 0.25 * exp(-0.00287 + x*(0.459 + x*(3.83 + x*(-6.80 + x*5.25))));
// }

// // Calculates the Rayleigh phase function
// half getRayleighPhase(half eyeCos2)
// {
//     return 0.75 + 0.75*eyeCos2;
// }
// half getRayleighPhase(half3 light, half3 ray)
// {
//     half eyeCos = dot(light, ray);
//     return getRayleighPhase(eyeCos * eyeCos);
// }

// uniform half _Exposure; // HDR exposure
// uniform half3 _GroundColor;
// uniform half _SunSize;
// uniform half _SunSizeConvergence;
// uniform half3 _SkyTint;
// uniform half _AtmosphereThickness;

int _CMFParams;
// #define _CubeFace _Params
// static const int _CubeFaceSTUFF = 0;

float3 ComputeCubeDirection(float2 globalTexcoord)
{
    //return float3(0.0, 1.0, 0.0);
    float2 xy = globalTexcoord * 2.0 - 1.0;
    float3 direction;
    if(_CMFParams == 20)
    {
        direction = normalize(float3(1.0, -xy.y, -xy.x));
    }
    // if(_CubeFace == 0.0)
    // {
    //     direction = normalize(float3(1.0, -xy.y, -xy.x));
    // }
    // else if(_CubeFace == 1.0)
    // {
    //     direction = normalize(float3(-1.0, -xy.y, xy.x));
    // }
    // else if(_CubeFace == 2.0)
    // {
    //     direction = normalize(float3(xy.x, 1.0, xy.y));
    // }
    // else if(_CubeFace == 3.0)
    // {
    //     direction = normalize(float3(xy.x, -1.0, -xy.y));
    // }
    // else if(_CubeFace == 4.0)
    // {
    //     direction = normalize(float3(xy.x, -xy.y, 1.0));
    // }
    // else if(_CubeFace == 5.0)
    // {
    //     direction = normalize(float3(-xy.x, -xy.y, -1.0));
    // }

    return direction;
}

sk_v2f sk_vert(sk_appdata IN)
{
    sk_v2f OUT;

    #if UNITY_UV_STARTS_AT_TOP
        const float2 vertexPositions[3] =
        {
            { -1.0f,  3.0f },
            { -1.0f, -1.0f },
            {  3.0f, -1.0f }
        };

        const float2 texCoords[3] =
        {
            { 0.0f, -1.0f },
            { 0.0f, 1.0f },
            { 2.0f, 1.0f }
        };
    #else
        const float2 vertexPositions[3] =
        {
            {  3.0f,  3.0f },
            { -1.0f, -1.0f },
            { -1.0f,  3.0f }
        };

        const float2 texCoords[3] =
        {
            { 2.0f, 1.0f },
            { 0.0f, -1.0f },
            { 0.0f, 1.0f }
        };
    #endif

    uint primitiveID = IN.vertexID / 3;
    uint vertexID = IN.vertexID % 3;

    float2 pos = vertexPositions[vertexID];
    OUT.vertex = float4(pos, 0.0, 1.0);
    OUT.primitiveID = primitiveID;
    OUT.localTexcoord = float3(texCoords[vertexID], 0.0f);
    OUT.globalTexcoord = float3(pos.xy * 0.5 + 0.5, 1.0);
    #if UNITY_UV_STARTS_AT_TOP
        OUT.globalTexcoord.y = 1.0 - OUT.globalTexcoord.y;
    #endif
    //OUT.direction = OUT.globalTexcoord; // HACK_TEST
    OUT.direction = ComputeCubeDirection(OUT.globalTexcoord.xy);
    return OUT;
}

#endif // DCL_SKYBOX_VERTEX_INCLUDED