// Screen-space planar reflection for RendererFeature_Ocean
// (screenSpaceReflectionSettings toggle).
//
// Fullscreen pass that produces a reflection texture the water shader samples
// (global _OceanScreenReflectionTex). It mirrors the opaque scene colour about
// a screen-space horizon line (_ReflectionHorizon) — a cheap planar reflection
// of everything above the water — and returns a coverage alpha that fades as
// the mirrored sample approaches the horizon. Written to an offscreen RT; the
// water shader blends it against the environment probe by Fresnel.
Shader "Hidden/DCL/RenderFeatures/OceanSSR"
{
    Properties
    {
        _ReflectionHorizon ("Screen Horizon (uv.y)", Float) = 0.5
        _ReflectionFalloff ("Falloff", Float) = 1.5
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "OceanSSR"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            float _ReflectionHorizon;
            float _ReflectionFalloff;

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;
                o.positionHCS = GetFullScreenTriangleVertexPosition(vertexID);
                o.uv = GetFullScreenTriangleTexCoord(vertexID);
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Mirror about the horizon line: a water pixel below the horizon
                // reflects the scene an equal distance above it.
                float mirroredY = 2.0 * _ReflectionHorizon - input.uv.y;
                float2 mUV = float2(input.uv.x, mirroredY);

                if (mUV.y < 0.0 || mUV.y > 1.0)
                    return half4(0, 0, 0, 0);

                half3 col = SampleSceneColor(mUV);
                // Fade coverage as the reflected sample nears the horizon (grazing).
                float cover = saturate(pow(saturate((_ReflectionHorizon - input.uv.y) * _ReflectionFalloff), 0.5));
                return half4(col, cover);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
