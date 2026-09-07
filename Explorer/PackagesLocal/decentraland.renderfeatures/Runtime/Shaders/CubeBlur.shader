// DCL/CubeBlur — angular blur / roughness convolution for the skybox
// environment probe. For each texel of the bound cubemap face RT it
// reconstructs the world direction (from _CubemapFace + screen UV), builds a
// tangent frame around it and averages a kernel of samples of _SourceCube taken
// at LOD _SourceMip and spread by _BlurRadius (tangent-plane radians-ish). Wider
// radius + higher source LOD at deeper mips gives a cheap prefiltered chain so
// rough/metallic surfaces read a softened sky.
Shader "DCL/CubeBlur"
{
    Properties
    {
        _SourceMip ("Source Mip", Float) = 0
        _CubemapFace ("Cubemap Face", Float) = 0
        _BlurRadius ("Blur Radius", Float) = 0.05
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "CubeBlur"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            TEXTURECUBE(_SourceCube);
            SAMPLER(sampler_SourceCube);
            float _SourceMip;
            float _CubemapFace;
            float _BlurRadius;

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;
                float2 p = float2((vertexID << 1) & 2, vertexID & 2);
                o.uv = p;
                o.positionHCS = float4(p * 2.0 - 1.0, 0.0, 1.0);
                return o;
            }

            float3 DirFromFaceUV(int face, float2 uv)
            {
                float2 t = uv * 2.0 - 1.0;
                float3 dir;
                if (face == 0)      dir = float3( 1.0, -t.y, -t.x);
                else if (face == 1) dir = float3(-1.0, -t.y,  t.x);
                else if (face == 2) dir = float3( t.x,  1.0,  t.y);
                else if (face == 3) dir = float3( t.x, -1.0, -t.y);
                else if (face == 4) dir = float3( t.x, -t.y,  1.0);
                else                dir = float3(-t.x, -t.y, -1.0);
                return normalize(dir);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 n = DirFromFaceUV((int)_CubemapFace, input.uv);

                // Tangent frame around the sampling direction.
                float3 up = abs(n.y) < 0.99 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
                float3 tangent = normalize(cross(up, n));
                float3 bitangent = cross(n, tangent);

                // 5x5 separable-ish Gaussian kernel over the tangent plane.
                const int R = 2;
                float3 acc = 0.0;
                float wsum = 0.0;
                [unroll] for (int y = -R; y <= R; y++)
                {
                    [unroll] for (int x = -R; x <= R; x++)
                    {
                        float2 o = float2(x, y) * _BlurRadius;
                        float w = exp(-(o.x * o.x + o.y * o.y) / max(2.0 * _BlurRadius * _BlurRadius, 1e-6));
                        float3 dir = normalize(n + tangent * o.x + bitangent * o.y);
                        acc += SAMPLE_TEXTURECUBE_LOD(_SourceCube, sampler_SourceCube, dir, _SourceMip).rgb * w;
                        wsum += w;
                    }
                }
                return half4(acc / max(wsum, 1e-6), 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
