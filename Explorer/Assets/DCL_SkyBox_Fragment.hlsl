#ifndef DCL_SKYBOX_FRAGMENT_INCLUDED
#define DCL_SKYBOX_FRAGMENT_INCLUDED

// Includes
#include "Assets/DCL_SkyBox_Data.hlsl"
#include "UnityLightingCommon.cginc"

#if defined(UNITY_COLORSPACE_GAMMA)
    #define GAMMA 2
    #define COLOR_2_GAMMA(color) color
    #define COLOR_2_LINEAR(color) color*color
    #define LINEAR_2_OUTPUT(color) sqrt(color)
#else
    #define GAMMA 2.2
    // HACK: to get gfx-tests in Gamma mode to agree until UNITY_ACTIVE_COLORSPACE_IS_GAMMA is working properly
    #define COLOR_2_GAMMA(color) ((unity_ColorSpaceDouble.r>2.0) ? pow(color,1.0/GAMMA) : color)
    #define COLOR_2_LINEAR(color) color
    #define LINEAR_2_LINEAR(color) color
#endif

// RGB wavelengths
// .35 (.62=158), .43 (.68=174), .525 (.75=190)
static const float3 kDefaultScatteringWavelength = float3(.65, .57, .475);
static const float3 kVariableRangeForScatteringWavelength = float3(.15, .15, .15);

#define OUTER_RADIUS 1.025
static const float kOuterRadius = OUTER_RADIUS;
static const float kOuterRadius2 = OUTER_RADIUS*OUTER_RADIUS;
static const float kInnerRadius = 1.0;
static const float kInnerRadius2 = 1.0;

static const float kCameraHeight = 0.0001;

#define kRAYLEIGH (lerp(0.0, 0.0025, pow(_AtmosphereThickness,2.5)))      // Rayleigh constant
#define kMIE 0.0010             // Mie constant
#define kSUN_BRIGHTNESS 20.0    // Sun brightness

#define kMAX_SCATTER 50.0 // Maximum scattering value, to prevent math overflows on Adrenos

static const half kHDSundiskIntensityFactor = 15.0;
static const half kSimpleSundiskIntensityFactor = 27.0;

static const half kSunScale = 400.0 * kSUN_BRIGHTNESS;
static const float kKmESun = kMIE * kSUN_BRIGHTNESS;
static const float kKm4PI = kMIE * 4.0 * 3.14159265;
static const float kScale = 1.0 / (OUTER_RADIUS - 1.0);
static const float kScaleDepth = 0.25;
static const float kScaleOverScaleDepth = (1.0 / (OUTER_RADIUS - 1.0)) / 0.25;
static const float kSamples = 2.0; // THIS IS UNROLLED MANUALLY, DON'T TOUCH

#define MIE_G (-0.990)
#define MIE_G2 0.9801

#define SKY_GROUND_THRESHOLD 0.02

#ifndef SKYBOX_COLOR_IN_TARGET_COLOR_SPACE
    #if defined(SHADER_API_MOBILE)
        #define SKYBOX_COLOR_IN_TARGET_COLOR_SPACE 1
    #else
        #define SKYBOX_COLOR_IN_TARGET_COLOR_SPACE 0
    #endif
#endif

float scale(float inCos)
{
    float x = 1.0 - inCos;
    return 0.25 * exp(-0.00287 + x*(0.459 + x*(3.83 + x*(-6.80 + x*5.25))));
}

// Calculates the Rayleigh phase function
half getRayleighPhase(half eyeCos2)
{
    return 0.75 + 0.75*eyeCos2;
}
half getRayleighPhase(half3 light, half3 ray)
{
    half eyeCos = dot(light, ray);
    return getRayleighPhase(eyeCos * eyeCos);
}

// Calculates the Mie phase function
half getMiePhase(half eyeCos, half eyeCos2, float fSunSize)
{
    half temp = 1.0 + MIE_G2 - 2.0 * MIE_G * eyeCos;
    temp = pow(temp, pow(fSunSize,0.65) * 10);
    temp = max(temp,1.0e-4); // prevent division by zero, esp. in half precision
    temp = 1.5 * ((1.0 - MIE_G2) / (2.0 + MIE_G2)) * (1.0 + eyeCos2) / temp;
    #if defined(UNITY_COLORSPACE_GAMMA) && SKYBOX_COLOR_IN_TARGET_COLOR_SPACE
        temp = pow(temp, .454545);
    #endif
    return temp;
}

