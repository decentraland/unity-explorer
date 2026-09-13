// Decentraland / Rock (GPUI)
//
// URP-lit stylized rock shader for the GPU-instanced landscape stone prototypes
// (Rock0*_GPUIPro materials). Every pass carries
//   #pragma instancing_options procedural:setupGPUI
// so the per-instance world matrix comes from TreeRendererService's
// gpuiTransformBuffer when drawn through Graphics.RenderMeshPrimitives, while
// plain (non-instanced) renderers still work through the regular path.
//
// Surface model (property names match the slots the Rock0*_GPUIPro materials bind):
//   * _BaseColor is the per-rock baked albedo texture, tinted by _Color.
//   * _TerrainRender is the terrain base colour, sampled by absolute world XZ
//     (scaled by _Tiling, shifted by _Offset) and blended into the rock albedo
//     by absolute world height so the rock's foot takes the ground's tint.
//   * _Moss replaces the albedo on up-facing world vertex normals, _MossMask
//     sets how far down the sides it reaches.
//   * _Normal_Map is the per-rock tangent-space normal map, scaled by _BumpScale.
//   * Lighting: URP PBR (metallic 0, smoothness _Smoothness) through
//     UniversalFragmentPBR, so the Forward+ cluster light loop, main-light
//     shadows, light layers, SH ambient and fog all follow the pipeline.

