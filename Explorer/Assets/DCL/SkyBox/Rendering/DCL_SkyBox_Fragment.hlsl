#ifndef DCL_SKYBOX_FRAGMENT_INCLUDED
#define DCL_SKYBOX_FRAGMENT_INCLUDED

// Includes
#include "./DCL_SkyBox_Data.hlsl"
#include "Lighting.cginc"


#define GAMMA 2.2
// HACK: to get gfx-tests in Gamma mode to agree until UNITY_ACTIVE_COLORSPACE_IS_GAMMA is working properly
#define COLOR_2_GAMMA(color) ((unity_ColorSpaceDouble.r>2.0) ? pow(color,1.0/GAMMA) : color)
#define COLOR_2_LINEAR(color) color
#define LINEAR_2_LINEAR(color) color

/*
// RGB wavelengths
// .35 (.62=158), .43 (.68=174), .525 (.75=190)
static const float3 kDefaultScatteringWavelength = float3(.65, .57, .475);
static const float3 kVariableRangeForScatteringWavelength = float3(.15, .15, .15);

#define OUTER_RADIUS 1.025
static const float kOuterRadius = OUTER_RADIUS;
static const float kOuterRadius2 = OUTER_RADIUS*OUTER_RADIUS;
static const float kInnerRadius = 1.0;
static const float kInnerRadius2 = 1.0;

static const float kCameraHeight = 0.000001;

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
*/

//////////////////////////////////////
//////////////////////////////////////

// RGB wavelengths
// .35 (.62=158), .43 (.68=174), .525 (.75=190)
float3 _kDefaultScatteringWavelength;
float3 _kVariableRangeForScatteringWavelength;
float _OUTER_RADIUS;
#define _kOuterRadius _OUTER_RADIUS
#define _kOuterRadius2 (_OUTER_RADIUS*_OUTER_RADIUS)
float _kInnerRadius;
float _kInnerRadius2;
float _kCameraHeight;
float _kRAYLEIGH_MAX;
float _kRAYLEIGH_POW;
#define kRAYLEIGH (lerp(0.0, _kRAYLEIGH_MAX, pow(_AtmosphereThickness, _kRAYLEIGH_POW)))      // Rayleigh constant
float _kMIE;             // Mie constant
float _kSUN_BRIGHTNESS;    // Sun brightness
float _kMAX_SCATTER; // Maximum scattering value, to prevent math overflows on Adrenos
half _kHDSundiskIntensityFactor;
half _kSimpleSundiskIntensityFactor;
half _kSunScale_Multiplier;
#define _kSunScale (_kSunScale_Multiplier * _kSUN_BRIGHTNESS)
#define _kKmESun (_kMIE * _kSUN_BRIGHTNESS)
#define Pi 3.14159265
float _kKm4PI_Multi;
#define _kKm4PI (_kMIE * _kKm4PI_Multi * Pi)
#define _kScale (1.0 / (_OUTER_RADIUS - 1.0))
float _kScaleDepth;
float _kScaleOverScaleDepth_Multi;
#define _kScaleOverScaleDepth ((1.0 / (_OUTER_RADIUS - 1.0)) / _kScaleOverScaleDepth_Multi)
float _kSamples; // THIS IS UNROLLED MANUALLY, DON'T TOUCH
float _MIE_G;
float _MIE_G2;
float _SKY_GROUND_THRESHOLD;

float scale(float _fInCos)
{
    float x = 1.0 - _fInCos;
    return 0.25f * exp(-0.00287f + x * (0.459f + x * (3.83f + x * (-6.80f + x * 5.25f))));
}

// Calculates the Rayleigh phase function
half getRayleighPhase(half _fEyeCos2)
{
    return 0.75 + 0.75 * _fEyeCos2;
}
half getRayleighPhase(half3 _vLight, half3 _vRay)
{
    half fEyeCos = dot(_vLight, _vRay);
    return getRayleighPhase(fEyeCos * fEyeCos);
}

// Calculates the Mie phase function
half getMiePhase(half _fEyeCos, half _fEyeCos2, float _fSunSize)
{
    half fTemp = 1.0f + _MIE_G2 - 2.0f * _MIE_G * _fEyeCos;
    fTemp = pow(fTemp, pow(_fSunSize, 0.65f) * 10.0f);
    fTemp = max(fTemp,1.0e-4); // prevent division by zero, esp. in half precision
    fTemp = 1.5f * ((1.0f - _MIE_G2) / (2.0f + _MIE_G2)) * (1.0f + _fEyeCos2) / fTemp;
    return fTemp;
}

// Calculates the sun shape
half calcSunAttenuation(half3 _vLightPos, half3 _vRay, float _fSunSize, float _fSunSizeConvergence)
{
    half fFocusedEyeCos = pow(saturate(dot(_vLightPos, _vRay)), _fSunSizeConvergence);
    return getMiePhase(-fFocusedEyeCos, fFocusedEyeCos * fFocusedEyeCos, _fSunSize);
}

