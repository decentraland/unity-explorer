#ifndef DCL_STARBOX_FRAGMENT_INCLUDED
#define DCL_STARBOX_FRAGMENT_INCLUDED

// Includes
#include "./DCL_SkyBox_Data.hlsl"

#define DCL_DECLARE_TEX2DARRAY(tex) Texture2DArray tex; SamplerState sampler##tex
#define DCL_SAMPLE_TEX2DARRAY(tex,coord) tex.Sample (sampler##tex,coord)

DCL_DECLARE_TEX2DARRAY(_CubemapTextureArray);
#define SAMPLE_CUBEMAP_ARRAY(uv, texArrayID)                  DCL_SAMPLE_TEX2DARRAY(_CubemapTextureArray, float3(uv, texArrayID))

/////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////


float4 st_frag(sk_v2f IN) : SV_Target
{
    float3 ArrayColour = float3(0.0, 0.0, 0.0);
    #if defined(_CUBEMAP_FACE_RIGHT)
    ArrayColour = SAMPLE_CUBEMAP_ARRAY(IN.localTexcoord.xy, 0);
    #elif defined(_CUBEMAP_FACE_LEFT)
    ArrayColour = SAMPLE_CUBEMAP_ARRAY(IN.localTexcoord.xy, 1);
    #elif defined(_CUBEMAP_FACE_UP)
    ArrayColour = SAMPLE_CUBEMAP_ARRAY(IN.localTexcoord.xy, 2);
    #elif defined(_CUBEMAP_FACE_DOWN)
    ArrayColour = SAMPLE_CUBEMAP_ARRAY(IN.localTexcoord.xy, 3);
    #elif defined(_CUBEMAP_FACE_FRONT)
    ArrayColour = SAMPLE_CUBEMAP_ARRAY(IN.localTexcoord.xy, 4);
    #elif defined(_CUBEMAP_FACE_BACK)
    ArrayColour = SAMPLE_CUBEMAP_ARRAY(IN.localTexcoord.xy, 5);
    #else
    return float3(0, 0, 0);
    #endif

    return half4(ArrayColour.rgb, 1.0);
}

#endif // DCL_STARBOX_FRAGMENT_INCLUDED