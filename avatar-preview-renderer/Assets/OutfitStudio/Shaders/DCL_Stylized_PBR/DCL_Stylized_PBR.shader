// Outfit Studio — stylized PBR avatar shader (Disney-principled core + stylization layer).
// Property names deliberately match DCL/DCL_Toon so the studio's shader switcher can swap
// material.shader without losing values. See DCL_StylizedPBR_ForwardPass.hlsl for the model.
Shader "DCL/DCL_Stylized_PBR"
{
    Properties
    {
        // --- Per-renderer gates set by ToonMaterialGenerator (shared contract with DCL_Toon)
        [HideInInspector] [PerRendererData] _MainTexArr_ID ("MainTex Array ID", Integer) = -1
        [HideInInspector] [PerRendererData] _NormalMapArr_ID ("Normal Map Array ID", Integer) = -1
        [HideInInspector] [PerRendererData] _MatCap_SamplerArr_ID ("MatCap Array ID", Integer) = -1
        [HideInInspector] [PerRendererData] _Emissive_TexArr_ID ("Emissive Array ID", Integer) = -1
        [HideInInspector] [PerRendererData] _MetallicGlossMapArr_ID ("MetallicGlossMap Array ID", Integer) = -1
        [HideInInspector] [PerRendererData] _IsStylizedMetallic ("Is Stylized Metallic", Integer) = 0

        // --- Base
        _MainTex ("BaseMap", 2D) = "white" {}
        _BaseColor ("BaseColor", Color) = (1,1,1,1)
        _NormalMap ("NormalMap", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(0, 2)) = 1

        // --- PBR (Disney-principled sliders)
        _MetallicGlossMap ("Metallic(B) Roughness(G)", 2D) = "black" {}
        _Metallic ("Metallic (fallback)", Range(0, 1)) = 0
        _Smoothness ("Smoothness (fallback)", Range(0, 1)) = 0.5
        _Specular ("Specular (dielectric F0 scale)", Range(0, 1)) = 0.5
        _Sheen ("Sheen", Range(0, 1)) = 0
        _SheenTint ("Sheen Tint", Range(0, 1)) = 0.5
        _Clearcoat ("Clearcoat", Range(0, 1)) = 0
        _ClearcoatGloss ("Clearcoat Gloss", Range(0, 1)) = 0.8

        // --- Stylization
        _DiffuseWrap ("Diffuse Wrap", Range(0, 1)) = 0.35
        _ShadowSharpness ("Shadow Sharpness", Range(0, 1)) = 0.35
        _SpecularSoftness ("Specular Softness", Range(0, 4)) = 0.5

        // --- Rim (names shared with DCL_Toon so carried values feel familiar)
        [Toggle(_)] _RimLight ("RimLight", Float) = 1
        _RimLightIntensity ("Rim Intensity", Range(0, 4)) = 1
        _RimLightColor ("RimLightColor", Color) = (1,1,1,1)
        _RimLight_Power ("RimLight_Power", Range(0, 1)) = 0.3
        _RimLight_InsideMask ("RimLight_InsideMask", Range(0.0001, 1)) = 0.15
        _RimSharpness ("Rim Sharpness", Range(0, 1)) = 0
        [Toggle(_)] _Is_LightColor_RimLight ("Is_LightColor_RimLight", Float) = 1

        // --- Ambient / env
        _GI_Intensity ("GI_Intensity", Range(0, 5)) = 1
        _MatCap_Sampler ("MatCap (metal env)", 2D) = "black" {}
        _MatCapColor ("MatCapColor", Color) = (1,1,1,1)
        _BlurLevelMatcap ("Blur Level Matcap", Range(0, 4)) = 0
        _MatcapMetalBlend ("Matcap Metal Blend", Range(0, 1)) = 1
        _StylizedMetalStrength ("Metal Strength", Range(0, 4)) = 1
        _EmissionStrength ("Emission Strength", Range(0, 2)) = 0.19

        // --- Emission
        _Emissive_Tex ("Emissive", 2D) = "white" {}
        _Emissive_Color ("Emissive Color", Color) = (0,0,0,1)

        // --- Outline (user toggle; pass must exist for the avatar outline feature's FindPass("Outline"))
        [Toggle(_)] _OutlineEnabled ("Outline Enabled", Float) = 1
        _Outline_Width ("Outline_Width", Range(0, 10)) = 2
        _Outline_Color ("Outline_Color", Color) = (0.6320754, 0.6320754, 0.6320754, 1)
        [Toggle(_)] _Is_BlendBaseColor ("Is_BlendBaseColor", Float) = 1

        // --- Clipping / transparency (shared contract with DCL_Toon)
        _Clipping_Level ("Clipping_Level", Range(0, 1)) = 0
        _Tweak_transparency ("Tweak_transparency", Range(-1, 1)) = 0

        // --- Render state (set by ToonMaterialGenerator)
        [Enum(OFF, 0, FRONT, 1, BACK, 2)] _CullMode ("Cull Mode", int) = 2
        [Enum(OFF, 0, ON, 1)] _ZWriteMode ("ZWrite Mode", int) = 1
        [HideInInspector] _SrcBlend ("Src Blend", Float) = 5   // SrcAlpha
        [HideInInspector] _DstBlend ("Dst Blend", Float) = 10  // OneMinusSrcAlpha
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "Outline" }
            Cull Front

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #pragma dynamic_branch _IS_CLIPPING_OFF _IS_CLIPPING_MODE _IS_CLIPPING_TRANSMODE

            #include "DCL_StylizedPBR_Input.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 texcoord   : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv         : TEXCOORD0;
                float4 positionCS : SV_POSITION;
            };

            Varyings OutlineVertex(Attributes input)
            {
                Varyings output;
                // Inverted hull, same scale convention as DCL_Toon
                // (width * 0.001, faded out between 0.5 and 100 units from the camera)
                float3 objPos = TransformObjectToWorld(float3(0, 0, 0));
                float camDist = distance(objPos, _WorldSpaceCameraPos);
                float width = _Outline_Width * 0.001 * smoothstep(100.0, 0.5, camDist) * _OutlineEnabled;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz + input.normalOS * width);
                output.uv = input.texcoord;
                return output;
            }

            half4 OutlineFragment(Varyings input) : SV_Target
            {
                if (_OutlineEnabled < 0.5) clip(-1);
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TRANSFORM_TEX(input.uv, _MainTex));
                StylizedPBRAlphaClip(texColor.a);
                // Render the tuning-knob _Outline_Color literally (flat art-direction color) rather
                // than tinting it by the garment albedo, so the picked color shows exactly.
                return half4(_Outline_Color.rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite [_ZWriteMode]
            Cull [_CullMode]
            // Separate alpha factors so transparent surfaces accumulate coverage rather than eroding
            // the alpha of the opaque geometry behind them — see the note in DCL_Toon_Studio.shader.
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex StylizedPBRVertex
            #pragma fragment StylizedPBRFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog
            #pragma dynamic_branch _IS_CLIPPING_OFF _IS_CLIPPING_MODE _IS_CLIPPING_TRANSMODE

            #include "DCL_StylizedPBR_ForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_CullMode]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma dynamic_branch _IS_CLIPPING_OFF _IS_CLIPPING_MODE _IS_CLIPPING_TRANSMODE

            #include "DCL_StylizedPBR_Input.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 texcoord   : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv         : TEXCOORD0;
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                output.positionCS = positionCS;
                output.uv = input.texcoord;
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TRANSFORM_TEX(input.uv, _MainTex));
                StylizedPBRAlphaClip(texColor.a);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_CullMode]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma dynamic_branch _IS_CLIPPING_OFF _IS_CLIPPING_MODE _IS_CLIPPING_TRANSMODE

            #include "DCL_StylizedPBR_Input.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 texcoord   : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv         : TEXCOORD0;
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.texcoord;
                return output;
            }

            half DepthOnlyFragment(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TRANSFORM_TEX(input.uv, _MainTex));
                StylizedPBRAlphaClip(texColor.a);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_CullMode]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma dynamic_branch _IS_CLIPPING_OFF _IS_CLIPPING_MODE _IS_CLIPPING_TRANSMODE

            #include "DCL_StylizedPBR_Input.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 texcoord   : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 tangentWS  : TEXCOORD2;
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthNormalsVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.texcoord;
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                return output;
            }

            half4 DepthNormalsFragment(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TRANSFORM_TEX(input.uv, _MainTex));
                StylizedPBRAlphaClip(texColor.a);

                half3 normalTS = SampleNormalTS(input.uv);
                float sgn = input.tangentWS.w;
                float3 bitangent = sgn * cross(input.normalWS, input.tangentWS.xyz);
                half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent, input.normalWS);
                half3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tangentToWorld));
                return half4(NormalizeNormalPerPixel(normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
