// Procedural ground mist for a flat quad: two layers of scrolling value noise give a slow drift, a soft elliptical mask hides
// the quad edges, and the result is alpha-blended after the floor so it always sits on top of it.
Shader "DCL/Lobby/StageMist"
{
    Properties
    {
        _Color ("Color", Color) = (0.6, 0.5, 0.8, 0.35)
        _Scale ("Noise Scale", Float) = 3
        _Speed ("Drift (xy layer 1, zw layer 2)", Vector) = (0.02, 0.01, -0.015, 0.02)
        _Threshold ("Density Threshold", Range(0, 1)) = 0.35
        _Softness ("Density Softness", Range(0.01, 1)) = 0.4
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+2"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        // Alpha composites as coverage (src + dst * (1 - src)) so drawing over an opaque pixel keeps it opaque
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite Off
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
                float _Scale;
                float4 _Speed;
                float _Threshold;
                float _Softness;
                float _EdgeSoftness;
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

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;

                for (int i = 0; i < 3; i++)
                {
                    value += ValueNoise(p) * amplitude;
                    p = p * 2.1 + 17.0;
                    amplitude *= 0.5;
                }

                return value;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float time = _Time.y;
                float2 p = input.uv * _Scale;

                float layer1 = Fbm(p + (_Speed.xy * time));
                float layer2 = Fbm((p * 1.7) + (_Speed.zw * time) + 31.0);
                float density = smoothstep(_Threshold, _Threshold + _Softness, (layer1 * 0.6) + (layer2 * 0.4));

                // Elliptical soft mask so the quad border never shows
                float2 centred = (input.uv - 0.5) * 2.0;
                float edge = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, length(centred));

                return half4(_Color.rgb, _Color.a * density * edge);
            }
            ENDHLSL
        }
    }
}
