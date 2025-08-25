Shader "Custom/TerrainFromTexture"
{
    Properties
    {
        _HeightTexture ("Height Texture (R=Height, GB=Normal)", 2D) = "black" {}
        _HeightScale ("Height Scale", Float) = 100.0
        _WorldSize ("World Size", Float) = 1000.0
        _MainTex ("Albedo Texture", 2D) = "white" {}
        _Color ("Base Color", Color) = (1,1,1,1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        sampler2D _HeightTexture;
        sampler2D _MainTex;

        float _HeightScale;
        float _WorldSize;
        fixed4 _Color;
        half _Glossiness;
        half _Metallic;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 worldNormal;
        };

        // Unpack normal from RG channels
        float3 UnpackNormalRG(float2 packedNormal)
        {
            float2 normal_xz = packedNormal * 2.0 - 1.0;
            float normal_y = sqrt(max(0.0, 1.0 - dot(normal_xz, normal_xz)));
            return float3(normal_xz.x, normal_y, normal_xz.y);
        }

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);

            // Convert world position to UV for height texture sampling
            float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
            float2 heightUV = (worldPos.xz / _WorldSize) + 0.5;

            // Sample height from red channel
            float4 heightData = tex2Dlod(_HeightTexture, float4(heightUV, 0, 0));
            float height = heightData.r * _HeightScale;

            // Apply height displacement
            v.vertex.y += height;

            // Unpack and apply normal from GB channels
            float3 terrainNormal = UnpackNormalRG(heightData.gb);
            v.normal = terrainNormal;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // Sample albedo texture
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;

            // Apply material properties
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