#define PI 3.14159f

float3 LatlongToDirectionCoordinate(float2 coord)
{
    float theta = coord.y * PI;
    float phi = (coord.x * 2.f * PI - PI*0.5f);

    float cosTheta = cos(theta);
    float sinTheta = sqrt(1.0 - min(1.0, cosTheta*cosTheta));
    float cosPhi = cos(phi);
    float sinPhi = sin(phi);

    float3 direction = float3(sinTheta*cosPhi, cosTheta, sinTheta*sinPhi);
    direction.xy *= -1.0;
    return direction;
}

/////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////

float _SunSize;
float _SunSizeConvergence;
float _MoonSize;
float _MoonSizeConvergence;
float4 _SkyTint;
float _AtmosphereThickness;
float4 _GroundColor;
float _Exposure;
float4 _SunPos;
float4 _MoonPos;
float4 _LightDir;
float4 _SunCol;

float4 sk_frag(sk_v2f IN) : SV_Target
{
    float3 vSunPos = _SunPos.xyz;
    float3 vMoonPos = _MoonPos.xyz;
    const float3 vSkyTintInGammaSpace = COLOR_2_GAMMA(_SkyTint); // convert tint from Linear back to Gamma
    const float3 vScatteringWavelength = lerp ( _kDefaultScatteringWavelength - _kVariableRangeForScatteringWavelength,
                                                _kDefaultScatteringWavelength + _kVariableRangeForScatteringWavelength,
                                                half3(1,1,1) - vSkyTintInGammaSpace); // using Tint in sRGB gamma allows for more visually linear interpolation and to keep (.5) at (128, gray in sRGB) point
    const float3 vInvWavelength = 1.0 / pow(vScatteringWavelength, 4);

    const float fKrESun = kRAYLEIGH * _kSUN_BRIGHTNESS;
    const float fKr4PI = kRAYLEIGH * 4.0 * 3.14159265;

    // The camera's current position
    float3 vCameraPos = float3(0, _kInnerRadius + _kCameraHeight, 0);

    float fFar = 0.0;
    half3 vIn, vOut;

    // Get the ray from the camera to the fragment and its length (which is the far point of the ray passing through the atmosphere)
    float3 vEyeRay = normalize(IN.direction);
    if(vEyeRay.y >= -0.03) // Sky
    {
        float fUp = vEyeRay.y;
        // Calculate the length of the "atmosphere"
        fFar = sqrt(_kOuterRadius2 + _kInnerRadius2 * fUp * fUp - _kInnerRadius2) - _kInnerRadius * fUp;

        float3 vPos = vCameraPos + fFar * vEyeRay;

        // Calculate the ray's starting position, then calculate its scattering offset
        float fStartHeight = _kInnerRadius + _kCameraHeight;
        float fStartDepth = exp(_kScaleOverScaleDepth * (-_kCameraHeight));
        float fStartAngle = dot(vEyeRay, vCameraPos) / fStartHeight;
        float fStartOffset = fStartDepth * scale(fStartAngle);

        // Initialize the scattering loop variables
        float fSampleLength = fFar / _kSamples;
        float fScaledLength = fSampleLength * _kScale;
        float3 vSampleRay = vEyeRay * fSampleLength;
        float3 vSamplePoint = vCameraPos + vSampleRay * 0.5;

        // Now loop through the sample rays
        float3 vFrontColor = float3(0.0, 0.0, 0.0);
        // Weird workaround: WP8 and desktop FL_9_3 do not like the for loop here
        // (but an almost identical loop is perfectly fine in the ground calculations below)
        // Just unrolling this manually seems to make everything fine again.
        // for(int i=0; i<int(kSamples); i++)
        {
            float fHeight = length(vSamplePoint);
            float fDepth = exp(_kScaleOverScaleDepth * (_kInnerRadius - fHeight));
            float fLightAngle = dot(vSunPos.xyz, vSamplePoint) / fHeight;
            float fCameraAngle = dot(vEyeRay, vSamplePoint) / fHeight;
            float fScatter = (fStartOffset + fDepth * (scale(fLightAngle) - scale(fCameraAngle)));
            float3 vAttenuate = exp(-clamp(fScatter, 0.0, _kMAX_SCATTER) * (vInvWavelength * fKr4PI + _kKm4PI));

            float fDepthScaledByLength = fDepth * fScaledLength;
            vFrontColor += vAttenuate * fDepthScaledByLength;
            vSamplePoint += vSampleRay;
        }
        {
            float fHeight = length(vSamplePoint);
            float fDepth = exp(_kScaleOverScaleDepth * (_kInnerRadius - fHeight));
            float fLightAngle = dot(vSunPos.xyz, vSamplePoint) / fHeight;
            float fCameraAngle = dot(vEyeRay, vSamplePoint) / fHeight;
            float fScatter = (fStartOffset + fDepth * (scale(fLightAngle) - scale(fCameraAngle)));
            float3 vAttenuate = exp(-clamp(fScatter, 0.0, _kMAX_SCATTER) * (vInvWavelength * fKr4PI + _kKm4PI));

            float fDepthScaledByLength = fDepth * fScaledLength;
            vFrontColor += vAttenuate * fDepthScaledByLength;
            vSamplePoint += vSampleRay;
        }

        // Finally, scale the Mie and Rayleigh colors and set up the varying variables for the pixel shader
        vIn = vFrontColor * (vInvWavelength * fKrESun);
        vOut = vFrontColor * _kKmESun;
    }
    else // Ground
    {
        fFar = (-_kCameraHeight) / (min(-0.001, vEyeRay.y));

        float3 vPos = vCameraPos + fFar * vEyeRay;

        // Calculate the ray's starting position, then calculate its scattering offset
        float fStartDepth = exp((-_kCameraHeight) * (1.0/_kScaleDepth));
        float fCameraAngle = dot(-vEyeRay, vPos);
        float fLightAngle = dot(vSunPos.xyz, vPos);
        float fCameraScale = scale(fCameraAngle);
        float fLightScale = scale(fLightAngle);
        float fCameraOffset = fStartDepth*fCameraScale;
        float fTemp = (fLightScale + fCameraScale);

        // Initialize the scattering loop variables
        float fSampleLength = fFar / _kSamples;
        float fScaledLength = fSampleLength * _kScale;
        float3 vSampleRay = vEyeRay * fSampleLength;
        float3 vSamplePoint = vCameraPos + vSampleRay * 0.5;

        // Now loop through the sample rays
        float3 vFrontColor = float3(0.0, 0.0, 0.0);
        float3 vAttenuate;
        // for(int i=0; i<int(kSamples); i++) // Loop removed because we kept hitting SM2.0 temp variable limits. Doesn't affect the image too much.
        {
            float fHeight = length(vSamplePoint);
            float fDepth = exp(_kScaleOverScaleDepth * (_kInnerRadius - fHeight));
            float fScatter = fDepth*fTemp - fCameraOffset;
            vAttenuate = exp(-clamp(fScatter, 0.0, _kMAX_SCATTER) * (vInvWavelength * fKr4PI + _kKm4PI));
            vFrontColor += vAttenuate * (fDepth * fScaledLength);
            vSamplePoint += vSampleRay;
        }

        vIn = vFrontColor * (vInvWavelength * fKrESun + fKrESun);
        vOut = clamp(vAttenuate, 0.0, 1.0);
    }

    // if we want to calculate color in vprog:
    // 1. in case of linear: multiply by _Exposure in here (even in case of lerp it will be common multiplier, so we can skip mul in fshader)
    // 2. in case of gamma and SKYBOX_COLOR_IN_TARGET_COLOR_SPACE: do sqrt right away instead of doing that in fshader
    //float3 vGroundColor = _Exposure * (vIn + COLOR_2_LINEAR(_GroundColor) * vOut);
    float3 vGroundColor = _Exposure * (vIn + (_GroundColor) * vOut);
    float3 vSkyColor    = _Exposure * (vIn * getRayleighPhase(vSunPos.xyz, -vEyeRay));
    
    // Below code mixes in some purple to the space atmosphere
    // float interp = pow(1.0f - ((vIn.r + vIn.b + vIn.g) / 3.0f), 10.0f);
    // vSkyColor = lerp(vSkyColor, float3(0.06, 0.02, 0.14), float3(interp, interp, interp));

    // The sun should have a stable intensity in its course in the sky. Moreover it should match the highlight of a purely specular material.
    // This matching was done using the standard shader BRDF1 on the 5/31/2017
    // Finally we want the sun to be always bright even in LDR thus the normalization of the lightColor for low intensity.
    float3 vSunColour = _SunCol.xyz;
    half fLightColorIntensity = clamp(length(vSunColour), 0.25, 1);
    float3 vSunColour_Intensity = _kHDSundiskIntensityFactor * saturate(vOut) * vSunColour / fLightColorIntensity;
    
    half y = vEyeRay.y / _SKY_GROUND_THRESHOLD;
    half3 vCol = lerp(vSkyColor, vGroundColor, saturate(-y));

    if(y > -0.03)
    {
        vCol += vSunColour_Intensity * calcSunAttenuation(vSunPos.xyz, vEyeRay, _SunSize, _SunSizeConvergence);
        vCol += calcSunAttenuation(vMoonPos.xyz, vEyeRay, _MoonSize, _MoonSizeConvergence);
    }

    vCol = COLOR_2_LINEAR(vCol);
    
    float fAlpha = pow(vCol.r + vCol.g + vCol.b, 0.8f);
    return half4(vCol, fAlpha);
}

#endif // DCL_SKYBOX_FRAGMENT_INCLUDED