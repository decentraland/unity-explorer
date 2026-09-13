// Silhouette mask shader for RenderFeature_ObjectHighlight (input material).
//
// Draws each highlighted renderer into an offscreen mask RT. The output is
// premultiplied: rgb = tint * opacity, a = opacity. Storing premultiplied
// colour lets the blur/composite recover the pure tint by dividing by the
// coverage weight (rgb / a) without the opacity term skewing the hue.
//
// The per-object tint/opacity arrives through the global _HighlightColor,
// set once per renderer by the feature's mask pass (CommandBuffer.DrawRenderer
// has no per-draw MaterialPropertyBlock overload, so a global is used).
//
// No depth test: the full silhouette is captured regardless of occlusion so
// the outline stays continuous when the object is partially hidden.
// _HighlightColor is deliberately NOT a shader property: a declared property is
// backed by the material's own value, which takes precedence over the global the
// mask pass sets per renderer and would pin every silhouette to one tint.
Shader "Hidden/DCL/RenderFeatures/HighlightMask"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "HighlightMask"
            Tags { "LightMode" = "DCL_HighlightMask" }

            ZWrite Off
            ZTest Always
            Cull Off
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _HighlightColor;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half a = _HighlightColor.a;
                return half4(_HighlightColor.rgb * a, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
