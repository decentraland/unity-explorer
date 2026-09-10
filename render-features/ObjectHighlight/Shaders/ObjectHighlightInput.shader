// Override material the object-highlight pass draws highlighted renderers with.
//
// Pass 0 fattens the silhouette to form the outline and is blurred afterwards; pass 1 shades the
// surface inside that silhouette and runs after the blur. Every parameter is a global set per
// renderer by the render pass - none are material properties, so the pass can reuse one material
// instead of instantiating one per draw.
Shader "DCL/ObjectHighlight/Input"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        HLSLINCLUDE
        #pragma target 4.5

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        float4 _Highlight_ObjectOffset;
        float4 _Highlight_Color;
        float _Highlight_OutlineWidth;
        float _Highlight_OutlineDepthBias;
        float _Highlight_SurfaceDepthBias;
        float _Highlight_Fill;
        float _Highlight_Rim;
        float _Highlight_FresnelPower;
        float _Highlight_MaxOpacity;
        float _Highlight_Pulse;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float viewDepth : TEXCOORD2;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings SharedVertex(Attributes IN, float outlineWidth)
        {
            Varyings OUT;
            UNITY_SETUP_INSTANCE_ID(IN);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

            float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz + _Highlight_ObjectOffset.xyz);
            float4 positionCS = TransformWorldToHClip(positionWS);

            // Push each vertex out along its screen-space normal to fatten the silhouette. Guarded because
            // normalize(0) is NaN when the clip-space normal has no xy component, which the surface pass
            // would otherwise hit with a width of zero.
            if (outlineWidth > 0.0)
            {
                float3 clipNormal = mul((float3x3)UNITY_MATRIX_VP, mul((float3x3)UNITY_MATRIX_M, IN.normalOS));
                positionCS.xy += normalize(clipNormal.xy) / _ScreenParams.xy * outlineWidth * positionCS.w * 2.0;
            }

            OUT.positionCS = positionCS;
            OUT.positionWS = positionWS;
            OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
            OUT.viewDepth = -TransformWorldToView(positionWS).z;
            return OUT;
        }

        // Eye-space distance between this fragment and whatever opaque geometry is nearest at the same
        // pixel. Positive means something is in front of us.
        float DepthDelta(Varyings IN)
        {
            float2 screenUV = GetNormalizedScreenSpaceUV(IN.positionCS);
            return IN.viewDepth - LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
        }
        ENDHLSL

        // 0 - the outline. Blurred by the following passes, then punched through by pass 1.
        Pass
        {
            Name "ObjectHighlightOutline"

            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment

            Varyings OutlineVertex(Attributes IN)
            {
                return SharedVertex(IN, _Highlight_OutlineWidth);
            }

            half4 OutlineFragment(Varyings IN) : SV_Target
            {
                // Hide the outline behind anything clearly nearer, so it stops drawing over the player.
                // The tolerance is far looser than the surface one: outline fragments sit outside the
                // silhouette, over ground and walls whose depth differs from the object's own by a few
                // centimetres at grazing angles, and a tight threshold would chew holes in the line.
                if (DepthDelta(IN) > _Highlight_OutlineDepthBias)
                    discard;

                return _Highlight_Color;
            }
            ENDHLSL
        }

        // 1 - the surface. Runs after the blur and replaces the silhouette's interior, so writing zero
        // alpha here is what erases the blur's inward bleed.
        Pass
        {
            Name "ObjectHighlightSurface"

            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex SurfaceVertex
            #pragma fragment SurfaceFragment

            Varyings SurfaceVertex(Attributes IN)
            {
                return SharedVertex(IN, 0.0);
            }

            half4 SurfaceFragment(Varyings IN, FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                // Everything hidden is discarded rather than written as zero alpha. This pass replaces
                // rather than blends, so writing would clobber a nearer surface that already shaded this
                // pixel, and whichever draw arrives last would win: a highlighted object is many renderers
                // (a barrel's staves, bands and fish), and its own hidden parts would punch holes through
                // its visible ones. Discarding is order-independent.
                // Back faces first - the target has no depth buffer, so nothing else rejects them.
                if (IS_FRONT_VFACE(cullFace, 1.0, 0.0) < 0.5)
                    discard;

                // Then anything sitting behind other opaque geometry.
                if (DepthDelta(IN) > _Highlight_SurfaceDepthBias)
                    discard;

                float3 viewDirWS = normalize(GetCameraPositionWS() - IN.positionWS);

                // abs() rather than a raw dot, so a mesh whose normals are authored pointing inwards still
                // gets a rim at its silhouette instead of a saturated one across the whole surface.
                float ndv = abs(dot(normalize(IN.normalWS), viewDirWS));
                float rim = pow(saturate(1.0 - ndv), _Highlight_FresnelPower);

                // Capped rather than saturated. The rim is a constant-angle band, so geometry only a few
                // pixels wide sits inside it end to end and would otherwise reach full opacity and hide
                // itself - a fishing rod vanishes under the same maths that makes a barrel read well.
                float alpha = min(_Highlight_Fill + (_Highlight_Rim * rim), _Highlight_MaxOpacity);
                alpha *= _Highlight_Pulse;

                return half4(_Highlight_Color.rgb, alpha * _Highlight_Color.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