// Calculates the sun shape
half calcSunAttenuation(half3 lightPos, half3 ray, float fSunSize, float fSunSizeConvergence)
{
    half focusedEyeCos = pow(saturate(dot(lightPos, ray)), fSunSizeConvergence);
    return getMiePhase(-focusedEyeCos, focusedEyeCos * focusedEyeCos, fSunSize);
}

/////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////

float _SunSize;
float _SunSizeConvergence;
float4 _SkyTint;
float _AtmosphereThickness;
float4 _GroundColor;
float _Exposure;

float4 sk_frag(sk_v2f IN) : SV_Target
{
    float3 vSunPos = normalize(float3(-0.36f, -0.4f, 0.9f));
    float3 kSkyTintInGammaSpace = COLOR_2_GAMMA(_SkyTint); // convert tint from Linear back to Gamma
    float3 kScatteringWavelength = lerp (   kDefaultScatteringWavelength-kVariableRangeForScatteringWavelength,
                                            kDefaultScatteringWavelength+kVariableRangeForScatteringWavelength,
                                            half3(1,1,1) - kSkyTintInGammaSpace); // using Tint in sRGB gamma allows for more visually linear interpolation and to keep (.5) at (128, gray in sRGB) point
    float3 kInvWavelength = 1.0 / pow(kScatteringWavelength, 4);

    float kKrESun = kRAYLEIGH * kSUN_BRIGHTNESS;
    float kKr4PI = kRAYLEIGH * 4.0 * 3.14159265;

    // The camera's current position
    float3 cameraPos = float3(0, kInnerRadius + kCameraHeight, 0);    

    // Get the ray from the camera to the fragment and its length (which is the far point of the ray passing through the atmosphere)
    float3 eyeRay = normalize(IN.direction);

    float far = 0.0;
    half3 cIn, cOut;

    if(eyeRay.y >= 0.0) // Sky
    {
        // Calculate the length of the "atmosphere"
        far = sqrt(kOuterRadius2 + kInnerRadius2 * eyeRay.y * eyeRay.y - kInnerRadius2) - kInnerRadius * eyeRay.y;

        float3 pos = cameraPos + far * eyeRay;

        // Calculate the ray's starting position, then calculate its scattering offset
        float height = kInnerRadius + kCameraHeight;
        float depth = exp(kScaleOverScaleDepth * (-kCameraHeight));
        float startAngle = dot(eyeRay, cameraPos) / height;
        float startOffset = depth*scale(startAngle);


        // Initialize the scattering loop variables
        float sampleLength = far / kSamples;
        float scaledLength = sampleLength * kScale;
        float3 sampleRay = eyeRay * sampleLength;
        float3 samplePoint = cameraPos + sampleRay * 0.5;

        // Now loop through the sample rays
        float3 frontColor = float3(0.0, 0.0, 0.0);
        // Weird workaround: WP8 and desktop FL_9_3 do not like the for loop here
        // (but an almost identical loop is perfectly fine in the ground calculations below)
        // Just unrolling this manually seems to make everything fine again.
        // for(int i=0; i<int(kSamples); i++)
        {
            float height = length(samplePoint);
            float depth = exp(kScaleOverScaleDepth * (kInnerRadius - height));
            float lightAngle = dot(_WorldSpaceLightPos0.xyz, samplePoint) / height;
            float cameraAngle = dot(eyeRay, samplePoint) / height;
            float scatter = (startOffset + depth*(scale(lightAngle) - scale(cameraAngle)));
            float3 attenuate = exp(-clamp(scatter, 0.0, kMAX_SCATTER) * (kInvWavelength * kKr4PI + kKm4PI));

            float fDepthScaledByLength = depth * scaledLength;
            frontColor += attenuate * fDepthScaledByLength;
            samplePoint += sampleRay;
        }
        {
            float height = length(samplePoint);
            float depth = exp(kScaleOverScaleDepth * (kInnerRadius - height));
            float lightAngle = dot(_WorldSpaceLightPos0.xyz, samplePoint) / height;
            float cameraAngle = dot(eyeRay, samplePoint) / height;
            float scatter = (startOffset + depth*(scale(lightAngle) - scale(cameraAngle)));
            float3 attenuate = exp(-clamp(scatter, 0.0, kMAX_SCATTER) * (kInvWavelength * kKr4PI + kKm4PI));

            float fDepthScaledByLength = depth * scaledLength;
            frontColor += attenuate * fDepthScaledByLength;
            samplePoint += sampleRay;
        }

        // Finally, scale the Mie and Rayleigh colors and set up the varying variables for the pixel shader
        cIn = frontColor * (kInvWavelength * kKrESun);
        cOut = frontColor * kKmESun;
    }
    else // Ground
    {
        far = (-kCameraHeight) / (min(-0.001, eyeRay.y));

        float3 pos = cameraPos + far * eyeRay;

        // Calculate the ray's starting position, then calculate its scattering offset
        float depth = exp((-kCameraHeight) * (1.0/kScaleDepth));
        float cameraAngle = dot(-eyeRay, pos);
        float lightAngle = dot(_WorldSpaceLightPos0.xyz, pos);
        float cameraScale = scale(cameraAngle);
        float lightScale = scale(lightAngle);
        float cameraOffset = depth*cameraScale;
        float temp = (lightScale + cameraScale);

        // Initialize the scattering loop variables
        float sampleLength = far / kSamples;
        float scaledLength = sampleLength * kScale;
        float3 sampleRay = eyeRay * sampleLength;
        float3 samplePoint = cameraPos + sampleRay * 0.5;

        // Now loop through the sample rays
        float3 frontColor = float3(0.0, 0.0, 0.0);
        float3 attenuate;
        // for(int i=0; i<int(kSamples); i++) // Loop removed because we kept hitting SM2.0 temp variable limits. Doesn't affect the image too much.
        {
            float height = length(samplePoint);
            float depth = exp(kScaleOverScaleDepth * (kInnerRadius - height));
            float scatter = depth*temp - cameraOffset;
            attenuate = exp(-clamp(scatter, 0.0, kMAX_SCATTER) * (kInvWavelength * kKr4PI + kKm4PI));
            frontColor += attenuate * (depth * scaledLength);
            samplePoint += sampleRay;
        }

        cIn = frontColor * (kInvWavelength * kKrESun + kKmESun);
        cOut = clamp(attenuate, 0.0, 1.0);
    }

    // if we want to calculate color in vprog:
    // 1. in case of linear: multiply by _Exposure in here (even in case of lerp it will be common multiplier, so we can skip mul in fshader)
    // 2. in case of gamma and SKYBOX_COLOR_IN_TARGET_COLOR_SPACE: do sqrt right away instead of doing that in fshader
    //cIn = float3(1.0, 1.0, 1.0) * 0.5;
    float3 groundColor = _Exposure * (cIn + COLOR_2_LINEAR(_GroundColor) * cOut);
    float3 skyColor    = _Exposure * (cIn * getRayleighPhase(vSunPos.xyz, -eyeRay));
    //groundColor = (_GroundColor.xyz);
    //skyColor    = (_SkyTint.xyz);

    // The sun should have a stable intensity in its course in the sky. Moreover it should match the highlight of a purely specular material.
    // This matching was done using the standard shader BRDF1 on the 5/31/2017
    // Finally we want the sun to be always bright even in LDR thus the normalization of the lightColor for low intensity.
    float3 vSunColour = _LightColor0.xyz;
    vSunColour = float3(1.0, 1.0, 1.0);
    half lightColorIntensity = clamp(length(vSunColour), 0.25, 1);
    float3 vSunColour_Intensity    = kHDSundiskIntensityFactor * saturate(cOut) * vSunColour / lightColorIntensity;

    #if defined(UNITY_COLORSPACE_GAMMA) && SKYBOX_COLOR_IN_TARGET_COLOR_SPACE
        groundColor = sqrt(groundColor);
        skyColor    = sqrt(skyColor);
        vSunColour_Intensity    = sqrt(vSunColour_Intensity);
    #endif

    half3 col = half3(0.0, 0.0, 0.0);
    float3 ray = normalize(IN.direction);
    half y = ray.y / SKY_GROUND_THRESHOLD;
    y = -y;

    // if we did precalculate color in vprog: just do lerp between them
    col = lerp(skyColor, groundColor, saturate(y));

    //if(y < 0.0)
    {
        col += vSunColour_Intensity * calcSunAttenuation(vSunPos.xyz, -ray, _SunSize, _SunSizeConvergence);
    }

    float LdotV =pow(saturate(dot(vSunPos.xyz, -ray)), 10);
    if(LdotV >= 0.99f)
    {
        col += float3(1.0, 1.0, 1.0);
    }

    #if defined(UNITY_COLORSPACE_GAMMA) && !SKYBOX_COLOR_IN_TARGET_COLOR_SPACE
        col = LINEAR_2_OUTPUT(col);
    #endif

    //return half4(IN.direction, 1.0);
    return half4(col,1.0);
}

#endif // DCL_SKYBOX_FRAGMENT_INCLUDED