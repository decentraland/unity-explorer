Shader "Custom/AnimatedBackgroundOptimized"
{
    Properties
    {
        _InnerColor ("Inner Color", Color) = (0.8,0.2,1,1)
        _OuterColor ("Outer Color", Color) = (0.3,0,0.5,1)
        _Radius ("Radius", Range(0,1)) = 0.35
        _Smoothness ("Smoothness", Range(0.01,0.5)) = 0.25
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
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _InnerColor;
            float4 _OuterColor;
            float _Radius;
            float _Smoothness;

            float2 AspectCorrectUV(float2 uv)
            {
                float aspect = _ScreenParams.x / _ScreenParams.y;
                uv = (uv - 0.5) * float2(aspect, 1) + 0.5;
                return uv;
            }

            // Simple 2D noise (value noise)
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = AspectCorrectUV(i.uv);
                float2 centerUV = uv - 0.5;
                centerUV.x *= _ScreenParams.y / _ScreenParams.x;
                float radius = length(centerUV);
                float mask = smoothstep(_Radius + _Smoothness, _Radius, radius);
                float4 color = lerp(_OuterColor, _InnerColor, mask);
                return color;
            }
            ENDCG
        }
    }
    FallBack "Unlit/Color"
} 