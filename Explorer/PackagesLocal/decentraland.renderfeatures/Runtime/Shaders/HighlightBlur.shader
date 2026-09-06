// Separable Gaussian blur for RenderFeature_ObjectHighlight (blur material).
//
// Pass 0 = horizontal, Pass 1 = vertical. Reads the premultiplied silhouette
// mask from _HighlightBlurSrc and spreads it by _BlurRadius texels, producing
// the glow that becomes the outline. Operates on intermediate RTs only, so the
// self-generated fullscreen triangle needs no platform Y-flip handling (a
// symmetric kernel is orientation-invariant anyway).
//
// The source binding is its own name (not the composite's _HighlightMaskTex) so
// chaining H -> V cannot leave the composite pass reading a half-blurred mask.
// _BlurRadius is deliberately NOT a shader property: a declared property is
// backed by the material's own value, which takes precedence over the global the
// blur passes set and would pin the glow to one width.
Shader "Hidden/DCL/RenderFeatures/HighlightBlur"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_HighlightBlurSrc);
        SAMPLER(sampler_HighlightBlurSrc);
        float4 _HighlightBlurSrc_TexelSize;
        float _BlurRadius;

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

        half4 GaussianBlur(float2 uv, float2 dir)
        {
            float2 step = _HighlightBlurSrc_TexelSize.xy * dir * max(_BlurRadius, 0.0);

            // 9-tap normalized Gaussian weights.
            const float w0 = 0.2270270270;
            const float w1 = 0.1945945946;
            const float w2 = 0.1216216216;
            const float w3 = 0.0540540541;
            const float w4 = 0.0162162162;

            half4 c = SAMPLE_TEXTURE2D(_HighlightBlurSrc, sampler_HighlightBlurSrc, uv) * w0;
            c += SAMPLE_TEXTURE2D(_HighlightBlurSrc, sampler_HighlightBlurSrc, uv + step * 1.0) * w1;
            c += SAMPLE_TEXTURE2D(_HighlightBlurSrc, sampler_HighlightBlurSrc, uv - step * 1.0) * w1;
            c += SAMPLE_TEXTURE2D(_HighlightBlurSrc, sampler_HighlightBlurSrc, uv + step * 2.0) * w2;
            c += SAMPLE_TEXTURE2D(_HighlightBlurSrc, sampler_HighlightBlurSrc, uv - step * 2.0) * w2;
            c += SAMPLE_TEXTURE2D(_HighlightBlurSrc, sampler_HighlightBlurSrc, uv + step * 3.0) * w3;
            c += SAMPLE_TEXTURE2D(_HighlightBlurSrc, sampler_HighlightBlurSrc, uv - step * 3.0) * w3;
            c += SAMPLE_TEXTURE2D(_HighlightBlurSrc, sampler_HighlightBlurSrc, uv + step * 4.0) * w4;
            c += SAMPLE_TEXTURE2D(_HighlightBlurSrc, sampler_HighlightBlurSrc, uv - step * 4.0) * w4;
            return c;
        }
        ENDHLSL

        Pass
        {
            Name "HighlightBlurHorizontal"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            half4 frag(Varyings i) : SV_Target { return GaussianBlur(i.uv, float2(1.0, 0.0)); }
            ENDHLSL
        }

        Pass
        {
            Name "HighlightBlurVertical"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            half4 frag(Varyings i) : SV_Target { return GaussianBlur(i.uv, float2(0.0, 1.0)); }
            ENDHLSL
        }
    }
    Fallback Off
}
