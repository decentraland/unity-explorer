// Directional caustics for RendererFeature_Ocean (directionalCaustics toggle).
//
// Fullscreen additive pass: reconstructs each opaque pixel's world position
// from the scene depth texture and, for surfaces below the sea level, adds an
// animated caustics pattern projected along the main light direction, fading
// with distance beneath the surface. Skybox pixels contribute nothing.
Shader "Hidden/DCL/RenderFeatures/OceanCaustics"
{
    Properties
    {
        _SeaLevel ("Sea Level (world Y)", Float) = 0
        _CausticsScale ("Scale", Float) = 0.35
        _CausticsSpeed ("Speed", Float) = 0.6
        _CausticsIntensity ("Intensity", Float) = 0.6
        _CausticsFade ("Depth Fade", Float) = 6
        [HDR] _CausticsColor ("Color", Color) = (0.6, 0.9, 1.0, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "OceanCaustics"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend One One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float _SeaLevel;
            float _CausticsScale;
            float _CausticsSpeed;
            float _CausticsIntensity;
            float _CausticsFade;
            float4 _CausticsColor;

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

            float Caustic(float2 uv, float t)
            {
                float c = sin(uv.x + t) * sin(uv.y - t)
                        + sin(uv.x * 1.7 - t * 1.3) * sin(uv.y * 1.3 + t * 0.7);
                c = saturate(c * 0.5 + 0.5);
                return pow(c, 3.0);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float rawDepth = SampleSceneDepth(input.uv);

                #if UNITY_REVERSED_Z
                    bool isSky = rawDepth <= 1e-6;
                #else
                    bool isSky = rawDepth >= 1.0 - 1e-6;
                #endif
                if (isSky)
                    return half4(0, 0, 0, 0);

                float3 worldPos = ComputeWorldSpacePosition(input.uv, rawDepth, UNITY_MATRIX_I_VP);

                float belowSurface = _SeaLevel - worldPos.y;
                if (belowSurface <= 0.0)
                    return half4(0, 0, 0, 0);

                float t = _Time.y * _CausticsSpeed;
                // Project along the light direction so caustics ripple with the sun.
                Light mainLight = GetMainLight();
                float2 flow = mainLight.direction.xz * t;
                float2 uv = worldPos.xz * _CausticsScale + flow;

                float caustic = Caustic(uv, t);
                float fade = exp(-belowSurface / max(_CausticsFade, 0.01));

                half3 col = _CausticsColor.rgb * mainLight.color * caustic * fade * _CausticsIntensity;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