Shader "DCL/Rock_GPUI"
{
    Properties
    {
        [MainTexture] _BaseColor ("Base Map (Albedo)", 2D)       = "white" {}
                      _Color     ("Tint", Color)                 = (1, 1, 1, 1)

        [NoScaleOffset] _Normal_Map ("Normal Map", 2D)           = "bump" {}
                        _BumpScale  ("Normal Scale", Float)      = 1

        _Moss     ("Moss Color", Color)                          = (0.043, 0.427, 0.302, 0)
        _MossMask ("Moss Amount", Range(0, 1))                   = 0.9

        [NoScaleOffset] _TerrainRender ("Terrain Base Color", 2D) = "white" {}
                        _Tiling        ("Terrain Tiling", Float)  = 1
                        _Offset        ("Terrain Offset", Float)  = 0

        _Smoothness ("Smoothness", Range(0, 1))                  = 0.2

        [HideInInspector] _SpecColor  ("Specular Color", Color)  = (0.2, 0.2, 0.2, 1)
        [HideInInspector] _Cutoff     ("Alpha Cutoff", Float)    = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Opaque"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "Queue"           = "Geometry"
        }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor_ST;
            float4 _Color;
            float  _BumpScale;
            float4 _Moss;
            float  _MossMask;
            float  _Smoothness;
            float  _Tiling;
            float  _Offset;
        CBUFFER_END

        TEXTURE2D(_BaseColor);     SAMPLER(sampler_BaseColor);
        TEXTURE2D(_Normal_Map);    SAMPLER(sampler_Normal_Map);
        TEXTURE2D(_TerrainRender); SAMPLER(sampler_TerrainRender);
        ENDHLSL

        // -----------------------------------------------------------------
        // Forward Lit
        // -----------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   RockVertex
            #pragma fragment RockFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            // Declared so the cluster loop only walks reflection probes when the
            // pipeline's probe atlas is on; otherwise reflections come from unity_SpecCube0.
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fog
            #include_with_pragmas "Packages/org.decentraland.unityuniversalinstancer/Runtime/Shaders/Include/GPUInstancerSetup.hlsl"
            #pragma instancing_options procedural:setupGPUI
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float4 tangentWS  : TEXCOORD3; // w = bitangent sign
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings RockVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   vni = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS = vpi.positionCS;
                OUT.positionWS = vpi.positionWS;
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseColor);
                OUT.normalWS   = vni.normalWS;
                OUT.tangentWS  = float4(vni.tangentWS, IN.tangentOS.w);
                return OUT;
            }

            half4 RockFragment(Varyings IN) : SV_Target
            {
                float3 pWS  = GetAbsolutePositionWS(IN.positionWS);
                half4  rock = SAMPLE_TEXTURE2D(_BaseColor, sampler_BaseColor, IN.uv) * _Color;

                // Terrain colour is blended in by absolute world height: full
                // below y = -0.04, none above y = 0.12. The UV is absolute world
                // XZ so neighbouring rocks share the ground pattern.
                float2 uvT     = pWS.xz * 0.1 * _Tiling + _Offset;
                half3  terrain = SAMPLE_TEXTURE2D(_TerrainRender, sampler_TerrainRender, uvT).rgb;
                half3  ground  = lerp(terrain, rock.rgb, smoothstep(-0.04, 0.12, pWS.y));

                // Moss coverage follows the world-space vertex normal, not the
                // normal-mapped one, so it reads as whole facets.
                float3 vertexN   = normalize(IN.normalWS);
                half3  baseColor = lerp(_Moss.rgb, ground, smoothstep(vertexN.y, 1.12, _MossMask));

                float3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_Normal_Map, sampler_Normal_Map, IN.uv), _BumpScale);
                float3 bitangentWS = cross(IN.normalWS, IN.tangentWS.xyz) * IN.tangentWS.w;
                half3x3 tangentToWorld = half3x3(IN.tangentWS.xyz, bitangentWS, IN.normalWS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo     = baseColor;
                surfaceData.metallic   = 0;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS   = normalTS;
                surfaceData.occlusion  = 1;
                surfaceData.alpha      = 1;

                InputData inputData = (InputData)0;
                inputData.positionWS               = IN.positionWS;
                inputData.positionCS               = IN.positionCS;
                inputData.tangentToWorld           = tangentToWorld;
                inputData.normalWS                 = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tangentToWorld));
                inputData.viewDirectionWS          = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.shadowCoord              = TransformWorldToShadowCoord(IN.positionWS);
                inputData.fogCoord                 = InitializeInputDataFog(float4(IN.positionWS, 1.0), 0); // fog is evaluated per fragment from positionWS
                inputData.bakedGI                  = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV  = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask               = half4(1, 1, 1, 1);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return half4(color.rgb, 1);
            }
            ENDHLSL
        }

        // -----------------------------------------------------------------
        // Shadow caster
        // -----------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   ShadowVertex
            #pragma fragment ShadowFragment

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include_with_pragmas "Packages/org.decentraland.unityuniversalinstancer/Runtime/Shaders/Include/GPUInstancerSetup.hlsl"
            #pragma instancing_options procedural:setupGPUI
            #pragma multi_compile_instancing

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            ShadowVaryings ShadowVertex(ShadowAttributes IN)
            {
                ShadowVaryings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 positionWS = GetVertexPositionInputs(IN.positionOS.xyz).positionWS;
                float3 normalWS   = GetVertexNormalInputs(IN.normalOS).normalWS;

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 ShadowFragment(ShadowVaryings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // -----------------------------------------------------------------
        // Depth only
        // -----------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   DepthVertex
            #pragma fragment DepthFragment

            #include_with_pragmas "Packages/org.decentraland.unityuniversalinstancer/Runtime/Shaders/Include/GPUInstancerSetup.hlsl"
            #pragma instancing_options procedural:setupGPUI
            #pragma multi_compile_instancing

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            DepthVaryings DepthVertex(DepthAttributes IN)
            {
                DepthVaryings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                OUT.positionCS = GetVertexPositionInputs(IN.positionOS.xyz).positionCS;
                return OUT;
            }

            half4 DepthFragment(DepthVaryings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // -----------------------------------------------------------------
        // Depth normals
        // -----------------------------------------------------------------
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #include_with_pragmas "Packages/org.decentraland.unityuniversalinstancer/Runtime/Shaders/Include/GPUInstancerSetup.hlsl"
            #pragma instancing_options procedural:setupGPUI
            #pragma multi_compile_instancing

            struct DNAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DNVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
            };

            DNVaryings DepthNormalsVertex(DNAttributes IN)
            {
                DNVaryings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                OUT.positionCS = GetVertexPositionInputs(IN.positionOS.xyz).positionCS;
                OUT.normalWS   = GetVertexNormalInputs(IN.normalOS).normalWS;
                return OUT;
            }

            half4 DepthNormalsFragment(DNVaryings IN) : SV_Target
            {
                float3 n = normalize(IN.normalWS);
                return half4(n * 0.5 + 0.5, 0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
