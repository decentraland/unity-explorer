// Billboard dust mote: a procedural soft disc tinted by the particle colour, so no texture is needed.
Shader "DCL/Lobby/StageDust"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 0.96, 0.9, 1)
        _Softness ("Edge Softness", Range(0.01, 1)) = 0.6
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+3"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Blend SrcAlpha OneMinusSrcAlpha
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
                float _Softness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float distanceFromCentre = length(input.uv - 0.5) * 2.0;
                half disc = 1.0 - smoothstep(1.0 - _Softness, 1.0, distanceFromCentre);
                half4 color = input.color * _Color;
                return half4(color.rgb, color.a * disc);
            }
            ENDHLSL
        }
    }
}
