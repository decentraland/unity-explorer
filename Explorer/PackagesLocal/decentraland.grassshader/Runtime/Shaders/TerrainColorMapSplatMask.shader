// Decentraland / Terrain Color-Map Splat coverage mask (URP)
//
// Writes per-terrain ground coverage into the color-map's alpha channel. The renderer clears the
// map's alpha to zero, then draws this shader over each terrain's sub-rectangle so alpha marks
// where a terrain actually exists inside the (possibly larger) color-map bounds box. The grass
// shader multiplies its ground tint by that coverage, so blades over empty regions between sparse
// or L-shaped terrain layouts are not tinted. Coverage is the summed splat weight, so undefined
// splat texels contribute nothing.
//
// Blending is additive because a terrain's layer weights are spread across one control map per four
// layers: a terrain whose ground is painted with layers 4+ carries no weight at all in the first
// map, so the renderer draws this pass once per control map and the target's UNorm format clamps
// the accumulated coverage at 1.

Shader "Hidden/Decentraland/TerrainColorMapSplatMask"
{
    Properties
    {
        _Control ("Control (Splatmap)", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Coverage"
            ColorMask A
            Blend One One

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Control);  SAMPLER(sampler_Control);
            float4 _Control_ST;
            float4 _Control_TexelSize;
            // 1 for each control channel that carries a layer, so channels past the ground's layer
            // count (an opaque alpha, an unused channel) add no coverage.
            float4 _LayerMask;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings OUT;
                float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
                OUT.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                // Mirrors URP's GetFullScreenTriangleTexCoord: only the sampling UV flips, so the
                // coverage a destination row records comes from the terrain texel that row shows.
                #if UNITY_UV_STARTS_AT_TOP
                uv.y = 1.0 - uv.y;
                #endif
                OUT.uv = uv;
                return OUT;
            }

            // A control map that spans the region exactly once (_Control_ST == 1,1,0,0) is
            // clamp-sampled with its texels corner-aligned to the terrain grid, so the outer
            // half-texel has to fold in or the border blends against the clamp edge. Without a bound
            // texel size there is nothing to inset by and the raw UV is already correct. A tiled
            // control map repeats across the region through _Control_ST and needs no inset.
            float2 ControlUV(float2 uv)
            {
                bool spansRegion = all(_Control_ST.xy == 1.0) && all(_Control_ST.zw == 0.0);
                float2 size = _Control_TexelSize.zw;
                float2 inset = size.x > 1.0 && size.y > 1.0 ? (uv * (size - 1.0) + 0.5) / size : uv;
                return spansRegion ? inset : uv * _Control_ST.xy + _Control_ST.zw;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                half4 control = SAMPLE_TEXTURE2D(_Control, sampler_Control, ControlUV(IN.uv));
                half coverage = saturate(dot(control, _LayerMask));
                return half4(0.0, 0.0, 0.0, coverage);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
