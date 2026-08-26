// Outfit Studio — flat white avatar shader for emote thumbnails.
//
// The look DCL's own emote listings use: an unlit white mannequin with a contour. Everything the
// other studio shaders do to shape the surface — lighting, rim, matcap, emission, base color,
// normal maps — is deliberately absent, so the render carries silhouette and pose only. The
// outline is the only thing left to art-direct, so width, colour and "as mask" (cut the outline
// out of the image instead of painting it) and detail suppression (drop the line where the
// surface creases too sharply for it to read cleanly) are the mode's only knobs.
//
// The one thing it still reads from the albedo is ALPHA (see EmotesAlphaClip) — cutout wearables
// would otherwise become solid slabs.
//
// Writing 1.0 is NOT enough to get white on screen: the studio camera's ACES tonemapping lands a
// 1.0 surface on 212/255, and Bloom's soft knee (threshold 1, knee 0.5) still haloes it. The
// switcher zeroes the camera's volumeLayerMask while this mode is selected so the volume stack
// falls back to its defaults — no tonemapping, no bloom — and 1.0 reaches 255,255,255. Keep the
// forward pass at exactly 1.0 rather than above it: nothing here should read as emissive if the
// bypass is ever lifted.
//
// Property names match DCL/DCL_Toon and DCL/DCL_Stylized_PBR so the studio's shader switcher can
// reassign material.shader in both directions without losing values — see DCL_Emotes_Input.hlsl.
Shader "DCL/DCL_Emotes"
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

        // --- Base (only _MainTex's alpha and _BaseColor's alpha are read; the RGB is discarded)
        _MainTex ("BaseMap", 2D) = "white" {}
        _BaseColor ("BaseColor", Color) = (1,1,1,1)
        _NormalMap ("NormalMap", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(0, 2)) = 1

        // --- Carried, not used: declared so a swap to this shader and back is lossless
        _MetallicGlossMap ("Metallic(B) Roughness(G)", 2D) = "black" {}
        _Metallic ("Metallic (fallback)", Range(0, 1)) = 0
        _Smoothness ("Smoothness (fallback)", Range(0, 1)) = 0.5
        _Specular ("Specular (dielectric F0 scale)", Range(0, 1)) = 0.5
        _Sheen ("Sheen", Range(0, 1)) = 0
        _SheenTint ("Sheen Tint", Range(0, 1)) = 0.5
        _Clearcoat ("Clearcoat", Range(0, 1)) = 0
        _ClearcoatGloss ("Clearcoat Gloss", Range(0, 1)) = 0.8
        _DiffuseWrap ("Diffuse Wrap", Range(0, 1)) = 0.35
        _ShadowSharpness ("Shadow Sharpness", Range(0, 1)) = 0.35
        _SpecularSoftness ("Specular Softness", Range(0, 4)) = 0.5
        [Toggle(_)] _RimLight ("RimLight", Float) = 1
        _RimLightIntensity ("Rim Intensity", Range(0, 4)) = 1
        _RimLightColor ("RimLightColor", Color) = (1,1,1,1)
        _RimLight_Power ("RimLight_Power", Range(0, 1)) = 0.3
        _RimLight_InsideMask ("RimLight_InsideMask", Range(0.0001, 1)) = 0.15
        _RimSharpness ("Rim Sharpness", Range(0, 1)) = 0
        [Toggle(_)] _Is_LightColor_RimLight ("Is_LightColor_RimLight", Float) = 1
        _GI_Intensity ("GI_Intensity", Range(0, 5)) = 1
        _MatCap_Sampler ("MatCap (metal env)", 2D) = "black" {}
        _MatCapColor ("MatCapColor", Color) = (1,1,1,1)
        _BlurLevelMatcap ("Blur Level Matcap", Range(0, 4)) = 0
        _MatcapMetalBlend ("Matcap Metal Blend", Range(0, 1)) = 1
        _StylizedMetalStrength ("Metal Strength", Range(0, 4)) = 1
        _EmissionStrength ("Emission Strength", Range(0, 2)) = 0.19
        _Emissive_Tex ("Emissive", 2D) = "white" {}
        _Emissive_Color ("Emissive Color", Color) = (0,0,0,1)
        [Toggle(_)] _Is_BlendBaseColor ("Is_BlendBaseColor", Float) = 1

        // --- Outline. The one thing this mode does art-direct: the studio pushes these from its
        // own knobs (EmotesKnobs in StudioAvatarShaderSwitcher), so they never inherit whatever
        // the previously selected shader left on the material. Defaults match those knobs.
        // _OutlineEnabled is inert here — the outline on/off control is the card frame's
        // "Hide outline" (AvatarLoader.OutlineSuppressed), which drops the renderer from the
        // outline pass entirely.
        [Toggle(_)] _OutlineEnabled ("Outline Enabled", Float) = 1
        _Outline_Width ("Outline_Width", Range(0, 10)) = 5
        _Outline_Color ("Outline_Color", Color) = (0,0,0,1)
        [Toggle(_)] _OutlineAsMask ("Outline As Mask", Float) = 1
        // 0 = off (every silhouette edge draws). Higher values drop the outline wherever the
        // surface creases sharply enough to break the line into noise — fingers, face wrinkles —
        // by discarding outline fragments where screen-space normal curvature exceeds a threshold
        // that tightens as this goes up. See the crease-detection note in the Outline pass.
        _Outline_DetailSuppress ("Outline_DetailSuppress", Range(0, 1)) = 0.33

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
            // RenderFeature_AvatarOutline draws the avatar's own material with FindPass("Outline"),
            // so the outline works purely by this pass existing under that name.
            Name "Outline"
            Tags { "LightMode" = "Outline" }
            Cull Front
            // Depth-biases the shell away from the camera a touch so it reliably loses the depth
            // test against unrelated nearby geometry it happens to sit close to in world space —
            // shoe-vs-deck, sleeve-vs-hand contact points — instead of flickering/z-fighting with
            // it. Distinct from the crease noise _Outline_DetailSuppress fixes: this is two separate
            // meshes' shells colliding, not one mesh folding into itself, so it needs its own knob.
            // Flip the sign or scale the magnitude if it undershoots/overshoots in testing.
            Offset 2, 2

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #pragma dynamic_branch _IS_CLIPPING_OFF _IS_CLIPPING_MODE _IS_CLIPPING_TRANSMODE

            #include "DCL_Emotes_Input.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 texcoord   : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 positionCS : SV_POSITION;
            };

            Varyings OutlineVertex(Attributes input)
            {
                Varyings output;
                // Inverted hull, same scale convention as DCL_Toon
                // (width * 0.001, faded out between 0.5 and 100 units from the camera).
                // Distance is measured per VERTEX, not from the object's pivot: a dynamic pose can
                // put an arm or leg meaningfully closer to (or farther from) the camera than the
                // pivot, and a single object-wide distance gave every vertex the same world-space
                // push regardless of its own depth — parts nearer the camera then projected thicker
                // on screen, parts farther away projected thinner (sometimes sub-pixel), instead of
                // a uniform stroke width across the whole silhouette.
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float camDist = distance(positionWS, _WorldSpaceCameraPos);
                float width = _Outline_Width * 0.001 * smoothstep(100.0, 0.5, camDist);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz + input.normalOS * width);
                output.uv = input.texcoord;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 OutlineFragment(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TRANSFORM_TEX(input.uv, _MainTex));
                EmotesAlphaClip(texColor.a);

                // Detail suppression. The extruded hull pokes into neighbouring geometry wherever
                // the surface creases sharply (fingers pressed together, face wrinkles) and z-fights
                // with it, which reads as noise/dashes instead of a stroke. fwidth(normalWS) spikes
                // at those hard creases (screen-space normal discontinuity between triangles) while
                // staying near zero along the true smooth silhouette, so it doubles as a curvature
                // detector without any extra mesh data. Cutoff tightens as the knob goes up; 0 keeps
                // every silhouette edge exactly as before.
                if (_Outline_DetailSuppress > 0.001)
                {
                    half curvature = length(fwidth(input.normalWS));
                    half cutoff = lerp(1.0h, 0.02h, (half)_Outline_DetailSuppress);
                    clip(cutoff - curvature);
                }

                // Mask mode. This pass is what CUTS the outline: the feature injects it at
                // BeforeRenderingOpaques with ZWrite on, so the hull stamps near depth and
                // everything drawn afterwards — the card panel, the body, the other wearables —
                // depth-fails inside the band. Painting it black is only the last step, so writing
                // zeroes here leaves the band empty.
                //
                // What FILLS it is the other half of the toggle: StudioCardFrame flips the card
                // panel to ZTest Always / ZWrite Off while this is on, so the card paints straight
                // through the cut without disturbing the stamp that keeps the avatar out. With no
                // card frame the band just stays at the camera's transparent clear — a hole, which
                // exports transparent and reads as black on screen.
                //
                // RGB must be 0 as well as alpha, not just alpha: OutfitCapture.RecoverAdditiveAlpha
                // re-derives alpha from brightness when exporting a transparent still, and would
                // resurrect a transparent-but-bright pixel as opaque.
                if (_OutlineAsMask > 0.5) return half4(0, 0, 0, 0);

                // The knob colour literally, never tinted by the garment albedo — this mode has
                // thrown the albedo away everywhere else too.
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
            #pragma vertex EmotesVertex
            #pragma fragment EmotesFragment
            #pragma dynamic_branch _IS_CLIPPING_OFF _IS_CLIPPING_MODE _IS_CLIPPING_TRANSMODE

            #include "DCL_Emotes_Input.hlsl"

            // No instancing/stereo macros: nothing here varies per instance, and the avatar is
            // skinned geometry that never instances — same shape as the DepthOnly pass below.
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

            Varyings EmotesVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.texcoord;
                return output;
            }

            half4 EmotesFragment(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TRANSFORM_TEX(input.uv, _MainTex));
                EmotesAlphaClip(texColor.a);

                // No fog either: it would tint the white by depth, which is exactly the kind of
                // shading this mode exists to remove.

                // Alpha: same semantics as DCL_Toon (opaque unless a clipping mode is active)
                half alpha = 1.0;
                if (_IS_CLIPPING_MODE || _IS_CLIPPING_TRANSMODE)
                {
                    alpha = saturate(texColor.a + _Tweak_transparency);
                }

                return half4(1.0, 1.0, 1.0, alpha);
            }
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

            #include "DCL_Emotes_Input.hlsl"
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
                EmotesAlphaClip(texColor.a);
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

            #include "DCL_Emotes_Input.hlsl"

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
                EmotesAlphaClip(texColor.a);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            // Flat geometric normals: this shader never samples the normal map, so writing a
            // perturbed normal here would let a depth-normals consumer shade detail the visible
            // render doesn't have.
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_CullMode]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma dynamic_branch _IS_CLIPPING_OFF _IS_CLIPPING_MODE _IS_CLIPPING_TRANSMODE

            #include "DCL_Emotes_Input.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 texcoord   : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthNormalsVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.texcoord;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFragment(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TRANSFORM_TEX(input.uv, _MainTex));
                EmotesAlphaClip(texColor.a);
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
