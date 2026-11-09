#ifndef DCL_SUN_COMPOSITE_INCLUDED
#define DCL_SUN_COMPOSITE_INCLUDED

// Composites the sun/moon layer (disc + halo) over the sky. Replaces the Screen Blend node in the Sun & Moon group.
// Legacy variant (no _DCL_SKY_STYLIZED keyword) is Shader Graph's Blend node in Screen mode, reproduced exactly.
// Stylized variant is additive: Screen goes negative when both inputs exceed 1, which happens when the HDR disc sits
// on the HDR horizon band and turns the disc cyan. UseLut is kept for the graph wiring but the keyword decides.
void SunComposite_float(float4 Base, float4 Blend, float Opacity, float UseLut, out float4 Out)
{
#ifdef _DCL_SKY_STYLIZED
    float4 result = Base + Blend;
#else
    float4 result = 1.0 - (1.0 - Blend) * (1.0 - Base);
#endif
    Out = lerp(Base, result, Opacity);
}

#endif
