Shader "Custom/ShadowCatcher"
{
    Properties
    {
        _ShadowColor ("Shadow Color", Color) = (0, 0, 0, 1)
        _Strength ("Strength", Range(0, 1)) = 0.55
        _EdgeFadeStart ("Edge Fade Start", Range(0, 1)) = 0.6
        _EdgeFadeEnd ("Edge Fade End", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "ShadowCatcher"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            ZWrite Off
            ZTest LEqual
            Cull Back

            // Color blends as straight alpha over whatever is behind, so a black shadow
            // multiplies the background down. Alpha needs its own blend: with a single
            // SrcAlpha/OneMinusSrcAlpha the destination alpha would end up as strength
            // squared, which reads as a much weaker shadow once the canvas is composited
            // over the page (the background is fully transparent, so there is no existing
            // alpha to blend into).
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            // The catcher is transparent, so URP's screen space shadow path does not apply
            // to it and it has to sample the shadow map directly. Defined before including
            // Shadows.hlsl, which branches on it.
            #define _SURFACE_TYPE_TRANSPARENT 1

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShadowColor;
                half _Strength;
                half _EdgeFadeStart;
                half _EdgeFadeEnd;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = positionInputs.positionCS;
                OUT.positionWS = positionInputs.positionWS;
                OUT.uv = IN.uv;

                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                half attenuation = MainLightRealtimeShadow(TransformWorldToShadowCoord(IN.positionWS));

                // Past the shadow distance the shadow map has nothing to say, so lift the
                // attenuation back to lit instead of leaving a hard cut across the plane.
                attenuation = lerp(attenuation, 1.0h, GetMainLightShadowFade(IN.positionWS));

                // Radial fade in UV space so the plane's own border never shows up as an edge.
                half fromCenter = length(half2(IN.uv * 2.0h - 1.0h));
                half edge = 1.0h - smoothstep(_EdgeFadeStart, _EdgeFadeEnd, fromCenter);

                half shadow = (1.0h - attenuation) * _Strength * _ShadowColor.a * edge;

                return half4(_ShadowColor.rgb, shadow);
            }
            ENDHLSL
        }
    }
}
