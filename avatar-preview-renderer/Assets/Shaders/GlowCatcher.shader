Shader "Custom/GlowCatcher"
{
    Properties
    {
        // Very slightly cyan white, matching the pool the Shop used to paint in CSS. The strength is
        // deliberately a fifth of the 0.15 that CSS used - the same alpha reads far heavier on a
        // foreshortened disc under the feet than it did flat across the panel.
        _GlowColor ("Glow Color", Color) = (0.9255, 0.9725, 0.9765, 1)
        _Strength ("Strength", Range(0, 1)) = 0.03
        _MidPoint ("Mid Stop", Range(0, 1)) = 0.45
        _MidStrength ("Mid Strength", Range(0, 1)) = 0.05
        _EdgeFade ("Edge Fade", Range(0, 1)) = 0.78
    }

    SubShader
    {
        Tags
        {
            // Ahead of the shadow catcher's plain Transparent, so the shadow lands ON the lit floor
            // rather than under it. Both write no depth, so the queue is the only thing ordering them.
            "RenderType"="Transparent" "Queue"="Transparent-1" "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "GlowCatcher"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            ZWrite Off
            ZTest LEqual
            Cull Back

            // The mirror image of the shadow catcher. Straight alpha over a transparent canvas puts
            // white into the colour channels, so once the browser composites the canvas the page ends
            // up at page + a * (white - page) - brighter, and identical to what the CSS radial gradient
            // was doing. Alpha still needs its own One/OneMinusSrcAlpha blend for the same reason it
            // does there: with SrcAlpha the destination alpha would come out as strength squared.
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _GlowColor;
                half _Strength;
                half _MidPoint;
                half _MidStrength;
                half _EdgeFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;

                OUT.positionCS = GetVertexPositionInputs(IN.positionOS.xyz).positionCS;
                OUT.uv = IN.uv;

                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                half fromCenter = length(half2(IN.uv * 2.0h - 1.0h));

                // Two straight ramps meeting at _MidPoint, which is what a three stop CSS radial
                // gradient is. Keeping the shape rather than swapping in a smoothstep means the dials
                // carry the values the Shop was already tuned to.
                half mid = _Strength * _MidStrength;
                half inner = lerp(_Strength, mid, saturate(fromCenter / max(_MidPoint, HALF_MIN)));
                half outer = lerp(mid, 0.0h, saturate((fromCenter - _MidPoint) / max(_EdgeFade - _MidPoint, HALF_MIN)));

                half glow = fromCenter < _MidPoint ? inner : outer;

                return half4(_GlowColor.rgb, glow * _GlowColor.a);
            }
            ENDHLSL
        }
    }
}
