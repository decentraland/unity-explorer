Shader "Unlit/VertOut"
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

         struct appdata
         {
            float4 vertex : POSITION;
            float2 uv : TEXCOORD0;
         };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            struct SVertOut
          {
             float3 pos;
             float3 norm;
             float4 tang;
          };

            StructuredBuffer<SVertOut> _VertIn;

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v, uint vIdx : SV_VertexID)
            {
               v2f o;
               o.vertex = UnityObjectToClipPos(_VertIn[vIdx].pos);
               o.uv = TRANSFORM_TEX(v.uv, _MainTex);
               return o;
             //v.vertex.xyz = UnityObjectToClipPosODS(_VertIn[vIdx].pos);
             //v.normal = vin.norm;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}