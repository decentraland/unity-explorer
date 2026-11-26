// Unlit, opaque backdrop image on an upright quad. The floor in front of it does the blending, so the image is untouched,
// except where the window mask lets snow fall over the view outside; zero snow alphas leave the image as it is.
Shader "DCL/Lobby/StageBackdrop"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _WindowMask ("Window Mask", 2D) = "black" {}
        _SnowMap ("Snow", 2D) = "black" {}
        _SnowColor ("Snow Centre Color (alpha = strength)", Color) = (1, 1, 1, 0)
        _SnowGlowColor ("Snow Glow Color (alpha = strength)", Color) = (0.55, 0.75, 1, 0)
        _SnowParams ("Snow (speed, tiling, image aspect, wind)", Vector) = (0.02, 1.5, 1.78, 0.01)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_WindowMask);
            SAMPLER(sampler_WindowMask);
            TEXTURE2D(_SnowMap);
            SAMPLER(sampler_SnowMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _SnowColor;
                half4 _SnowGlowColor;
                float4 _SnowParams;
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

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            // One layer of the tiled snow (R = flake centre, G = glow around it), falling at speed image heights per second and
            // drifting with the wind. The sway changes slowly across the image, so each flake moves whole instead of being sheared
            // out of shape; scale > 1 gives smaller, farther flakes
            half2 SnowLayer(float2 snowUV, float scale, float speed, float phase)
            {
                float t = _Time.y;
                float tiling = _SnowParams.y * scale;
                float2 uv = snowUV * scale;
                // Distance travelled at a wind speed of (1 + 0.3 sin(0.35 t)): gusts speed it up and slow it down without ever growing with time
                float drift = t - ((0.3 / 0.35) * cos((t * 0.35) + phase));
                uv.x -= drift * _SnowParams.w * tiling;
                uv.x += sin((t * 0.5) + (uv.y * 0.35) + phase) * 0.06;
                uv.y += t * speed * tiling;
                return SAMPLE_TEXTURE2D(_SnowMap, sampler_SnowMap, uv + phase).rg;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                half mask = SAMPLE_TEXTURE2D(_WindowMask, sampler_WindowMask, input.uv).r;
                float2 snowUV = float2(input.uv.x * _SnowParams.z, input.uv.y) * _SnowParams.y;
                half2 snow = saturate((SnowLayer(snowUV, 1.6, _SnowParams.x * 0.6, 0.37) * 0.6) + SnowLayer(snowUV, 1.0, _SnowParams.x, 0.0)) * mask;

                // The glow tints what is behind the flake, then the centre lays the core colour on top
                color.rgb = lerp(color.rgb, _SnowGlowColor.rgb, snow.y * _SnowGlowColor.a);
                color.rgb = lerp(color.rgb, _SnowColor.rgb, snow.x * _SnowColor.a);
                return half4(color.rgb, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half frag(Varyings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
}
