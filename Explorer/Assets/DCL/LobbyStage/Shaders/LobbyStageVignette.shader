// Screen-space vignette for the lobby stage. The vertex stage writes clip-space positions directly, so the quad covers the whole
// frame of whatever camera draws it and nothing has to be fitted to a lens; only its bounds still take part in culling.
Shader "DCL/Lobby/StageVignette"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 1)
        _Intensity ("Intensity", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0.05, 1)) = 0.6
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Overlay"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity;
                float _Smoothness;
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

            // 1 / distance from the frame's centre to one of its corners, so the ramp reaches 1 exactly there
            #define INV_CORNER_DISTANCE 0.70710678

            Varyings vert(Attributes input)
            {
                Varyings output;

                // The built-in quad spans -0.5..0.5, so doubling it fills clip space. Z sits inside the valid depth range of
                // every platform convention; the pass ignores depth anyway.
                output.positionCS = float4(input.positionOS.xy * 2.0, 0.0, 1.0);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 0 at the centre of the frame, 1 at its corners; the ellipse follows the frame instead of staying circular
                float2 centred = (input.uv - 0.5) * 2.0;
                float distanceFromCentre = length(centred) * INV_CORNER_DISTANCE;

                float darkening = smoothstep(1.0 - _Smoothness, 1.0, distanceFromCentre);

                return half4(_Color.rgb, _Color.a * _Intensity * darkening);
            }
            ENDHLSL
        }
    }
}
