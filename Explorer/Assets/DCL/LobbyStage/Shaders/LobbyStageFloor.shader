// Lit stage floor: tiled base map with Lambert lighting from the main and additional lights and a soft spot around the stage
// centre. It stays solid near the avatar and dissolves to transparent along a world-space band (set by LobbyStage from the
// wanted screen height), so the backdrop behind shows through.
Shader "DCL/Lobby/StageFloor"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (0.55, 0.55, 0.58, 1)
        _SpotColor ("Spot Color (added at centre)", Color) = (0.15, 0.13, 0.2, 1)
        _SpotRadius ("Spot Radius", Float) = 2.5
        _SpotSoftness ("Spot Softness", Float) = 3
        _BlendAxis ("Blend Axis (world XZ direction)", Vector) = (0, 0, -1, 0)
        _BlendStart ("Blend Start (along axis)", Float) = 3
        _BlendEnd ("Blend End (along axis)", Float) = 7
        _BlendHardness ("Blend Hardness", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _SpotColor;
                float _SpotRadius;
                float _SpotSoftness;
                float4 _BlendAxis;
                float _BlendStart;
                float _BlendEnd;
                float _BlendHardness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float2 originXZ : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.originXZ = TransformObjectToWorld(float3(0, 0, 0)).xz;
                return output;
            }

            half3 Lambert(Light light, half3 normalWS)
            {
                return light.color * (light.distanceAttenuation * light.shadowAttenuation * saturate(dot(normalWS, light.direction)));
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 normalWS = normalize(input.normalWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 lighting = SampleSH(normalWS) + Lambert(mainLight, normalWS);

                #if defined(_ADDITIONAL_LIGHTS)
                    #if USE_CLUSTER_LIGHT_LOOP
                        for (uint directionalIndex = 0; directionalIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); directionalIndex++)
                            lighting += Lambert(GetAdditionalLight(directionalIndex, input.positionWS), normalWS);
                    #endif

                    uint pixelLightCount = GetAdditionalLightsCount();

                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        lighting += Lambert(GetAdditionalLight(lightIndex, input.positionWS), normalWS);
                    LIGHT_LOOP_END
                #endif

                float radial = length(input.positionWS.xz - input.originXZ);
                half spot = 1.0 - smoothstep(_SpotRadius, _SpotRadius + _SpotSoftness, radial);
                half3 color = albedo.rgb * lighting + _SpotColor.rgb * spot;

                float alongAxis = dot(input.positionWS, _BlendAxis.xyz);
                half blend = smoothstep(_BlendStart, _BlendEnd, alongAxis);
                // Hardness squeezes the gradient towards a step at the middle of the band
                blend = saturate(((blend - 0.5) / max(1.0 - _BlendHardness, 0.001)) + 0.5);
                blend = 1.0 - blend;

                return half4(color, albedo.a * blend);
            }
            ENDHLSL
        }
    }
}
