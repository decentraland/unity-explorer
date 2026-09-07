// Outline/glow composite for RenderFeature_ObjectHighlight (output material).
//
// Reads the blurred glow (_HighlightBlurTex, premultiplied) and the sharp
// silhouette coverage (_HighlightMaskTex.a) and additively blends a coloured
// rim over the frame. The ring is (glowCoverage - sharpCoverage): the blurred
// halo minus the solid interior, which leaves a soft outline hugging the
// silhouette. A faint interior fill is added so the highlighted object also
// reads as "lit".
Shader "Hidden/DCL/RenderFeatures/HighlightComposite"
{
    Properties
    {
        _HighlightGlowStrength ("Glow Strength", Float) = 1.4
        _HighlightFillStrength ("Interior Fill", Float) = 0.15
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "HighlightComposite"
            Tags { "LightMode" = "DCL_HighlightComposite" }

            ZWrite Off
            ZTest Always
            Cull Off
            // Additive in RGB only: the fragment's alpha is a constant 1 and an
            // additive alpha channel would drive destination alpha to 1 across the
            // whole screen, not just under the rim.
            Blend One One, Zero One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_HighlightBlurTex);
            SAMPLER(sampler_HighlightBlurTex);
            TEXTURE2D(_HighlightMaskTex);
            SAMPLER(sampler_HighlightMaskTex);
            float _HighlightGlowStrength;
            float _HighlightFillStrength;

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;
                float2 p = float2((vertexID << 1) & 2, vertexID & 2);
                o.uv = p;
                o.positionHCS = float4(p * 2.0 - 1.0, 0.0, 1.0);
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 blur = SAMPLE_TEXTURE2D(_HighlightBlurTex, sampler_HighlightBlurTex, input.uv);
                half sharp = SAMPLE_TEXTURE2D(_HighlightMaskTex, sampler_HighlightMaskTex, input.uv).a;

                half glowA = blur.a;
                // Recover the pure tint from the premultiplied glow.
                half3 tint = glowA > 1e-4h ? blur.rgb / glowA : half3(0.0h, 0.0h, 0.0h);

                half ring = saturate(glowA - sharp);
                half3 outCol = tint * (ring * _HighlightGlowStrength + sharp * _HighlightFillStrength);
                return half4(outCol, 1.0h);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
