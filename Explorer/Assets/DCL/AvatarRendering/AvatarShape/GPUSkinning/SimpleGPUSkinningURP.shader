Shader "Unlit/SimpleGPUSkinningURP"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct MeshInput
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
                float4 boneWeights01 : TEXCOORD1;
                float4 boneWeights23 : TEXCOORD2;
            };

            struct Vertex2Fragment
            {
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4x4 _WorldInverse;
            float4x4 _Matrices[100];
            float4x4 _BindPoses[100];

            float4x4 inverse(float4x4 input)
            {
                #define minor(a,b,c) determinant(float3x3(input.a, input.b, input.c))
                //determinant(float3x3(input._22_23_23, input._32_33_34, input._42_43_44))

                float4x4 cofactors = float4x4(
                    minor(_22_23_24, _32_33_34, _42_43_44),
                    -minor(_21_23_24, _31_33_34, _41_43_44),
                    minor(_21_22_24, _31_32_34, _41_42_44),
                    -minor(_21_22_23, _31_32_33, _41_42_43),

                    -minor(_12_13_14, _32_33_34, _42_43_44),
                    minor(_11_13_14, _31_33_34, _41_43_44),
                    -minor(_11_12_14, _31_32_34, _41_42_44),
                    minor(_11_12_13, _31_32_33, _41_42_43),

                    minor(_12_13_14, _22_23_24, _42_43_44),
                    -minor(_11_13_14, _21_23_24, _41_43_44),
                    minor(_11_12_14, _21_22_24, _41_42_44),
                    -minor(_11_12_13, _21_22_23, _41_42_43),

                    -minor(_12_13_14, _22_23_24, _32_33_34),
                    minor(_11_13_14, _21_23_24, _31_33_34),
                    -minor(_11_12_14, _21_22_24, _31_32_34),
                    minor(_11_12_13, _21_22_23, _31_32_33)
                );
                #undef minor
                return transpose(cofactors) / determinant(input);
            }
            
            Vertex2Fragment vert(MeshInput IN)
            {
                Vertex2Fragment o;

                float4x4 localBoneMatrix0 = mul(mul(_WorldInverse, _Matrices[IN.boneWeights01.x]),
                                                _BindPoses[IN.boneWeights01.x]);
                float4x4 localBoneMatrix1 = mul(mul(_WorldInverse, _Matrices[IN.boneWeights01.z]),
                                                _BindPoses[IN.boneWeights01.z]);
                float4x4 localBoneMatrix2 = mul(mul(_WorldInverse, _Matrices[IN.boneWeights23.x]),
                                                _BindPoses[IN.boneWeights23.x]);
                float4x4 localBoneMatrix3 = mul(mul(_WorldInverse, _Matrices[IN.boneWeights23.z]),
                                                _BindPoses[IN.boneWeights23.z]);

                // Skin with 4 weights per vertex
                float4x4 finalPose = mul(localBoneMatrix0, IN.boneWeights01.y);
                finalPose += mul(localBoneMatrix1, IN.boneWeights01.w);
                finalPose += mul(localBoneMatrix2, IN.boneWeights23.y);
                finalPose += mul(localBoneMatrix3, IN.boneWeights23.w);

                float4 pos = mul(finalPose, float4(IN.vertex.xyz, 1.0));
                float3x3 trans = transpose(inverse(finalPose));
                float3 normal = mul(trans, IN.normal);

                o.normal = normalize(normal);
                o.vertex = UnityObjectToClipPos(pos);
                o.color = ShadeVertexLightsFull(o.vertex, o.normal, 4, false);

                o.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return o;
            }

            fixed4 frag(Vertex2Fragment IN) : SV_Target
            {
                float3 col = tex2D(_MainTex, IN.uv) * float4(1,1,1,1);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
