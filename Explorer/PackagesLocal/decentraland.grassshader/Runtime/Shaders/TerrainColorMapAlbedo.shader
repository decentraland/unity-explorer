// Decentraland / Terrain Color-Map Albedo bake (URP)
//
// Renders a Unity Terrain's splat-blended surface colour, top-down and unlit, into the
// GrassColorMapRenderer's RenderTexture. Pass 0 blends up to four terrain layers weighted by a
// splat control map; pass 1 blits a single raw texture (used for non-terrain objects and for the
// "use original materials" bake path). A fullscreen triangle is emitted procedurally, so the
// renderer only has to set the destination viewport and bind the per-terrain textures.

Shader "Hidden/Decentraland/TerrainColorMapAlbedo"
{
    Properties
    {
        _MainTex ("Raw Texture", 2D)        = "white" {}
        _Control ("Control (Splatmap)", 2D) = "black" {}
        _Splat0  ("Layer 0", 2D)            = "white" {}
        _Splat1  ("Layer 1", 2D)            = "white" {}
        _Splat2  ("Layer 2", 2D)            = "white" {}
        _Splat3  ("Layer 3", 2D)            = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        ZTest Always
        ZWrite Off
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);
        TEXTURE2D(_Control);  SAMPLER(sampler_Control);
        TEXTURE2D(_Splat0);   SAMPLER(sampler_Splat0);
        TEXTURE2D(_Splat1);   SAMPLER(sampler_Splat1);
        TEXTURE2D(_Splat2);   SAMPLER(sampler_Splat2);
        TEXTURE2D(_Splat3);   SAMPLER(sampler_Splat3);

        float4 _Splat0_ST;
        float4 _Splat1_ST;
        float4 _Splat2_ST;
        float4 _Splat3_ST;
        float4 _TerrainSize;
        float4 _Control_ST;
        float4 _Control_TexelSize;

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
            // Mirrors URP's GetFullScreenTriangleTexCoord: the clip position is unflipped, only the
            // sampling UV flips, so on top-left-origin APIs the source texel a destination row reads
            // still matches the +Z-up world mapping the grass shader samples the finished map with.
            #if UNITY_UV_STARTS_AT_TOP
            uv.y = 1.0 - uv.y;
            #endif
            OUT.uv = uv;
            return OUT;
        }

        // A control map that spans the region exactly once (_Control_ST == 1,1,0,0) is clamp-sampled
        // with its texels corner-aligned to the terrain grid, so the outer half-texel has to fold in
        // or the border blends against the clamp edge. Without a bound texel size there is nothing
        // to inset by and the raw UV is already correct. A tiled control map repeats across the
        // region through _Control_ST and needs no inset.
        float2 ControlUV(float2 uv)
        {
            bool spansRegion = all(_Control_ST.xy == 1.0) && all(_Control_ST.zw == 0.0);
            float2 size = _Control_TexelSize.zw;
            float2 inset = size.x > 1.0 && size.y > 1.0 ? (uv * (size - 1.0) + 0.5) / size : uv;
            return spansRegion ? inset : uv * _Control_ST.xy + _Control_ST.zw;
        }
        ENDHLSL

        Pass
        {
            Name "SplatBlend"
            ColorMask RGB
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragSplat

            half4 FragSplat(Varyings IN) : SV_Target
            {
                half4 control = SAMPLE_TEXTURE2D(_Control, sampler_Control, ControlUV(IN.uv));
                float2 localXZ = IN.uv * _TerrainSize.xy;

                half3 col = 0;
                col += SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, localXZ * _Splat0_ST.xy + _Splat0_ST.zw).rgb * control.r;
                col += SAMPLE_TEXTURE2D(_Splat1, sampler_Splat1, localXZ * _Splat1_ST.xy + _Splat1_ST.zw).rgb * control.g;
                col += SAMPLE_TEXTURE2D(_Splat2, sampler_Splat2, localXZ * _Splat2_ST.xy + _Splat2_ST.zw).rgb * control.b;
                col += SAMPLE_TEXTURE2D(_Splat3, sampler_Splat3, localXZ * _Splat3_ST.xy + _Splat3_ST.zw).rgb * control.a;

                return half4(col, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Raw"
            ColorMask RGB
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragRaw

            half4 FragRaw(Varyings IN) : SV_Target
            {
                half3 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).rgb;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
