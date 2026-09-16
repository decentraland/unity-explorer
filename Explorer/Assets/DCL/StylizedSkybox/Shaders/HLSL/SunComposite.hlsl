#ifndef DCL_SUN_COMPOSITE_INCLUDED
#define DCL_SUN_COMPOSITE_INCLUDED

#include "Assets/DCL/StylizedSkybox/Shaders/HLSL/SkyboxGlobals.hlsl"

// Dims the disc and halo as the active body approaches the horizon, the way the Unreal celestial material fades its
// quad by height: full above `height` (sine of elevation), down to `floor` at and below the horizon. A height of 0
// means off, so the legacy look is untouched.
float SunComposite_HorizonDarkening()
{
    float height = _DclCelestialParams.x;
    if (height <= 0.0) return 1.0;

    return lerp(_DclCelestialParams.y, 1.0, saturate(_DclSunDirection.y / height));
}

// Composites the sun/moon layer (disc + halo) over the sky. Replaces the Screen Blend node in the Sun & Moon group.
// Legacy path (UseLut == 0) is Shader Graph's Blend node in Screen mode, reproduced exactly.
// Lookup path (UseLut == 1) is additive: Screen goes negative when both inputs exceed 1, which happens when the HDR
// disc sits on the HDR horizon band and turns the disc cyan.
void SunComposite_float(float4 Base, float4 Blend, float Opacity, float UseLut, out float4 Out)
{
    Blend *= SunComposite_HorizonDarkening();

    float4 screen = 1.0 - (1.0 - Blend) * (1.0 - Base);
    float4 additive = Base + Blend;
    float4 result = UseLut > 0.5 ? additive : screen;
    Out = lerp(Base, result, Opacity);
}

#endif
