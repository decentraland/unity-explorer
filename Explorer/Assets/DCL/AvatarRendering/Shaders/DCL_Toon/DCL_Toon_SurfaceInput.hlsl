#ifndef TOON_INPUT_SURFACE_INCLUDED
#define TOON_INPUT_SURFACE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

///////////////////////////////////////////////////////////////////////////////
//                      Material Property Helpers                            //
///////////////////////////////////////////////////////////////////////////////
half Alpha(half albedoAlpha, half4 color, half cutoff)
{
    #if !defined(_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A) && !defined(_GLOSSINESS_FROM_BASE_ALPHA)
        half alpha = albedoAlpha * color.a;
    #else
        half alpha = color.a;
    #endif

    alpha = AlphaDiscard(alpha, cutoff);

    return alpha;
}

half4 SampleAlbedoAlpha(float2 uv)
{
    int nBaseMapArrID = _BaseMapArr_ID;
    return half4(SAMPLE_BASEMAP(uv,nBaseMapArrID));
}

half3 SampleNormal(float2 uv, half scale = half(1.0))
{
    #ifdef _NORMALMAP
        int nBumpMapArrID = _BumpMapArr_ID;
        half4 n = SAMPLE_BUMPMAP(uv, nBumpMapArrID);
        #if BUMP_SCALE_NOT_SUPPORTED
            return UnpackNormal(n);
        #else
            return UnpackNormalScale(n, scale);
        #endif
    #else
        return half3(0.0h, 0.0h, 1.0h);
    #endif
}

half3 SampleEmission(float2 uv, half3 emissionColor)
{
    #ifndef _EMISSION
        return 0;
    #else
        int nEmissionMapArrID = _EmissionMapArr_ID;
        return SAMPLE_EMISSIONMAP(uv,nEmissionMapArrID).rgb * emissionColor;
    #endif
}

#endif
