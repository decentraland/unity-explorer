// Inverted-hull avatar outline for RendererFeature_AvatarOutline.
//
// A single geometry pass drawn BEFORE the opaque queue: it renders the back
// faces of each flagged renderer (Cull Front), extruded outward along the
// vertex normal in clip space by a near-constant screen-space width, in a flat
// outline colour, writing depth. The regular opaque avatar pass then draws over
// the interior, leaving only the extruded rim visible as the outline.
//
// Avatars are skinned by a compute shader into the global avatar vertex buffer
// and their renderers keep the bind-pose mesh, so when the draw pass publishes
// a renderer's vertex base (_DCL_OutlineSkinnedBase >= 0) the position and
// normal come from _GlobalAvatarBuffer at base + vertex id, the same lookup the
// avatar shaders make. A negative base draws the mesh attributes, which covers
// any other skinned/static renderer with normals.
Shader "Hidden/DCL/RenderFeatures/AvatarOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.05, 0.6, 1, 1)
        _OutlineThickness ("Outline Thickness (px-ish)", Float) = 4.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "AvatarOutline"
            Tags { "LightMode" = "DCL_AvatarOutline" }

            Cull Front
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineThickness;
            CBUFFER_END

            // Layout of the compute-skinned avatar vertices (ComputeShaderSkinning.compute).
            struct VertexInfo
            {
                float3 position;
                float3 normal;
                float4 tangent;
            };

            StructuredBuffer<VertexInfo> _GlobalAvatarBuffer;

            // Set per draw by RenderPass_OutlineDraw: the renderer's first vertex in
            // _GlobalAvatarBuffer, or negative for renderers that own their mesh.
            float _DCL_OutlineSkinnedBase;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                uint   vertexID   : SV_VertexID;
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

                float3 positionOS = input.positionOS.xyz;
                float3 normalOS = input.normalOS;

                if (_DCL_OutlineSkinnedBase >= 0.0)
                {
                    VertexInfo skinned = _GlobalAvatarBuffer[(uint)_DCL_OutlineSkinnedBase + input.vertexID];
                    positionOS = skinned.position;
                    normalOS = skinned.normal;
                }

                VertexPositionInputs vpi = GetVertexPositionInputs(positionOS);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);

                float4 posCS = vpi.positionCS;
                // Project the world normal into clip space and offset along it.
                // Multiplying by posCS.w keeps the width ~constant in screen space
                // after the perspective divide; the aspect term keeps X and Y even.
                float3 normalCS = mul((float3x3)UNITY_MATRIX_VP, normalWS);
                float2 dir = normalize(normalCS.xy + 1e-6);
                float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);
                dir.x /= aspect;

                posCS.xy += dir * (_OutlineThickness * 0.001) * posCS.w;
                o.positionHCS = posCS;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
