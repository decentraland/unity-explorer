// Wave displacement pre-pass for RendererFeature_Ocean
// (displacementPrePassSettings toggle).
//
// Renders a wide-area low-frequency swell heightfield for a square world region
// into a single-channel RT the water vertex shader samples
// (global _OceanDisplacementTex). This lets the ocean carry a large rolling
// swell across many tiles on top of each tile's local Gerstner detail, at a
// resolution independent of mesh tessellation. The C# feature sets the region
// (_OceanDispCenter/_OceanDispSize) from range/cellSize.
Shader "Hidden/DCL/RenderFeatures/OceanDisplacement"
{
    Properties
    {
        _OceanDispCenter ("Region Center XZ", Vector) = (0,0,0,0)
        _OceanDispSize ("Region Size (world)", Float) = 500
        _OceanDispHeight ("Swell Height", Float) = 0.6
        _OceanDispFreq ("Swell Frequency", Float) = 0.03
        _OceanDispSpeed ("Swell Speed", Float) = 0.4
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "OceanDisplacement"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _OceanDispCenter;
            float _OceanDispSize;
            float _OceanDispHeight;
            float _OceanDispFreq;
            float _OceanDispSpeed;

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
                // uv [0,1] → world XZ across the region.
                float2 worldXZ = _OceanDispCenter.xy + (input.uv - 0.5) * _OceanDispSize;
                float t = _Time.y * _OceanDispSpeed;

                // A couple of long crossing swells → smooth rolling height.
                float2 d1 = normalize(float2(1.0, 0.35));
                float2 d2 = normalize(float2(-0.4, 1.0));
                float h = sin(dot(worldXZ, d1) * _OceanDispFreq + t)
                        + 0.6 * sin(dot(worldXZ, d2) * _OceanDispFreq * 1.7 - t * 1.2);
                h *= _OceanDispHeight * 0.5;

                return half4(h, 0, 0, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
