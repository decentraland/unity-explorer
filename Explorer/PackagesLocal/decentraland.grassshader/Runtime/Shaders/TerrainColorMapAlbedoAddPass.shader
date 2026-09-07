// Decentraland / Terrain Color-Map Albedo add-pass bake (URP)
//
// Additive companion to TerrainColorMapAlbedo. A Unity Terrain with more than four layers stores
// the extra layers in additional splat control maps; the color-map renderer draws this shader once
// per extra control map, blending layers 4-7 (8-11, ...) on top of the base pass with additive
// blending. Same procedural fullscreen-triangle and splat-blend maths as the base pass.

Shader "Hidden/Decentraland/TerrainColorMapAlbedoAddPass"
{
    Properties
    {
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

        Pass
        {
            Name "SplatBlendAdd"
            ColorMask RGB
            Blend One One

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragSplat

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                // Mirrors URP's GetFullScreenTriangleTexCoord: only the sampling UV flips, so a
                // destination row reads the same source texel the base pass wrote it from.
                #if UNITY_UV_STARTS_AT_TOP
                uv.y = 1.0 - uv.y;
                #endif
                OUT.uv = uv;
                return OUT;
            }

            // Splat control texels are corner-aligned with the terrain grid, so the outer half-texel
            // has to fold in or the border blends against the clamp edge. Without a bound texel size
            // there is nothing to inset by and the raw UV is already correct.
            float2 ControlUV(float2 uv)
            {
                float2 size = _Control_TexelSize.zw;
                return size.x > 1.0 && size.y > 1.0 ? (uv * (size - 1.0) + 0.5) / size : uv;
            }

            half4 FragSplat(Varyings IN) : SV_Target
            {
                half4 control = SAMPLE_TEXTURE2D(_Control, sampler_Control, ControlUV(IN.uv));
                float2 localXZ = IN.uv * _TerrainSize.xy;

                half3 col = 0;
                col += SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, localXZ * _Splat0_ST.xy + _Splat0_ST.zw).rgb * control.r;
                col += SAMPLE_TEXTURE2D(_Splat1, sampler_Splat1, localXZ * _Splat1_ST.xy + _Splat1_ST.zw).rgb * control.g;
                col += SAMPLE_TEXTURE2D(_Splat2, sampler_Splat2, localXZ * _Splat2_ST.xy + _Splat2_ST.zw).rgb * control.b;
                col += SAMPLE_TEXTURE2D(_Splat3, sampler_Splat3, localXZ * _Splat3_ST.xy + _Splat3_ST.zw).rgb * control.a;

                return half4(col, 0.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
